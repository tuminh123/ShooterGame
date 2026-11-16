using System;

#region CharacterCatalog

/// <summary>Tra template và cấu hình tăng trưởng theo loại nhân vật.</summary>
public interface ICharacterCatalog
{
    CharacterDataSO GetCharacter(CharacterType characterType);
    CharacterLevelConfigSO GetConfig(CharacterType characterType);
}

#endregion

#region CharacterLevelReader

/// <summary>Đọc cấp, bonus, chi phí nâng và thông báo cấp vĩnh viễn.</summary>
public interface ICharacterLevelReader
{
    event Action<CharacterType, int, int> LevelChanged;
    event Action<CharacterType, int, Exception> SaveFailed;
    int GetLevel(CharacterType characterType);
    StatBonus GetTotalBonus(CharacterType characterType);
    bool TryGetUpgradeCost(CharacterType characterType, out long cost);
}

#endregion

#region CharacterLevelCommands

/// <summary>Tăng cấp vĩnh viễn hoặc thử lưu tiến trình đã cập nhật.</summary>
public interface ICharacterLevelCommands
{
    bool SaveProgress();
    bool TryIncreaseLevel(CharacterType characterType, out bool saved);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của CharacterLevelService; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ICharacterLevelService
{
    ICharacterCatalog GetCharacterCatalog();
    ICharacterLevelReader GetCharacterLevelReader();
    ICharacterLevelCommands GetCharacterLevelCommands();
}

#endregion
