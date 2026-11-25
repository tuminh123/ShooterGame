using System;
using System.Collections.Generic;

/// <summary>Tiến độ của một mission trong kỳ hiện tại (RAM). Ra ngoài chỉ đọc; chỉ <see cref="MissionBoard"/> và MissionManager đổi.</summary>
public sealed class MissionRuntime
{
    #region State

    private readonly int[] progress; // Cùng thứ tự với Data.Tasks.
    private bool claimed;

    /// <summary>Cấu hình của mission.</summary>
    public MissionDataSO Data { get; }

    /// <summary>Trạng thái suy ra từ tiến độ và cờ đã nhận.</summary>
    public MissionState State
    {
        get
        {
            if (claimed) return MissionState.Claimed;
            for (int i = 0; i < progress.Length; i++)
                if (progress[i] < Data.Tasks[i].Target) return MissionState.InProgress;
            return MissionState.Claimable;
        }
    }

    /// <summary>Tiến độ của mission con thứ <paramref name="taskIndex"/> (đã chặn ở mục tiêu).</summary>
    public int GetProgress(int taskIndex) => progress[taskIndex];

    #endregion

    #region Change (miền Mission)

    internal MissionRuntime(MissionDataSO data)
    {
        Data = data != null ? data : throw new ArgumentNullException(nameof(data));
        progress = new int[data.Tasks.Count];
    }

    // Cộng vào mọi mission con nhận tín hiệu này; trả về có đổi hay không.
    internal bool Add(MissionSignal signal)
    {
        if (claimed) return false;
        bool changed = false;
        for (int i = 0; i < progress.Length; i++)
        {
            MissionTask task = Data.Tasks[i];
            if (progress[i] >= task.Target || !task.Matches(signal)) continue;
            progress[i] = (int)Math.Min(task.Target, (long)progress[i] + signal.Amount);
            changed = true;
        }
        return changed;
    }

    // Đặt tiến độ đúng bằng amount, không cộng dồn: gọi lại nhiều lần cho cùng kết quả, save cũ lệch thì được sửa.
    internal bool SetProgress(MissionObjectiveType objective, int amount)
    {
        if (claimed) return false;
        bool changed = false;
        for (int i = 0; i < progress.Length; i++)
        {
            MissionTask task = Data.Tasks[i];
            int value = Math.Min(task.Target, amount);
            if (task.Objective != objective || value == progress[i]) continue;
            progress[i] = value;
            changed = true;
        }
        return changed;
    }

    internal void MarkClaimed() => claimed = true;

    internal void Reset()
    {
        claimed = false;
        Array.Clear(progress, 0, progress.Length);
    }

    #endregion

    #region Save

    // Khớp theo taskId nên đổi thứ tự trong asset không làm lệch tiến độ; target giảm thì tiến độ bị chặn lại.
    internal void Restore(MissionsSaveData.Mission saved)
    {
        claimed = saved.claimed;
        for (int i = 0; i < progress.Length; i++)
            foreach (MissionsSaveData.Task task in saved.tasks)
                if (string.Equals(task.taskId, Data.Tasks[i].Id, StringComparison.Ordinal))
                    progress[i] = Math.Min(task.progress, Data.Tasks[i].Target);
    }

    internal MissionsSaveData.Mission ToSaveData()
    {
        var saved = new MissionsSaveData.Mission { missionId = Data.Id, claimed = claimed };
        for (int i = 0; i < progress.Length; i++)
            saved.tasks.Add(new MissionsSaveData.Task { taskId = Data.Tasks[i].Id, progress = progress[i] });
        return saved;
    }

    #endregion
}

/// <summary>Một nhóm mission đang chạy: database, kỳ hiện tại và tiến độ từng mission.</summary>
public sealed class MissionBoard
{
    #region State

    private readonly List<MissionRuntime> missions = new List<MissionRuntime>();
    private long cycle;

    /// <summary>Cấu hình của nhóm.</summary>
    public MissionDatabaseSO Database { get; }

    /// <summary>Đang mở (event ngoài thời gian thì đóng).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Lúc kỳ hiện tại kết thúc (UTC); chỉ có nghĩa khi <see cref="IsOpen"/>.</summary>
    public DateTime EndsAt { get; private set; }

    /// <summary>Các mission theo thứ tự trong database; có <see cref="FinalMission"/> thì nó nằm cuối, nên lưu/nhận/reset như mission thường.</summary>
    public IReadOnlyList<MissionRuntime> Missions => missions;

    /// <summary>Mission tổng kết; null nếu nhóm không có.</summary>
    public MissionRuntime FinalMission { get; }

    /// <summary>Số mission thường của nhóm.</summary>
    public int NormalCount => Database.Missions.Count;

    /// <summary>Số mission thường đã nhận thưởng.</summary>
    public int ClaimedCount
    {
        get
        {
            int count = 0;
            foreach (MissionRuntime mission in missions)
                if (mission != FinalMission && mission.State == MissionState.Claimed) count++;
            return count;
        }
    }

    #endregion

    #region Change (miền Mission)

    internal MissionBoard(MissionDatabaseSO database)
    {
        Database = database != null ? database : throw new ArgumentNullException(nameof(database));
        foreach (MissionDataSO mission in database.Missions)
            missions.Add(new MissionRuntime(mission));
        if (database.FinalMission == null) return;
        FinalMission = new MissionRuntime(database.FinalMission);
        missions.Add(FinalMission);
    }

    /// <summary>Cập nhật kỳ theo giờ; sang kỳ khác thì xóa tiến độ và cờ đã nhận. Trả về có đổi (mở/đóng/reset) hay không.</summary>
    internal bool Refresh(DateTime utcNow)
    {
        bool open = Database.TryGetCycle(utcNow, out long current, out DateTime endsAt);
        bool changed = open != IsOpen;
        IsOpen = open;
        EndsAt = endsAt;
        if (!open || current == cycle) return changed; // Đóng thì giữ nguyên, không reset event đang chờ mở.
        cycle = current;
        foreach (MissionRuntime mission in missions) mission.Reset();
        return true;
    }

    internal bool Add(MissionSignal signal)
    {
        if (!IsOpen) return false;
        bool changed = false;
        foreach (MissionRuntime mission in missions) changed |= mission.Add(signal);
        return changed;
    }

    /// <summary>Đánh dấu đã nhận; mission thường thì Final Mission tăng theo.</summary>
    internal void MarkClaimed(MissionRuntime mission)
    {
        mission.MarkClaimed();
        SyncFinalMission();
    }

    // Suy từ số mission đã nhận thay vì cộng từng lần: save cũ chưa có Final Mission vẫn đúng.
    private void SyncFinalMission()
        => FinalMission?.SetProgress(MissionObjectiveType.CompleteMission, ClaimedCount);

    #endregion

    #region Save

    internal void Restore(MissionsSaveData.Board saved)
    {
        cycle = saved.cycle;
        foreach (MissionsSaveData.Mission entry in saved.missions)
            foreach (MissionRuntime mission in missions)
                if (string.Equals(entry.missionId, mission.Data.Id, StringComparison.Ordinal)) mission.Restore(entry);
        SyncFinalMission();
    }

    internal MissionsSaveData.Board ToSaveData()
    {
        var saved = new MissionsSaveData.Board { databaseId = Database.Id, cycle = cycle };
        foreach (MissionRuntime mission in missions) saved.missions.Add(mission.ToSaveData());
        return saved;
    }

    #endregion
}
