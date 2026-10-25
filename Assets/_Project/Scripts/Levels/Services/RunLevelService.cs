using System;

// * Xử lý level/EXP của người chơi trong trận, do LevelManager điều phối; không ghi tiến trình này vào save lâu dài.
public sealed class RunLevelService : IDisposable
{
    #region Shared Configuration & Run State

    private readonly RunLevelConfigSO config;  // * Một config chung lấy từ LevelDatabaseSO.
    private readonly RunLevelProgress progress;  // * Trạng thái riêng của lượt chơi hiện tại, không đặt trên asset.
    private bool changingProgress;               // * Chặn thay đổi lồng nhau trong callback, giữ thứ tự thông báo nhất quán.
    private bool disposed;                       // * Vòng đời service do Zenject quản lý.

    public int Level { get { EnsureAvailable(); return progress.Level; } }                    // * Level hiện tại trong trận.
    public int Experience { get { EnsureAvailable(); return progress.Experience; } }          // * EXP dư ở cấp hiện tại.
    public bool IsMaxLevel { get { EnsureAvailable(); return progress.IsMaxLevel; } }         // * Đã đạt trần config hay chưa.
    public StatBonus TotalBonus { get { EnsureAvailable(); return progress.TotalBonus; } }   // * Tổng bonus cố định đã nhận trong trận.

    // * EXP cần cho lần lên cấp hiện tại; trả 0 khi đạt trần.
    public int ExperienceToNextLevel
    {
        get
        {
            EnsureAvailable();
            config.TryGetNextLevel(progress.Level, out int required, out _);
            return required;
        }
    }

    #endregion

    #region Run Notifications

    public event Action<int, int> LevelChanged;       // * Level cũ, level mới; một lần nhận EXP tăng nhiều cấp phát một thông báo chứa cả khoảng cấp.
    public event Action<int, int> ExperienceChanged;  // * EXP dư, EXP cần lên cấp tiếp; yêu cầu bằng 0 khi đạt trần.
    public event Action RunReset;                    // * Bắt đầu lượt mới, level 1 và EXP 0; không phải sự kiện tăng cấp.

    #endregion

    #region Service Initialization

    // * Chuẩn bị tiến trình level 1; không phát event trong constructor trước khi bên nghe đăng ký.
    public RunLevelService(LevelDatabaseSO database)
    {
        if (database == null || database.RunLevelConfig == null)
            throw new ArgumentException("Assign the shared run level config in LevelDatabaseSO.", nameof(database));
        config = database.RunLevelConfig;
        progress = new RunLevelProgress(config);
    }

    #endregion

    #region Experience & New Run

    // * Nhận EXP, cập nhật trước rồi phát event; trả số cấp tăng để UI mở đủ lượt chọn nâng cấp.
    public int AddExperience(int amount)
    {
        BeginChange();
        try
        {
            int previousLevel = progress.Level;
            int previousExperience = progress.Experience;
            int gainedLevels = progress.AddExperience(amount);
            if (gainedLevels > 0)
                LevelChanged?.Invoke(previousLevel, progress.Level);
            if (gainedLevels > 0 || previousExperience != progress.Experience)
                ExperienceChanged?.Invoke(progress.Experience, ExperienceToNextLevel);
            return gainedLevels;
        }
        finally
        {
            changingProgress = false;
        }
    }

    // * Bắt đầu lượt mới: reset level/EXP và phát thông báo để UI hoặc hệ thống chỉ số tính lại.
    public void StartNewRun()
    {
        BeginChange();
        try
        {
            progress.Reset();
            RunReset?.Invoke();
            ExperienceChanged?.Invoke(progress.Experience, ExperienceToNextLevel);
        }
        finally
        {
            changingProgress = false;
        }
    }

    #endregion

    #region Service Lifecycle Guards

    // * Hủy callback khi kết thúc scope; không save level hoặc EXP của trận.
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        LevelChanged = null;
        ExperienceChanged = null;
        RunReset = null;
    }

    // * Không cho callback vừa nhận thông báo tự thay đổi tiếp tiến trình trong cùng chuỗi xử lý.
    private void BeginChange()
    {
        EnsureAvailable();
        if (changingProgress)
            throw new InvalidOperationException("Cannot change run progress from a run notification callback.");
        changingProgress = true;
    }

    // * Bảo vệ truy cập sau khi service bị dispose.
    private void EnsureAvailable()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(RunLevelService));
    }

    #endregion
}
