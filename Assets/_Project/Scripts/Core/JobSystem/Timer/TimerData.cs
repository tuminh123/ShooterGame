using System;

#region Timer Handle

// Handle không chứa hành vi; phiên bản ngăn callback cũ tác động vào slot đã tái sử dụng.
public readonly struct TimerHandle : IEquatable<TimerHandle>
{
    internal readonly ITimerManager Owner; // Manager đã cấp handle.
    internal readonly int Index; // Ô chứa trạng thái timer.
    internal readonly uint Version; // Phân biệt các lần sử dụng cùng một ô.

    // Chỉ manager tạo handle khi đăng ký.
    internal TimerHandle(ITimerManager owner, int index, uint version)
    { Owner = owner; Index = index; Version = version; }

    // So sánh toàn bộ danh tính, không chỉ index.
    public bool Equals(TimerHandle other)
        => ReferenceEquals(Owner, other.Owner) && Index == other.Index && Version == other.Version;
    public override bool Equals(object obj) => obj is TimerHandle other && Equals(other); // Dùng cùng quy tắc so sánh cho object.
    public override int GetHashCode() => HashCode.Combine(Index, Version); // Hỗ trợ dùng handle làm khóa tra cứu.
}

#endregion

#region Results & Listener

public readonly struct TimerResult
{
    public readonly int DueTickCount; // Các nhịp trong khoảng thời gian còn hiệu lực, kể cả nhịp cuối.
    public readonly bool Completed; // Đã hết thời gian; listener xử lý nhịp cuối trước khi dọn.
    // Đóng gói kết quả tính toán để chuyển về main thread.
    public TimerResult(int dueTickCount, bool completed)
    { DueTickCount = dueTickCount; Completed = completed; }
}

// Listener chỉ được gọi sau Complete trên main thread; có thể hủy/reset timer trong callback.
public interface ITimerListener
{
    // Nhận đúng handle phát sinh kết quả để bên sở hữu loại callback cũ.
    void OnTimer(TimerHandle handle, TimerResult result);
}

#endregion

#region Native State & Events

// Chỉ chứa số và cờ để Burst xử lý; thời gian tính bằng giây.
internal struct TimerState
{
    public double Remaining; // Thời gian hiệu lực còn lại.
    public double Interval; // Khoảng cách giữa các nhịp; 0 nếu chỉ chờ hoàn tất.
    public double Elapsed; // Thời gian đã tích lũy kể từ nhịp trước.
    public uint Version; // Phiên bản của slot đang sử dụng.
    public uint Revision; // Restart vô hiệu kết quả đã tính theo lịch trước, nhưng không đổi handle.
    public byte Active; // 1: slot đang được đăng ký.
    public byte Paused; // 1: giữ nguyên các bộ đếm.
    public byte Completed; // 1: đã hoàn tất, chờ Restart hoặc Cancel.
}

// Event nội bộ của job; không chứa listener hay tham chiếu managed.
internal struct TimerEvent
{
    public int Index; // Slot phát sinh kết quả.
    public uint Version; // Loại kết quả thuộc slot đã hủy/tái sử dụng.
    public uint Revision; // Loại kết quả thuộc lịch đã Restart.
    public int TickCount; // Số nhịp đến hạn trong bước tính này.
    public byte Completed; // 1: thời gian hiệu lực đã hết.
}

#endregion
