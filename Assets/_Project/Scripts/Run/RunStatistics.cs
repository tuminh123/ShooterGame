using System;

/// <summary>Thống kê của lượt chơi: vàng nhặt được và số quái bị hạ. Chỉ đếm, không cộng ví.</summary>
/// <remarks>Do <see cref="RunManager"/> tạo; manager quyết định khi nào một lần nhặt/hạ quái được tính.</remarks>
public sealed class RunStatistics
{
    #region State

    /// <summary>Vàng nhặt trong trận. Ví đã được cộng lúc nhặt, nên cuối trận không quyết toán lại số này.</summary>
    public int Gold { get; private set; }

    /// <summary>Số quái bị hạ trong trận.</summary>
    public int EnemiesDefeated { get; private set; }

    public event Action<int> GoldChanged;
    public event Action<int> EnemiesDefeatedChanged;

    #endregion

    #region Behaviour

    /// <summary>Cộng vàng vào thống kê; 0 thì bỏ qua để không phát event thừa.</summary>
    public void AddGold(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount == 0) return;
        Gold = checked(Gold + amount);
        GoldChanged?.Invoke(Gold);
    }

    /// <summary>Tính thêm một quái bị hạ.</summary>
    public void AddDefeat()
    {
        EnemiesDefeated++;
        EnemiesDefeatedChanged?.Invoke(EnemiesDefeated);
    }

    /// <summary>Về 0 cho lượt mới và báo HUD.</summary>
    public void Reset()
    {
        Gold = 0;
        EnemiesDefeated = 0;
        EnemiesDefeatedChanged?.Invoke(EnemiesDefeated);
        GoldChanged?.Invoke(Gold);
    }

    /// <summary>Gỡ mọi listener khi scene kết thúc.</summary>
    public void ClearListeners()
    {
        GoldChanged = null;
        EnemiesDefeatedChanged = null;
    }

    #endregion
}
