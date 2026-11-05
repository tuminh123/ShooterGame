// Hợp đồng dùng timer; vòng Tick/Dispose thuộc Zenject, không thuộc bên đăng ký.
public interface ITimerManager
{
    #region Time & Registration

    float DeltaTime { get; } // Thời gian gameplay của bước hiện tại.
    // Tạo timer có hạn, tùy chọn nhịp và trạng thái pause ban đầu.
    TimerHandle Register(float duration, ITimerListener listener, float interval = 0f, bool paused = false);
    // Tạo lịch lặp đến khi hủy; firstDelay cho phép chia pha các đối tượng.
    TimerHandle RegisterPeriodic(float interval, ITimerListener listener, float firstDelay);
    // Loại handle cũ khi slot đã hủy hoặc tái sử dụng.
    bool IsRegistered(TimerHandle handle);
    // Trả 0 nếu handle không còn hợp lệ.
    float GetRemaining(TimerHandle handle);

    #endregion

    #region Commands

    // Làm mới tuổi thọ; chỉ reset nhịp khi được yêu cầu.
    bool Restart(TimerHandle handle, float duration, bool resetInterval = false);
    // Pause riêng timer, không đổi đồng hồ toàn game.
    bool SetPaused(TimerHandle handle, bool paused);
    // Giải phóng đăng ký, không phát callback hoàn tất.
    bool Cancel(TimerHandle handle);

    #endregion
}
