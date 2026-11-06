using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Zenject;

// Một bộ đếm chung cho cooldown, effect và lifetime. Không biết hành vi gameplay của listener.
public sealed class TimerManager : ITimerManager, ITickable, IDisposable
{
    #region Dependencies & State

    private readonly ITimeSource timeSource; // Nguồn deltaTime gameplay, thay được qua DI.
    private readonly Stack<int> freeSlots = new Stack<int>(); // Các ô đã hủy, ưu tiên dùng lại khi Register.
    private NativeArray<TimerState> states; // Trạng thái số để job cập nhật song song.
    private NativeList<TimerEvent> events; // Chỉ chứa timer đến nhịp hoặc hoàn tất trong bước hiện tại.
    private ITimerListener[] listeners = new ITimerListener[128]; // Bên nhận callback ứng với từng ô; chỉ dùng trên main thread.
    private TimerEvent[] dispatchBuffer = new TimerEvent[128]; // Callback có thể tăng native buffer; chụp event trước khi gọi.
    private JobHandle pendingJob; // Phải Complete trước khi thay hoặc giải phóng native buffer.
    private int slotCount; // Số ô từng được mở, gồm cả ô rảnh; không phải số timer đang chạy.
    private bool simulating; // Chặn gọi Simulate lồng nhau từ callback.
    private bool disposed; // Chặn truy cập sau khi dịch vụ đã giải phóng bộ nhớ.
    public float DeltaTime => timeSource.DeltaTime; // Thời gian đầu vào cho các bên sử dụng cùng đồng hồ.

    #endregion

    #region Initialization

    // Cấp buffer ban đầu; tự tăng dung lượng khi cần, không giới hạn ở 128 timer.
    public TimerManager(ITimeSource timeSource)
    {
        this.timeSource = timeSource;
        states = new NativeArray<TimerState>(128, Allocator.Persistent);
        events = new NativeList<TimerEvent>(128, Allocator.Persistent);
    }

    #endregion

    #region Registration & Commands

    // duration: tuổi thọ; interval: nhịp lặp (0 nếu không có); paused: trạng thái dừng ban đầu.
    public TimerHandle Register(float duration, ITimerListener listener, float interval = 0f, bool paused = false)
    {
        if (disposed) throw new ObjectDisposedException(nameof(TimerManager));
        ValidateTimes(duration, interval);
        if (listener == null) throw new ArgumentNullException(nameof(listener));
        int index = freeSlots.Count > 0 ? freeSlots.Pop() : slotCount++;
        EnsureCapacity(slotCount);
        uint version = checked(states[index].Version + 1u);
        states[index] = new TimerState
        {
            Version = version, Revision = 1, Active = 1, Paused = paused ? (byte)1 : (byte)0,
            Remaining = duration, Interval = interval
        };
        listeners[index] = listener;
        return new TimerHandle(this, index, version);
    }

    // Lịch lặp không tự hết hạn; bên sở hữu phải Cancel. firstDelay cho phép chia pha các lượt quét AI.
    public TimerHandle RegisterPeriodic(float interval, ITimerListener listener, float firstDelay)
    {
        if (!(interval > 0f) || float.IsInfinity(interval) || float.IsNaN(firstDelay) || firstDelay < 0f || firstDelay > interval)
            throw new ArgumentOutOfRangeException(nameof(interval));
        TimerHandle handle = Register(float.PositiveInfinity, listener, interval);
        TimerState state = states[handle.Index];
        state.Elapsed = interval - firstDelay;
        states[handle.Index] = state;
        return handle;
    }

    // Kiểm tra cả manager, ô và phiên bản để loại handle cũ.
    public bool IsRegistered(TimerHandle handle)
        => !disposed && ReferenceEquals(handle.Owner, this) && handle.Index >= 0 && handle.Index < slotCount &&
           states[handle.Index].Active != 0 && states[handle.Index].Version == handle.Version;

    // Trả 0 nếu timer đã hủy hoặc handle không còn hợp lệ.
    public float GetRemaining(TimerHandle handle)
        => IsRegistered(handle) ? (float)states[handle.Index].Remaining : 0f;

    // Giữ interval và mặc định giữ nhịp; cooldown một lần không có nhịp để giữ.
    public bool Restart(TimerHandle handle, float duration, bool resetInterval = false)
    {
        if (!IsRegistered(handle)) return false;
        TimerState state = states[handle.Index];
        ValidateTimes(duration, (float)state.Interval);
        state.Remaining = duration;
        state.Completed = 0;
        state.Revision = checked(state.Revision + 1u);
        if (resetInterval) state.Elapsed = 0d;
        states[handle.Index] = state;
        return true;
    }

    // Dừng/tiếp tục riêng timer này; không thay thời gian còn lại hoặc nhịp tích lũy.
    public bool SetPaused(TimerHandle handle, bool paused)
    {
        if (!IsRegistered(handle)) return false;
        TimerState state = states[handle.Index];
        state.Paused = paused ? (byte)1 : (byte)0;
        states[handle.Index] = state;
        return true;
    }

    // Hủy không phát Completed. Timer hoàn tất vẫn giữ slot để bên sở hữu có thể Restart.
    public bool Cancel(TimerHandle handle)
    {
        if (!IsRegistered(handle)) return false;
        TimerState state = states[handle.Index];
        state.Active = 0;
        states[handle.Index] = state;
        listeners[handle.Index] = null;
        freeSlots.Push(handle.Index);
        return true;
    }

    #endregion

    #region Simulation & Event Dispatch

    // Zenject gọi mỗi frame theo nguồn thời gian đã inject.
    public void Tick() => Simulate(timeSource.DeltaTime);

    // Chạy job chung, chụp kết quả rồi mới gọi listener. Không gọi thêm nếu đã dùng Tick của Zenject.
    public void Simulate(float deltaTime)
    {
        if (disposed) return;
        ValidateTimes(deltaTime, 0f);
        if (deltaTime == 0f || slotCount == 0) return;
        if (simulating) throw new InvalidOperationException("Do not tick timers recursively.");
        simulating = true;
        try
        {
            events.Clear();
            pendingJob = new TimerJob { DeltaTime = deltaTime, States = states, Events = events.AsParallelWriter() }
                .Schedule(slotCount, 64);
            pendingJob.Complete();
            int count = events.Length; // Chốt số event trước khi callback có thể thêm/hủy timer.
            for (int index = 0; index < count; index++) dispatchBuffer[index] = events[index];
            for (int index = 0; index < count && !disposed; index++)
            {
                TimerEvent timerEvent = dispatchBuffer[index];
                var handle = new TimerHandle(this, timerEvent.Index, timerEvent.Version);
                if (!IsRegistered(handle) || states[handle.Index].Revision != timerEvent.Revision) continue;
                try { listeners[handle.Index].OnTimer(handle, new TimerResult(timerEvent.TickCount, timerEvent.Completed != 0)); }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    Cancel(handle);
                }
            }
        }
        finally { simulating = false; }
    }

    #endregion

    #region Capacity & Validation

    public void PrewarmCapacity(int count)
    {
        if (disposed) throw new ObjectDisposedException(nameof(TimerManager));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        EnsureCapacity(count);
    }

    // Tăng mọi buffer theo lũy thừa 2 và giữ trạng thái cũ; không thu nhỏ khi số timer giảm.
    private void EnsureCapacity(int count)
    {
        if (states.Length >= count) return;
        pendingJob.Complete();
        int capacity = Mathf.NextPowerOfTwo(count);
        var expanded = new NativeArray<TimerState>(capacity, Allocator.Persistent);
        NativeArray<TimerState>.Copy(states, expanded, states.Length);
        states.Dispose();
        states = expanded;
        events.Capacity = capacity; // Đủ tối đa một event cho mỗi slot, không resize trong job.
        Array.Resize(ref listeners, capacity);
        Array.Resize(ref dispatchBuffer, capacity);
    }

    // Loại số âm/NaN; chỉ timer có interval mới được tồn tại vô hạn.
    private static void ValidateTimes(float duration, float interval)
    {
        if (float.IsNaN(duration) || (float.IsInfinity(duration) && !(duration > 0f && interval > 0f)) || duration < 0f ||
            float.IsNaN(interval) || float.IsInfinity(interval) || interval < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration), "Timer values must be nonnegative; only periodic timers may have infinite duration.");
    }

    #endregion

    #region Cleanup

    // Chờ job xong, giải phóng native memory và bỏ tham chiếu đến các listener.
    public void Dispose()
    {
        if (disposed) return;
        pendingJob.Complete();
        disposed = true;
        states.Dispose();
        events.Dispose();
        Array.Clear(listeners, 0, listeners.Length);
        freeSlots.Clear();
    }

    #endregion
}
