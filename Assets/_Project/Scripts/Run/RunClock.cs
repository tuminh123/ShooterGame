using System;

/// <summary>Đồng hồ gameplay của lượt chơi: đếm giây khi Player còn sống, chỉ báo khi số giây nguyên đổi.</summary>
/// <remarks>Do <see cref="RunManager"/> tạo và gọi <see cref="Tick"/>; không bind vào Zenject.</remarks>
public sealed class RunClock
{
    #region State

    // Giữ phần giây lẻ để không trôi giờ; HUD chỉ cần số giây nguyên nên chỉ phát khi giá trị này đổi.
    private double elapsed;

    /// <summary>Số giây gameplay đã trôi qua trong lượt.</summary>
    public int ElapsedSeconds { get; private set; }

    /// <summary>Số giây nguyên mới.</summary>
    public event Action<int> TimeChanged;

    #endregion

    #region Behaviour

    /// <summary>Cộng thời gian frame. Bên gọi tự quyết định khi nào đồng hồ được chạy (Player sống, không pause).</summary>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        elapsed += deltaTime;
        // Cộng epsilon để 2.9999999 (sai số float cộng dồn) vẫn được tính là giây thứ 3.
        int seconds = checked((int)Math.Floor(elapsed + 0.0000001d));
        if (seconds == ElapsedSeconds) return;
        ElapsedSeconds = seconds;
        TimeChanged?.Invoke(ElapsedSeconds);
    }

    /// <summary>Về 0 cho lượt mới và báo HUD.</summary>
    public void Reset()
    {
        elapsed = 0d;
        ElapsedSeconds = 0;
        TimeChanged?.Invoke(ElapsedSeconds);
    }

    /// <summary>Gỡ mọi listener khi scene kết thúc.</summary>
    public void ClearListeners() => TimeChanged = null;

    #endregion
}
