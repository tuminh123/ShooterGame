using System;
using System.Collections.Generic;

/// <summary>
/// Level vĩnh viễn theo nhân vật Player: đọc/ghi key <c>character_levels</c>, bonus, và thanh EXP nâng cấp
/// (nạp nguyên liệu → đầy thanh thì lên cấp, phần dư chuyển sang cấp sau — giống EXP trong trận).
/// </summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo; thay cho CharacterLevelService + LevelManager cũ.
/// Miền khác chỉ thấy tổng bonus qua <see cref="IPermanentBonuses"/>; màn Character nâng cấp qua <see cref="ICharacterUpgrades"/>.
/// </remarks>
public sealed class CharacterLevels : IDisposable
{
    #region Dependencies & Character Lookups
    private readonly ISaveStore saves; // Chỉ đọc/ghi payload; system này sở hữu tiến trình level.
    private CharacterLevelsSaveData progress;
    private readonly Dictionary<CharacterType, CharacterLevelsSaveData.Entry> progressLookup = new Dictionary<CharacterType, CharacterLevelsSaveData.Entry>();
    private readonly Dictionary<CharacterType, CharacterDataSO> characters; // Tra template theo type, trung bình O(1).
    private readonly Dictionary<CharacterType, CharacterLevelConfigSO> levelConfigs; // Tra config vĩnh viễn riêng theo cùng type.
    private bool changingLevel; // Chặn gọi tăng cấp lồng nhau từ callback sự kiện.
    private bool disposed; // Chặn dùng lại sau khi ProgressionManager đã Dispose.

    #endregion

    /// <summary>Đọc tiến trình một lần sau khi Save đã mở file.</summary>
    public void Load()
    {
        if (!saves.IsLoaded || progress != null) return;
        progress = saves.Load(GameConstants.SaveKeys.CharacterLevels, new CharacterLevelsSaveData(), CharacterLevelsSaveData.Validate);
        foreach (CharacterLevelsSaveData.Entry entry in progress.characters)
        {
            progressLookup.Add(entry.characterType, entry);
            ClampUpgradePoints(entry);
        }
    }

    // Config có thể bị chỉnh A/B sau khi người chơi đã lưu: ép điểm về trong thanh hiện tại thay vì báo lỗi,
    // để save không bị hỏng. Chỉ sửa RAM; lần lưu kế tiếp sẽ ghi giá trị đã ép.
    private void ClampUpgradePoints(CharacterLevelsSaveData.Entry entry)
    {
        if (!levelConfigs.TryGetValue(entry.characterType, out CharacterLevelConfigSO config)) return;
        entry.upgradePoints = config.TryGetRequired(entry.permanentLevel, out int required)
            ? Math.Min(entry.upgradePoints, required - 1)
            : 0;
    }

    private int ReadLevel(CharacterType type)
        => TryReadEntry(type, out CharacterLevelsSaveData.Entry entry) ? entry.permanentLevel : 1;

    private int ReadUpgradePoints(CharacterType type)
        => TryReadEntry(type, out CharacterLevelsSaveData.Entry entry) ? entry.upgradePoints : 0;

    // Nhân vật chưa có bản ghi là level 1, thanh rỗng; việc đọc không tự thêm bản ghi.
    private bool TryReadEntry(CharacterType type, out CharacterLevelsSaveData.Entry entry)
    {
        if (progress == null || !saves.IsLoaded) throw new InvalidOperationException("Character level data has not loaded.");
        return progressLookup.TryGetValue(type, out entry);
    }

    #region Progress Notifications

    public event Action<CharacterType, int, int> LevelChanged; // Type, level cũ, level mới; phát sau khi cập nhật RAM và thử save.
    public event Action<CharacterType, int, Exception> SaveFailed; // Type, level mới, lỗi IO; level vẫn đã tăng trong RAM và còn dirty.

    #endregion

    #region Service Initialization

    // Chỉ dựng cache và kiểm tra cấu hình; không đọc hồ sơ trước khi SaveManager.Initialize chạy.
    public CharacterLevels(
        ISaveStore saves,
        CharacterDatabaseSO characterDatabase,
        LevelDatabaseSO levelDatabase)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        if (characterDatabase == null)
            throw new ArgumentNullException(nameof(characterDatabase));
        if (levelDatabase == null)
            throw new ArgumentNullException(nameof(levelDatabase));

        characters = characterDatabase.CreateLookup();
        Dictionary<CharacterType, CharacterLevelConfigSO> configsByType = levelDatabase.CreateTypeLookup();
        levelConfigs = new Dictionary<CharacterType, CharacterLevelConfigSO>(characters.Count);

        // Vòng duyệt thứ hai ghép type nhân vật với config, dùng chính enum đó làm key runtime/save.
        foreach (CharacterDataSO character in characters.Values)
        {
            // Database chứa cả Enemy; chỉ Player có tiến trình level vĩnh viễn và trang bị trong hồ sơ.
            if (character.Team != CharacterTeam.Player)
                continue;
            if (!configsByType.TryGetValue(character.CharacterType, out CharacterLevelConfigSO config))
                throw new InvalidOperationException($"Missing level config for character type: {character.CharacterType}.");
            levelConfigs.Add(character.CharacterType, config);
        }
    }

    #endregion

    #region Character Progress Queries

    // Đọc template theo type; Type không tồn tại sẽ báo lỗi để tránh tính nhầm nhân vật.
    public CharacterDataSO GetCharacter(CharacterType characterType)
    {
        EnsureAvailable();
        if (!characters.TryGetValue(characterType, out CharacterDataSO character))
            throw new KeyNotFoundException($"Unknown character type: {characterType}.");
        return character;
    }

    // Nhân vật chưa có bản ghi được xem là level 1.
    public int GetLevel(CharacterType characterType)
    {
        GetConfig(characterType);
        return ReadLevel(characterType);
    }

    // Tổng bonus vĩnh viễn = bonus theo level + perk của các sao đã mở; tính lại từ config, không sửa template.
    public StatBonus GetTotalBonus(CharacterType characterType)
    {
        CharacterLevelConfigSO config = GetConfig(characterType);
        return config.GetTotalBonus(ReadLevel(characterType)) + config.GetStarBonus(GetStars(characterType));
    }

    /// <summary>Perk theo thứ tự sao của nhân vật (đọc từ config).</summary>
    public IReadOnlyList<StarPerk> GetStarPerks(CharacterType characterType) => GetConfig(characterType).StarPerks;

    /// <summary>Nguyên liệu đã nạp vào thanh của cấp hiện tại.</summary>
    public int GetUpgradePoints(CharacterType characterType)
    {
        GetConfig(characterType);
        return ReadUpgradePoints(characterType);
    }

    /// <summary>Nguyên liệu cần để đầy thanh của cấp hiện tại; false nếu đã đạt hoặc vượt trần config.</summary>
    public bool TryGetRequiredPoints(CharacterType characterType, out int required)
    {
        CharacterLevelConfigSO config = GetConfig(characterType);
        return config.TryGetRequired(ReadLevel(characterType), out required);
    }

    /// <summary>
    /// Tổng nguyên liệu còn thiếu để chạm level tối đa; 0 khi đã ở trần.
    /// Dùng để lần nạp cuối chỉ tốn đúng phần còn thiếu, không làm mất tiền thật của người chơi.
    /// </summary>
    public long GetPointsToMaxLevel(CharacterType characterType)
    {
        CharacterLevelConfigSO config = GetConfig(characterType);
        int level = ReadLevel(characterType);
        long remaining = -ReadUpgradePoints(characterType);
        for (int current = level; config.TryGetRequired(current, out int required); current++)
            remaining += required;
        return Math.Max(0L, remaining);
    }

    // Đọc config để UI hiển thị trần, bonus mỗi cấp và công thức giá.
    public CharacterLevelConfigSO GetConfig(CharacterType characterType)
    {
        GetCharacter(characterType);
        if (!levelConfigs.TryGetValue(characterType, out CharacterLevelConfigSO config))
            throw new KeyNotFoundException($"Character has no permanent level config: {characterType}.");
        return config;
    }

    #endregion

    #region Permanent Level Advancement

    // Thử lưu hồ sơ hiện tại sau lỗi IO, không tăng level hoặc phát lại sự kiện tăng cấp.
    public bool SaveProgress()
    {
        EnsureAvailable();
        if (progress == null) throw new InvalidOperationException("Character level data has not loaded.");
        CharacterLevelsSaveData.Validate(progress);
        return saves.Save(GameConstants.SaveKeys.CharacterLevels, progress.Clone());
    }

    /// <summary>
    /// Nạp <paramref name="amount"/> nguyên liệu vào thanh: đầy thanh thì lên cấp và chuyển phần dư sang cấp sau
    /// (có thể lên nhiều cấp một lần), chạm trần thì bỏ phần dư. Level và điểm được ghi trong <b>một lần save</b>.
    /// True nghĩa là đã nạp; <paramref name="saved"/> cho biết đã ghi file thành công hay chưa.
    /// </summary>
    /// <remarks>
    /// Bên gọi kiểm tra điều kiện/thanh toán; system này chỉ đổi tiến trình. <see cref="LevelChanged"/> chỉ phát khi thật sự lên cấp.
    /// Nếu saved=false, chỉ thử save lại, không nạp lại để tránh nhận thêm điểm ngoài ý muốn.
    /// </remarks>
    public bool TryAddUpgradePoints(CharacterType characterType, int amount, out bool saved)
    {
        EnsureAvailable();
        if (changingLevel)
            throw new InvalidOperationException("Cannot change permanent level from a level notification callback.");
        if (amount < 1)
            throw new ArgumentOutOfRangeException(nameof(amount));
        saved = false;
        CharacterLevelConfigSO config = GetConfig(characterType);
        int previousLevel = ReadLevel(characterType);
        if (previousLevel >= config.MaxLevel)
            return false;

        changingLevel = true;
        try
        {
            // Cùng thuật toán với RunLevelProgress.AddExperience: trừ dần yêu cầu từng cấp cho tới khi không đủ.
            long remaining = (long)ReadUpgradePoints(characterType) + amount;
            int newLevel = previousLevel;
            while (config.TryGetRequired(newLevel, out int required) && remaining >= required)
            {
                remaining -= required;
                newLevel++;
            }

            CharacterLevelsSaveData.Entry entry = GetOrAddEntry(characterType);
            entry.permanentLevel = newLevel;
            entry.upgradePoints = newLevel >= config.MaxLevel ? 0 : (int)remaining;
            saved = SaveProgress();
            Exception saveError = saves.LastError;
            if (newLevel != previousLevel)
                LevelChanged?.Invoke(characterType, previousLevel, newLevel);
            if (!saved)
                SaveFailed?.Invoke(characterType, newLevel, saveError);
            return true;
        }
        finally
        {
            changingLevel = false;
        }
    }

    #endregion

    #region Stars

    /// <summary>Số sao đã mở; nhân vật chưa có bản ghi là 0.</summary>
    public int GetStars(CharacterType characterType)
    {
        GetConfig(characterType);
        return TryReadEntry(characterType, out CharacterLevelsSaveData.Entry entry) ? entry.stars : 0;
    }

    /// <summary>
    /// Mở thêm một sao nếu chưa tới <paramref name="maxStars"/> rồi save; true nghĩa là đã tăng,
    /// <paramref name="saved"/> cho biết đã ghi file chưa. Bên gọi kiểm tra điều kiện/thanh toán.
    /// </summary>
    // Lưu cùng key với level vì system này là chủ duy nhất của character_levels (hai chủ cùng ghi một key sẽ đè nhau).
    public bool TryAddStar(CharacterType characterType, int maxStars, out bool saved)
    {
        EnsureAvailable();
        saved = false;
        if (GetStars(characterType) >= maxStars) return false;
        GetOrAddEntry(characterType).stars++;
        saved = SaveProgress();
        if (!saved) SaveFailed?.Invoke(characterType, ReadLevel(characterType), saves.LastError);
        return true;
    }

    #endregion

    #region Private Helpers

    // Bản ghi chỉ được tạo khi có thay đổi thật, để việc đọc không làm phình save.
    private CharacterLevelsSaveData.Entry GetOrAddEntry(CharacterType characterType)
    {
        if (progressLookup.TryGetValue(characterType, out CharacterLevelsSaveData.Entry entry)) return entry;
        entry = new CharacterLevelsSaveData.Entry { characterType = characterType };
        progressLookup.Add(characterType, entry);
        progress.characters.Add(entry);
        return entry;
    }

    #endregion

    #region Service Cleanup

    // Hủy callback và cache; SaveManager vẫn tự quản lý vòng đời hồ sơ và việc thử lưu dirty.
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        LevelChanged = null;
        SaveFailed = null;
        characters.Clear();
        levelConfigs.Clear();
        progressLookup.Clear();
        progress = null;
    }

    // Không cho dùng lại service sau khi scene container giải phóng.
    private void EnsureAvailable()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(CharacterLevels));
    }

    #endregion
}
