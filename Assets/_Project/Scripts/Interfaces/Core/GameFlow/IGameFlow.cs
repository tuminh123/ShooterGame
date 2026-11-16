using System;

#region GameStateReader

/// <summary>Đọc trạng thái luồng game và lỗi chuẩn bị gần nhất.</summary>
public interface IGameStateReader
{
    GameState State { get; }
    Exception LastError { get; }
}

#endregion

#region RunCommands

/// <summary>Bắt đầu, chơi lại hoặc rời lượt chơi về Home.</summary>
public interface IRunCommands
{
    bool StartRun();
    bool RestartRun();
    bool ReturnHome();
}

#endregion

#region RunResultCommands

/// <summary>Yêu cầu kết thúc thắng hoặc thua qua cùng luồng kiểm tra.</summary>
public interface IRunResultCommands
{
    bool WinRun();
    bool LoseRun();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của GameManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IGameFlow
{
    IGameStateReader GetGameStateReader();
    IRunCommands GetRunCommands();
    IRunResultCommands GetRunResultCommands();
}

#endregion
