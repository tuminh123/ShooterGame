using System;

#region RunLevelReader

/// <summary>Đọc EXP, cấp và bonus tạm của lượt cùng các thông báo thay đổi.</summary>
public interface IRunLevelReader
{
    int Level { get; }
    int Experience { get; }
    bool IsMaxLevel { get; }
    StatBonus TotalBonus { get; }
    int ExperienceToNextLevel { get; }
    event Action<int, int> LevelChanged;
    event Action<int, int> ExperienceChanged;
    event Action RunReset;
}

#endregion

#region RunLevelCommands

/// <summary>Nhận EXP hoặc reset cấp của lượt chơi.</summary>
public interface IRunLevelCommands
{
    int AddExperience(int amount);
    void StartNewRun();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của RunLevelService; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IRunLevelService
{
    IRunLevelReader GetRunLevelReader();
    IRunLevelCommands GetRunLevelCommands();
}

#endregion
