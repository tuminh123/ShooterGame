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
    private readonly Wallet wallet; // Trừ tiền nâng level và ghi cùng một lần với tiến trình (giống StageProgress).
    private CharacterLevelsSaveData progress;
    private readonly Dictionary<CharacterType, CharacterLevelsSaveData.Entry> progressLookup = new Dictionary<CharacterType, CharacterLevelsSaveData.Entry>();
    private readonly Dictionary<CharacterType, CharacterDataSO> characters; // Tra template theo type, trung bình O(1).
    private readonly Dictionary<CharacterType, CharacterLevelConfigSO> levelConfigs; // Tra config vĩnh viễn riêng theo cùng type.
    private readonly RarityScaledFormula<CharacterRarity> levelCost; // Gold cần cho mỗi mốc, chung mọi nhân vật, theo độ hiếm.
    private readonly HashSet<CharacterType> starterCharacters; // Sở hữu sẵn từ đầu, không cần cờ owned trong save.
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

    // Công thức giá có thể bị chỉnh sau khi người chơi đã lưu: ép điểm về trong thanh hiện tại thay vì báo lỗi,
    // để save không bị hỏng. Chỉ sửa RAM; lần lưu kế tiếp sẽ ghi giá trị đã ép.
    private void ClampUpgradePoints(CharacterLevelsSaveData.Entry entry)
    {
        if (!levelConfigs.ContainsKey(entry.characterType)) return;
        entry.upgradePoints = TryGetRequired(entry.characterType, entry.permanentLevel, out int required)
            ? Math.Min(entry.upgradePoints, required - 1)
            : 0;
    }

    /// <summary>
    /// Một chỗ tính duy nhất "Gold cần cho mốc <paramref name="level"/> → level + 1" của một nhân vật:
    /// công thức chung trong LevelDatabaseSO × hệ số theo độ hiếm của nhân vật. False khi đã chạm Max Level của config.
    /// </summary>
    private bool TryGetRequired(CharacterType characterType, int level, out int required)
    {
        required = 0;
        if (level < 1 || level >= levelConfigs[characterType].MaxLevel)
            return false;
        // Thanh và điểm đã lưu là int: giá vượt int là cấu hình sai, báo lỗi tràn số thay vì thành số âm.
        required = checked((int)levelCost.Evaluate(level, characters[characterType].Rarity));
        return true;
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
        Wallet wallet,
        CharacterDatabaseSO characterDatabase,
        LevelDatabaseSO levelDatabase)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        if (characterDatabase == null)
            throw new ArgumentNullException(nameof(characterDatabase));
        if (levelDatabase == null)
            throw new ArgumentNullException(nameof(levelDatabase));

        characters = characterDatabase.CreateLookup();
        Dictionary<CharacterType, CharacterLevelConfigSO> configsByType = levelDatabase.CreateTypeLookup(); // Cũng kiểm tra công thức giá.
        levelCost = levelDatabase.PermanentLevelCost;
        starterCharacters = new HashSet<CharacterType>(levelDatabase.StarterCharacters);
        UpgradeCurrency = levelDatabase.PermanentLevelCurrency;
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

    /// <summary>
    /// Bonus chỉ số vĩnh viễn của nhân vật = bonus level (CharacterLevelConfigSO) + chỉ số dự phòng của các sao đã mở
    /// (CharacterDataSO.GetStarBonus: chỉ sao không có bước skill). Tính lại từ data mỗi lần, không sửa template.
    /// </summary>
    public StatBonus GetTotalBonus(CharacterType characterType)
    {
        StatBonus levelBonus = GetConfig(characterType).GetTotalBonus(ReadLevel(characterType));
        StatBonus starBonus = GetCharacter(characterType).GetStarBonus(GetStars(characterType));
        return levelBonus + starBonus;
    }

    /// <summary>Nguyên liệu đã nạp vào thanh của cấp hiện tại.</summary>
    public int GetUpgradePoints(CharacterType characterType)
    {
        GetConfig(characterType);
        return ReadUpgradePoints(characterType);
    }

    /// <summary>Loại tiền nâng level vĩnh viễn (chung mọi nhân vật, đọc từ LevelDatabaseSO).</summary>
    public CurrencyType UpgradeCurrency { get; }

    /// <summary>Gold cần để đầy thanh của cấp hiện tại; false nếu đã đạt hoặc vượt trần config.</summary>
    public bool TryGetRequiredPoints(CharacterType characterType, out int required)
    {
        GetConfig(characterType);
        return TryGetRequired(characterType, ReadLevel(characterType), out required);
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
    /// Trả <paramref name="costs"/> rồi nạp <paramref name="amount"/> nguyên liệu vào thanh: đầy thanh thì lên cấp và chuyển phần dư
    /// sang cấp sau (có thể lên nhiều cấp một lần), chạm trần thì bỏ phần dư. Tiền, level và điểm được ghi trong <b>một lần save</b>.
    /// True nghĩa là đã trả tiền và nạp; false nếu đã ở trần hoặc không đủ tiền. <paramref name="saved"/> cho biết đã ghi file chưa.
    /// </summary>
    /// <remarks>
    /// Kiểm tra đủ tiền trước khi đổi RAM nên thiếu tiền thì không có gì thay đổi. <see cref="LevelChanged"/> chỉ phát khi thật sự lên cấp.
    /// Nếu saved=false, chỉ thử save lại, không nạp lại để tránh trừ tiền hai lần.
    /// </remarks>
    public bool TryAddUpgradePoints(CharacterType characterType, int amount, IReadOnlyList<CurrencyCost> costs, out bool saved)
    {
        EnsureAvailable();
        if (changingLevel)
            throw new InvalidOperationException("Cannot change permanent level from a level notification callback.");
        if (amount < 1)
            throw new ArgumentOutOfRangeException(nameof(amount));
        saved = false;
        CharacterLevelConfigSO config = GetConfig(characterType);
        int previousLevel = ReadLevel(characterType);
        if (previousLevel >= config.MaxLevel || !wallet.CanAfford(costs))
            return false;

        changingLevel = true;
        try
        {
            // Cùng thuật toán với RunLevelProgress.AddExperience: trừ dần yêu cầu từng cấp cho tới khi không đủ.
            long remaining = (long)ReadUpgradePoints(characterType) + amount;
            int newLevel = previousLevel;
            while (TryGetRequired(characterType, newLevel, out int required) && remaining >= required)
            {
                remaining -= required;
                newLevel++;
            }

            CharacterLevelsSaveData.Entry entry = GetOrAddEntry(characterType);
            entry.permanentLevel = newLevel;
            entry.upgradePoints = newLevel >= config.MaxLevel ? 0 : (int)remaining;
            CharacterLevelsSaveData.Validate(progress);
            saved = wallet.SpendWith(costs, GameConstants.SaveKeys.CharacterLevels, progress.Clone());
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

    #endregion

    #region Collection Storage (CharacterCollection dùng)

    // Sở hữu, bản trùng và sao lưu cùng Entry với level vì system này là chủ duy nhất của key character_levels
    // (hai chủ cùng ghi một key sẽ đè nhau). Luật ghép/phân rã/nâng sao nằm ở CharacterCollection; ở đây chỉ đọc/ghi dữ liệu.

    /// <summary>Đã sở hữu: nhân vật khởi đầu (LevelDatabaseSO) hoặc đã gacha/ghép.</summary>
    public bool IsOwned(CharacterType characterType)
    {
        GetConfig(characterType);
        if (starterCharacters.Contains(characterType)) return true;
        return TryReadEntry(characterType, out CharacterLevelsSaveData.Entry entry) && entry.owned;
    }

    /// <summary>Số bản trùng đang giữ; nhân vật chưa có bản ghi là 0.</summary>
    public int GetCopies(CharacterType characterType)
    {
        GetConfig(characterType);
        return TryReadEntry(characterType, out CharacterLevelsSaveData.Entry entry) ? entry.copies : 0;
    }

    /// <summary>Bản ghi để CharacterCollection sửa tại chỗ (tạo nếu chưa có); sửa xong phải ghi bằng snapshot.</summary>
    internal CharacterLevelsSaveData.Entry EditEntry(CharacterType characterType)
    {
        EnsureAvailable();
        GetConfig(characterType);
        return GetOrAddEntry(characterType);
    }

    /// <summary>Bản sao tiến trình đã kiểm tra, để ghi cùng một lần với ví.</summary>
    internal CharacterLevelsSaveData CreateSnapshot()
    {
        EnsureAvailable();
        CharacterLevelsSaveData.Validate(progress);
        return progress.Clone();
    }

    /// <summary>Báo lỗi ghi file (RAM đã đổi, snapshot chờ ghi lại) theo cùng event với nâng level.</summary>
    internal void ReportSaveFailure(CharacterType characterType)
        => SaveFailed?.Invoke(characterType, ReadLevel(characterType), saves.LastError);

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
