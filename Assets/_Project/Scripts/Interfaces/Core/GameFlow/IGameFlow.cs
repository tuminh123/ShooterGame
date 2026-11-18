using System;

#region GameStateReader

/// <summary>Đọc trạng thái luồng game, lỗi chuẩn bị gần nhất và theo dõi khi state thay đổi.</summary>
public interface IGameStateReader
{
    GameState State { get; }
    Exception LastError { get; }

    /// <summary>
    /// (state trước, state sau), phát sau khi <see cref="State"/> đã đổi. Chỉ tầng UI được nghe event này;
    /// hệ thống gameplay bên dưới được GameManager gọi trực tiếp.
    /// </summary>
    event Action<GameState, GameState> StateChanged;
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
