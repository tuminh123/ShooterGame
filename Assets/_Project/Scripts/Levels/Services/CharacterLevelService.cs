using System;
using System.Collections.Generic;
using Zenject;

// * Xử lý level vĩnh viễn theo nhân vật; LevelManager dùng service này để tính thưởng và tăng cấp.
public sealed class CharacterLevelService : IInitializable, IDisposable
{
    #region Dependencies & Character Lookups

    private readonly SaveManager saveManager; // Chỉ đọc/ghi payload; service sở hữu tiến trình level.
    private CharacterLevelsSaveData progress;
    private readonly Dictionary<CharacterType, CharacterLevelsSaveData.Entry> progressLookup = new Dictionary<CharacterType, CharacterLevelsSaveData.Entry>();
    private readonly Dictionary<CharacterType, CharacterDataSO> characters;                // * Tra template theo type, trung bình O(1).
    private readonly Dictionary<CharacterType, CharacterLevelConfigSO> levelConfigs;       // * Tra config vĩnh viễn riêng theo cùng type.
    private bool changingLevel;                                                    // * Chặn gọi tăng cấp lồng nhau từ callback sự kiện.
    private bool disposed;                                                         // * Bảo vệ vòng đời service do Zenject quản lý.

    #endregion

    public void Initialize()
    {
        if (!saveManager.IsLoaded || progress != null) return;
        progress = saveManager.Load(GameConstants.SaveKeys.CharacterLevels, new CharacterLevelsSaveData(), ValidateData);
        foreach (CharacterLevelsSaveData.Entry entry in progress.characters) progressLookup.Add(entry.characterType, entry);
    }

    internal static void ValidateData(CharacterLevelsSaveData value)
    {
        if (value == null || value.characters == null) throw new InvalidOperationException("Missing character level data.");
        var types = new HashSet<CharacterType>();
        foreach (CharacterLevelsSaveData.Entry entry in value.characters)
            if (entry == null || entry.characterType == CharacterType.None ||
                !Enum.IsDefined(typeof(CharacterType), entry.characterType) || entry.permanentLevel < 1 || !types.Add(entry.characterType))
                throw new InvalidOperationException("Character progress must have unique defined types and positive levels.");
    }

    private int ReadLevel(CharacterType type)
    {
        if (progress == null || !saveManager.IsLoaded) throw new InvalidOperationException("Character level data has not loaded.");
        return progressLookup.TryGetValue(type, out CharacterLevelsSaveData.Entry entry) ? entry.permanentLevel : 1;
    }

    #region Progress Notifications

    public event Action<CharacterType, int, int> LevelChanged;       // * Type, level cũ, level mới; phát sau khi cập nhật RAM và thử save.
    public event Action<CharacterType, int, Exception> SaveFailed;   // * Type, level mới, lỗi IO; level vẫn đã tăng trong RAM và còn dirty.

    #endregion

    #region Service Initialization

    // * Chỉ dựng cache và kiểm tra cấu hình; không đọc hồ sơ trước khi SaveManager.Initialize chạy.
    public CharacterLevelService(SaveManager saveManager, CharacterDatabaseSO characterDatabase, LevelDatabaseSO levelDatabase)
    {
        this.saveManager = saveManager ?? throw new ArgumentNullException(nameof(saveManager));
        if (characterDatabase == null)
            throw new ArgumentNullException(nameof(characterDatabase));
        if (levelDatabase == null)
            throw new ArgumentNullException(nameof(levelDatabase));

        characters = characterDatabase.CreateLookup();
        Dictionary<CharacterType, CharacterLevelConfigSO> configsByType = levelDatabase.CreateTypeLookup();
        levelConfigs = new Dictionary<CharacterType, CharacterLevelConfigSO>(characters.Count);

        // * Vòng duyệt thứ hai ghép type nhân vật với config, dùng chính enum đó làm key runtime/save.
        foreach (CharacterDataSO character in characters.Values)
        {
            // * Database chứa cả Enemy; chỉ Player có tiến trình level vĩnh viễn và trang bị trong hồ sơ.
            if (character.Team != CharacterTeam.Player)
                continue;
            if (!configsByType.TryGetValue(character.CharacterType, out CharacterLevelConfigSO config))
                throw new InvalidOperationException($"Missing level config for character type: {character.CharacterType}.");
            levelConfigs.Add(character.CharacterType, config);
        }
    }

    #endregion

    #region Character Progress Queries

    // * Đọc template theo type; Type không tồn tại sẽ báo lỗi để tránh tính nhầm nhân vật.
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

    // * Tính tổng bonus vĩnh viễn từ level hiện tại, không sửa template hoặc chỉ số runtime.
    public StatBonus GetTotalBonus(CharacterType characterType)
    {
        CharacterLevelConfigSO config = GetConfig(characterType);
        return config.GetTotalBonus(ReadLevel(characterType));
    }

    // * Lấy chi phí cấp tiếp theo; false nếu đã đạt hoặc vượt trần config.
    public bool TryGetUpgradeCost(CharacterType characterType, out long cost)
    {
        CharacterLevelConfigSO config = GetConfig(characterType);
        return config.TryGetUpgradeCost(ReadLevel(characterType), out cost);
    }

    // * Đọc config để UI hiển thị trần, bonus mỗi cấp và công thức giá.
    public CharacterLevelConfigSO GetConfig(CharacterType characterType)
    {
        GetCharacter(characterType);
        if (!levelConfigs.TryGetValue(characterType, out CharacterLevelConfigSO config))
            throw new KeyNotFoundException($"Character has no permanent level config: {characterType}.");
        return config;
    }

    #endregion

    #region Permanent Level Advancement

    // * Thử lưu hồ sơ hiện tại sau lỗi IO, không tăng level hoặc phát lại sự kiện tăng cấp.
    public bool SaveProgress()
    {
        EnsureAvailable();
        if (progress == null) throw new InvalidOperationException("Character level data has not loaded.");
        ValidateData(progress);
        return saveManager.Save(GameConstants.SaveKeys.CharacterLevels, progress.Clone());
    }

    // * Tăng một cấp rồi gọi save; true nghĩa là level đã tăng, saved cho biết đã ghi file thành công hay chưa.
    // Bên gọi kiểm tra điều kiện/thanh toán qua CurrencyService; service này chỉ thay đổi level.
    // ! Nếu saved=false, chỉ thử save lại, không gọi tăng cấp lại để tránh nhận thêm một cấp ngoài ý muốn.
    public bool TryIncreaseLevel(CharacterType characterType, out bool saved)
    {
        EnsureAvailable();
        if (changingLevel)
            throw new InvalidOperationException("Cannot change permanent level from a level notification callback.");
        saved = false;
        CharacterLevelConfigSO config = GetConfig(characterType);
        int previousLevel = ReadLevel(characterType);
        if (previousLevel >= config.MaxLevel)
            return false;

        changingLevel = true;
        try
        {
            int newLevel = previousLevel + 1;
            if (!progressLookup.TryGetValue(characterType, out CharacterLevelsSaveData.Entry entry))
            {
                entry = new CharacterLevelsSaveData.Entry { characterType = characterType };
                progressLookup.Add(characterType, entry);
                progress.characters.Add(entry);
            }
            entry.permanentLevel = newLevel;
            saved = SaveProgress();
            Exception saveError = saveManager.LastError;
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

    #region Service Cleanup

    // * Hủy callback và cache; SaveManager vẫn tự quản lý vòng đời hồ sơ và việc thử lưu dirty.
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

    // * Không cho dùng lại service sau khi scene container giải phóng.
    private void EnsureAvailable()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(CharacterLevelService));
    }

    #endregion
}
