using System;

#region CharacterLevelCatalog

/// <summary>Tra template nhân vật và cấu hình cấp vĩnh viễn qua facade.</summary>
public interface ICharacterLevelCatalog
{
    CharacterDataSO GetCharacter(CharacterType characterType);
    CharacterLevelConfigSO GetPermanentConfig(CharacterType characterType);
}

#endregion

#region PermanentLevelReader

/// <summary>Đọc cấp vĩnh viễn, bonus và chi phí nâng qua facade.</summary>
public interface IPermanentLevelReader
{
    event Action<CharacterType, int, int> CharacterLevelChanged;
    event Action<CharacterType, int, Exception> CharacterLevelSaveFailed;
    int GetPermanentLevel(CharacterType characterType);
    StatBonus GetPermanentBonus(CharacterType characterType);
    bool TryGetUpgradeCost(CharacterType characterType, out long cost);
}

#endregion

#region PermanentLevelCommands

/// <summary>Nâng cấp vĩnh viễn và lưu qua đầu mối tiến trình.</summary>
public interface IPermanentLevelCommands
{
    bool TryIncreasePermanentLevel(CharacterType characterType, out bool saved);
    bool SaveCharacterProgress();
}

#endregion

#region RunProgress

/// <summary>Đọc cấp/EXP của lượt và theo dõi tăng cấp hoặc reset.</summary>
public interface IRunProgress
{
    event Action<int, int> RunLevelChanged;
    event Action<int, int> RunExperienceChanged;
    event Action RunReset;
    int RunLevel { get; }
    int RunExperience { get; }
    int RunExperienceToNextLevel { get; }
    bool IsRunMaxLevel { get; }
    StatBonus RunBonus { get; }
}

#endregion

#region RunProgressCommands

/// <summary>Reset tiến trình lượt hoặc cộng EXP qua facade.</summary>
public interface IRunProgressCommands
{
    void StartNewRun();
    int AddRunExperience(int amount);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của LevelManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ILevelService
{
    ICharacterLevelCatalog GetCharacterLevelCatalog();
    IPermanentLevelReader GetPermanentLevelReader();
    IPermanentLevelCommands GetPermanentLevelCommands();
    IRunProgress GetRunProgress();
    IRunProgressCommands GetRunProgressCommands();
}

#endregion
