using System;
using System.Collections.Generic;

// * Xử lý level vĩnh viễn theo nhân vật; LevelManager dùng service này để tính thưởng và tăng cấp.
public sealed class CharacterLevelService : IDisposable
{
    #region Dependencies & Character Lookups

    private readonly SaveManager saveManager;                                      // * Nguồn level hiện tại duy nhất, không copy level sang cache khác.
    private readonly Dictionary<CharacterType, CharacterDataSO> characters;                // * Tra template theo type, trung bình O(1).
    private readonly Dictionary<CharacterType, PermanentLevelConfigSO> levelConfigs;       // * Tra config vĩnh viễn riêng theo cùng type.
    private bool changingLevel;                                                    // * Chặn gọi tăng cấp lồng nhau từ callback sự kiện.
    private bool disposed;                                                         // * Bảo vệ vòng đời service do Zenject quản lý.

    #endregion

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
        Dictionary<CharacterType, PermanentLevelConfigSO> configsByType = levelDatabase.CreateTypeLookup();
        levelConfigs = new Dictionary<CharacterType, PermanentLevelConfigSO>(characters.Count);

        // * Vòng duyệt thứ hai ghép type nhân vật với config, dùng chính enum đó làm key runtime/save.
        foreach (CharacterDataSO character in characters.Values)
        {
            // * Database chứa cả Enemy; chỉ Player có tiến trình level vĩnh viễn và trang bị trong hồ sơ.
            if (character.Team != CharacterTeam.Player)
                continue;
            if (!configsByType.TryGetValue(character.CharacterType, out PermanentLevelConfigSO config))
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

    // * Trả level đã lưu của nhân vật có config vĩnh viễn; chưa có bản ghi thì SaveManager trả 1.
    public int GetLevel(CharacterType characterType)
    {
        GetConfig(characterType);
        return saveManager.GetPermanentLevel(characterType);
    }

    // * Tính tổng bonus vĩnh viễn từ level hiện tại, không sửa template hoặc chỉ số runtime.
    public StatBonus GetTotalBonus(CharacterType characterType)
    {
        PermanentLevelConfigSO config = GetConfig(characterType);
        return config.GetTotalBonus(saveManager.GetPermanentLevel(characterType));
    }

    // * Lấy chi phí cấp tiếp theo; false nếu đã đạt hoặc vượt trần config.
    public bool TryGetUpgradeCost(CharacterType characterType, out long cost)
    {
        PermanentLevelConfigSO config = GetConfig(characterType);
        return config.TryGetUpgradeCost(saveManager.GetPermanentLevel(characterType), out cost);
    }

    // * Đọc config để UI hiển thị trần, bonus mỗi cấp và công thức giá.
    public PermanentLevelConfigSO GetConfig(CharacterType characterType)
    {
        GetCharacter(characterType);
        if (!levelConfigs.TryGetValue(characterType, out PermanentLevelConfigSO config))
            throw new KeyNotFoundException($"Character has no permanent level config: {characterType}.");
        return config;
    }

    #endregion

    #region Permanent Level Advancement

    // * Thử lưu hồ sơ hiện tại sau lỗi IO, không tăng level hoặc phát lại sự kiện tăng cấp.
    public bool SaveProgress()
    {
        EnsureAvailable();
        return saveManager.Save();
    }

    // * Tăng một cấp rồi gọi save; true nghĩa là level đã tăng, saved cho biết đã ghi file thành công hay chưa.
    // ! Bên gọi phải xác nhận điều kiện/chi phí trước; chưa có ví nên hàm không kiểm tra hoặc trừ tài nguyên.
    // ! Nếu saved=false, chỉ thử save lại, không gọi tăng cấp lại để tránh nhận thêm một cấp ngoài ý muốn.
    public bool TryIncreaseLevel(CharacterType characterType, out bool saved)
    {
        EnsureAvailable();
        if (changingLevel)
            throw new InvalidOperationException("Cannot change permanent level from a level notification callback.");
        saved = false;
        PermanentLevelConfigSO config = GetConfig(characterType);
        int previousLevel = saveManager.GetPermanentLevel(characterType);
        if (previousLevel >= config.MaxLevel)
            return false;

        changingLevel = true;
        try
        {
            int newLevel = previousLevel + 1;
            saveManager.SetPermanentLevel(characterType, newLevel);
            saved = saveManager.Save();
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
    }

    // * Không cho dùng lại service sau khi scene container giải phóng.
    private void EnsureAvailable()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(CharacterLevelService));
    }

    #endregion
}
