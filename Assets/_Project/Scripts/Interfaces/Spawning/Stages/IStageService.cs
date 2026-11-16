using System;

#region StageSelection

/// <summary>Đọc danh mục màn và chọn Stage trước khi bắt đầu lượt.</summary>
public interface IStageSelection
{
    StageDataSO CurrentStage { get; }
    StageDatabaseSO Database { get; }
    void SelectStage(string stageId);
}

#endregion

#region StageRunLifecycle

/// <summary>Kiểm tra cấu hình, bắt đầu hoặc dừng các hệ spawn của Stage.</summary>
public interface IStageRunLifecycle
{
    void Validate();
    void BeginRun();
    void StopRun();
}

#endregion

#region StageResults

/// <summary>Nhận kết quả màn và cấp/đọc thưởng kết quả đúng một lần.</summary>
public interface IStageResults
{
    bool ResultGranted { get; }
    event Action Cleared;
    event Action<Exception> Failed;
    void FinishRun(bool won);
    long GetResultReward(CurrencyType type);
}

#endregion

#region StageSchedule

/// <summary>Đọc đồng hồ Stage và lịch cảnh báo spawn cho UI.</summary>
public interface IStageSchedule
{
    float StageTime { get; }
    event Action<StageSpawnWarning, float> SpawnScheduled;
    event Action WarningsCleared;
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của StageManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IStageService
{
    IStageSelection GetStageSelection();
    IStageRunLifecycle GetStageRunLifecycle();
    IStageResults GetStageResults();
    IStageSchedule GetStageSchedule();
}

#endregion
