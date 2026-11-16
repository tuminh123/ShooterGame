using System;

#region RunStatistics

/// <summary>Đọc thời gian, vàng nhặt và số quái hạ trong lượt chơi.</summary>
public interface IRunStatistics
{
    int Gold { get; }
    int ElapsedSeconds { get; }
    int EnemiesDefeated { get; }
    event Action<int> GoldChanged;
    event Action<int> TimeChanged;
    event Action<int> EnemiesDefeatedChanged;
}

#endregion

#region RunGoldRecorder

/// <summary>Ghi thống kê vàng; không cộng thêm vào ví người chơi.</summary>
public interface IRunGoldRecorder
{
    void AddGold(int amount);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của RunSessionManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IRunSession
{
    IRunStatistics GetRunStatistics();
    IRunGoldRecorder GetRunGoldRecorder();
}

#endregion
