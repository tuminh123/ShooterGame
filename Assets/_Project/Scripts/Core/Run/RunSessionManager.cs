using System;
using Zenject;
using UnityEngine;

// Trạng thái của lượt chơi: giữ vàng và đồng hồ; không biết UI hay cách tạo/nhặt vật phẩm.
public sealed class RunSessionManager : IInitializable, ITickable, IDisposable
{
    #region Dependencies & State

    private readonly PlayerManager players; // Chỉ cộng thời gian khi Player đang sống và hoạt động.
    private readonly LevelManager levels; // Nhận thông báo reset lượt chơi từ đầu mối hiện có.
    private readonly CharacterManager characters;
    private double elapsed; // Giữ phần giây lẻ, chỉ phát TimeChanged khi số giây nguyên đổi.
    private bool disposed;

    public int Gold { get; private set; } // Thống kê vàng trận; LootManager đã cộng vào ví, không cộng lại khi kết thúc.
    public int ElapsedSeconds { get; private set; } // Số giây gameplay, chịu ảnh hưởng timeScale.
    public int EnemiesDefeated { get; private set; }
    public event Action<int> GoldChanged;
    public event Action<int> TimeChanged;
    public event Action<int> EnemiesDefeatedChanged;

    public RunSessionManager(PlayerManager players, LevelManager levels,
        CharacterManager characters)
    { this.players = players; this.levels = levels; this.characters = characters; }

    #endregion

    #region Rewards & Clock

    public void Initialize()
    {
        levels.RunReset += ResetRun;
        characters.CharacterDied += OnCharacterDied;
    }

    // Chỉ cập nhật thống kê; nguồn thưởng phải cộng ví qua CurrencyService riêng.
    public void AddGold(int amount)
    {
        if (disposed) throw new ObjectDisposedException(nameof(RunSessionManager));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount == 0) return;
        Gold = checked(Gold + amount);
        GoldChanged?.Invoke(Gold);
    }

    public void Tick()
    {
        CharacterView player = players.CurrentPlayer;
        if (disposed || Time.deltaTime <= 0f || player == null || !player.isActiveAndEnabled ||
            player.RuntimeData == null || !player.RuntimeData.IsAlive) return;
        elapsed += Time.deltaTime;
        int seconds = checked((int)Math.Floor(elapsed + 0.0000001d));
        if (seconds == ElapsedSeconds) return;
        ElapsedSeconds = seconds;
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
        elapsed = 0d;
        EnemiesDefeated = 0;
        EnemiesDefeatedChanged?.Invoke(EnemiesDefeated);
        GoldChanged?.Invoke(Gold);
        TimeChanged?.Invoke(ElapsedSeconds);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        levels.RunReset -= ResetRun;
        characters.CharacterDied -= OnCharacterDied;
        GoldChanged = null;
        TimeChanged = null;
        EnemiesDefeatedChanged = null;
    }

    #endregion
}
