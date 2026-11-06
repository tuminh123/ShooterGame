using System;
using Zenject;

// Trạng thái của lượt chơi: giữ vàng và đồng hồ; không biết UI hay cách tạo/nhặt vật phẩm.
public sealed class RunSessionManager : IInitializable, ITimerListener, IDisposable
{
    #region Dependencies & State

    private readonly PlayerManager players; // Chỉ cộng thời gian khi Player đang sống và hoạt động.
    private readonly LevelManager levels; // Nhận thông báo reset lượt chơi từ đầu mối hiện có.
    private readonly ITimerManager timers; // Phép tính thời gian vẫn chạy trong TimerJob chung.
    private readonly CharacterManager characters;
    private TimerHandle clock; // Nhịp một giây của lượt hiện tại; hủy khi reset/dispose.
    private bool disposed;

    public int Gold { get; private set; } // Vàng trong trận, chưa ghi vào save vĩnh viễn.
    public int ElapsedSeconds { get; private set; } // Số giây gameplay, chịu ảnh hưởng timeScale.
    public int EnemiesDefeated { get; private set; }
    public event Action<int> GoldChanged;
    public event Action<int> TimeChanged;
    public event Action<int> EnemiesDefeatedChanged;

    public RunSessionManager(PlayerManager players, LevelManager levels, ITimerManager timers,
        CharacterManager characters)
    { this.players = players; this.levels = levels; this.timers = timers; this.characters = characters; }

    #endregion

    #region Rewards & Clock

    public void Initialize()
    {
        levels.RunReset += ResetRun;
        characters.CharacterDied += OnCharacterDied;
        clock = timers.RegisterPeriodic(1f, this, 1f);
    }

    // Loot hoặc nguồn thưởng khác đều dùng cùng API; không phát event khi lượng nhận bằng 0.
    public void AddGold(int amount)
    {
        if (disposed) throw new ObjectDisposedException(nameof(RunSessionManager));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount == 0) return;
        Gold = checked(Gold + amount);
        GoldChanged?.Invoke(Gold);
    }

    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        CharacterView player = players.CurrentPlayer;
        if (disposed || !clock.Equals(handle) || player == null || !player.isActiveAndEnabled ||
            player.RuntimeData == null || !player.RuntimeData.IsAlive) return;
        ElapsedSeconds = checked(ElapsedSeconds + result.DueTickCount);
        TimeChanged?.Invoke(ElapsedSeconds);
    }

    #endregion

    #region Reset & Cleanup

    private void OnCharacterDied(CharacterView character)
    {
        if (disposed || character.RuntimeData == null || character.RuntimeData.Team != CharacterTeam.Enemy) return;
        EnemiesDefeated++;
        EnemiesDefeatedChanged?.Invoke(EnemiesDefeated);
    }

    private void ResetRun()
    {
        Gold = 0;
        ElapsedSeconds = 0;
        EnemiesDefeated = 0;
        EnemiesDefeatedChanged?.Invoke(EnemiesDefeated);
        timers.Cancel(clock);
        clock = timers.RegisterPeriodic(1f, this, 1f);
        GoldChanged?.Invoke(Gold);
        TimeChanged?.Invoke(ElapsedSeconds);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        levels.RunReset -= ResetRun;
        characters.CharacterDied -= OnCharacterDied;
        timers.Cancel(clock);
        GoldChanged = null;
        TimeChanged = null;
        EnemiesDefeatedChanged = null;
    }

    #endregion
}
