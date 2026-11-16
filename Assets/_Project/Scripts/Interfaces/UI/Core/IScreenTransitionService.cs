#region ScreenTransition

/// <summary>Chạy chuyển cảnh UI rồi gửi lệnh bắt đầu, chơi lại hoặc về Home.</summary>
public interface IScreenTransition
{
    bool IsTransitioning { get; }
    bool StartRun();
    bool RestartRun();
    bool ReturnHome();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của ScreenTransitionController; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IScreenTransitionService
{
    IScreenTransition GetScreenTransition();
}

#endregion
