using System;

/// <summary>
/// Level/EXP của Player trong một lượt chơi và nguồn bonus chỉ số tương ứng trên Player.
/// Không ghi gì vào save: hết lượt là mất.
/// </summary>
/// <remarks>
/// Do <see cref="RunManager"/> tạo; thay cho RunLevelService + phần "run" của LevelManager cũ.
/// Bonus level trận là <b>một nguồn riêng</b> (lease) trong <see cref="StatSheet"/> của Player.
/// Run nằm trên Character nên tự thêm nguồn này khi Player xuất hiện, thay vì để PlayerSystem nghe ngược lên Run.
/// </remarks>
public sealed class RunLevel : IDisposable
{
    #region Fields

    private readonly RunLevelConfigSO config; // Chỉ đọc công thức EXP và bonus mỗi cấp; không ghi vào asset.
    private readonly RunLevelProgress progress; // Trạng thái level/EXP của lượt hiện tại.
    private readonly IPlayerReader players;
    // Nguồn bonus level trận trên Player hiện tại; null khi chưa có Player.
    // Không Dispose khi Player bị thu hồi: runtime về pool đã xóa mọi nguồn và vô hiệu hóa lease (giống PlayerSystem).
    private StatModifierLease playerBonus;
    // Chặn thay đổi lồng nhau từ callback (ví dụ listener LevelUp lại cộng EXP), để thứ tự thông báo luôn nhất quán.
    private bool changing;
    private bool disposed;

    #endregion

    #region Properties / Events

    /// <summary>Level hiện tại trong trận.</summary>
    public int Level => progress.Level;

    /// <summary>EXP dư ở cấp hiện tại.</summary>
    public int Experience => progress.Experience;

    /// <summary>EXP cần cho lần lên cấp hiện tại; 0 khi đạt trần.</summary>
    public int ExperienceToNextLevel
    {
        get
        {
            config.TryGetNextLevel(progress.Level, out int required, out _);
            return required;
        }
    }

    /// <summary>(level cũ, level mới); một lần nhận EXP tăng nhiều cấp chỉ phát một lần.</summary>
    public event Action<int, int> LevelUp;

    /// <summary>(EXP dư, EXP cần cho cấp tiếp).</summary>
    public event Action<int, int> ExperienceChanged;

    #endregion

    #region Constructor / Lifecycle

    /// <summary>Chuẩn bị tiến trình level 1 và nghe Player để gắn nguồn bonus.</summary>
    public RunLevel(RunLevelConfigSO config, IPlayerReader players)
    {
        this.config = config != null ? config : throw new ArgumentException("Gán Run Level Config trong LevelDatabaseSO.", nameof(config));
        this.players = players ?? throw new ArgumentNullException(nameof(players));
        progress = new RunLevelProgress(config);
        players.PlayerChanged += OnPlayerChanged;
    }

    /// <summary>Gỡ listener Player và mọi listener của level; không save tiến trình trận.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        players.PlayerChanged -= OnPlayerChanged;
        playerBonus = null;
        LevelUp = null;
        ExperienceChanged = null;
    }

    #endregion

    #region Experience

    /// <summary>
    /// Nhận EXP, cập nhật trạng thái và bonus trước rồi mới phát event.
    /// Trả số cấp đã tăng.
    /// </summary>
    public int AddExperience(int amount)
    {
        BeginChange();
        try
        {
            int previousLevel = progress.Level;
            int previousExperience = progress.Experience;
            int gainedLevels = progress.AddExperience(amount);
            if (gainedLevels > 0)
            {
                // Bonus phải có trước khi phát LevelUp, để bảng chọn skill/UI đọc được chỉ số mới.
                RefreshPlayerBonus();
                LevelUp?.Invoke(previousLevel, progress.Level);
            }
            if (gainedLevels > 0 || previousExperience != progress.Experience)
                ExperienceChanged?.Invoke(progress.Experience, ExperienceToNextLevel);
            return gainedLevels;
        }
        finally { changing = false; }
    }

    /// <summary>Về level 1, EXP 0 cho lượt mới; cập nhật bonus của Player (nếu còn) và báo HUD.</summary>
    public void Reset()
    {
        BeginChange();
        try
        {
            progress.Reset();
            RefreshPlayerBonus();
            ExperienceChanged?.Invoke(progress.Experience, ExperienceToNextLevel);
        }
        finally { changing = false; }
    }

    #endregion

    #region Player Bonus

    // Player mới: thêm một nguồn bonus theo level hiện tại. Player cũ (nếu có) đã bị thu hồi cùng các nguồn của nó.
    // Chạy ngay trong PlayerChanged của lần spawn, khi Player chưa nhận sát thương nên HP vẫn đầy theo max mới.
    private void OnPlayerChanged(CharacterView player)
    {
        playerBonus = null;
        if (disposed || player == null || player.RuntimeData == null) return;
        playerBonus = player.RuntimeData.Stats.AddSource(config.GetTotalBonus(progress.Level).ToModifiers());
    }

    // Cập nhật nguồn bonus tại chỗ (không cộng thêm nguồn mới).
    private void RefreshPlayerBonus() => playerBonus?.Update(config.GetTotalBonus(progress.Level).ToModifiers());

    #endregion

    #region Private Helpers

    private void BeginChange()
    {
        if (disposed) throw new ObjectDisposedException(nameof(RunLevel));
        if (changing) throw new InvalidOperationException("Không được thay đổi level trận từ trong callback của level trận.");
        changing = true;
    }

    #endregion
}
