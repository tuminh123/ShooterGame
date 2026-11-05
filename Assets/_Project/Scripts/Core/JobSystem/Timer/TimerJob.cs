using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct TimerJob : IJobParallelFor
{
    #region Job Data

    public double DeltaTime; // Thời gian gameplay của bước cập nhật, lấy từ main thread.
    public NativeArray<TimerState> States; // Mỗi worker chỉ sửa slot index của mình.
    public NativeList<TimerEvent>.ParallelWriter Events; // Ghi kết quả an toàn từ nhiều worker, không resize trong job.

    #endregion

    #region Time Calculation

    // Trừ thời gian, đếm nhịp và báo hoàn tất cho một slot; không gọi hành vi gameplay.
    public void Execute(int index)
    {
        TimerState state = States[index];
        if (state.Active == 0 || state.Paused != 0 || state.Completed != 0) return;

        double activeTime = math.min(DeltaTime, state.Remaining); // Không tính nhịp cho thời gian vượt quá tuổi thọ.
        state.Remaining = math.max(0d, state.Remaining - activeTime);
        int tickCount = 0; // Gộp các nhịp bị đi qua trong frame dài vào một event.
        if (state.Interval > 0d)
        {
            state.Elapsed += activeTime;
            tickCount = (int)math.min(int.MaxValue, math.floor((state.Elapsed + 0.0000001d) / state.Interval));
            state.Elapsed = math.max(0d, state.Elapsed - tickCount * state.Interval);
        }
        state.Completed = state.Remaining <= 0d ? (byte)1 : (byte)0;
        States[index] = state;

        // Mỗi slot xuất tối đa một event, chứa số nhịp thay vì cấp phát một event cho từng nhịp.
        if (tickCount > 0 || state.Completed != 0)
            Events.AddNoResize(new TimerEvent
            {
                Index = index, Version = state.Version, Revision = state.Revision,
                TickCount = tickCount, Completed = state.Completed
            });
    }

    #endregion
}
