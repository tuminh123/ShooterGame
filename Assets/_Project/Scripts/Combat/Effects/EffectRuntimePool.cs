using System;

/// <summary>
/// Pool runtime của effect gameplay: mỗi loại runtime một <see cref="Pool{T}"/>, thuê bằng hàm có kiểu rõ ràng.
/// </summary>
/// <remarks>
/// Do <see cref="CombatManager"/> tạo bằng <c>new</c>; không bind vào Zenject.
/// Thêm loại runtime mới = thêm một Pool, một hàm Rent và một nhánh trong <see cref="Return"/>.
/// Burn và Poison dùng chung <see cref="DamageOverTimeEffectRuntime"/> vì cùng kiểu hành vi.
/// </remarks>
public sealed class EffectRuntimePool
{
    #region Pools

    private readonly Pool<SlowEffectRuntime> slowPool = new Pool<SlowEffectRuntime>(() => new SlowEffectRuntime());
    private readonly Pool<DamageOverTimeEffectRuntime> damageOverTimePool =
        new Pool<DamageOverTimeEffectRuntime>(() => new DamageOverTimeEffectRuntime());

    /// <summary>Số runtime làm chậm đang rảnh (kiểm tra và prewarm).</summary>
    public int AvailableSlow => slowPool.AvailableCount;

    /// <summary>Số runtime sát thương theo thời gian đang rảnh.</summary>
    public int AvailableDamageOverTime => damageOverTimePool.AvailableCount;

    #endregion

    #region Rent

    /// <summary>Thuê runtime làm chậm; cấu hình gán thông số, EffectSystem gán context và kích hoạt sau đó.</summary>
    public SlowEffectRuntime RentSlow() => slowPool.Rent();

    /// <summary>Thuê runtime sát thương theo thời gian (burn, poison).</summary>
    public DamageOverTimeEffectRuntime RentDamageOverTime() => damageOverTimePool.Rent();

    #endregion

    #region Prewarm

    /// <summary>Bảo đảm có sẵn ít nhất số runtime rảnh cho mỗi loại; chỉ tạo thêm phần còn thiếu.</summary>
    public void Prewarm(int slowCount, int damageOverTimeCount)
    {
        slowPool.Prewarm(slowCount);
        damageOverTimePool.Prewarm(damageOverTimeCount);
    }

    #endregion

    #region Return

    /// <summary>
    /// Xóa state rồi trả runtime về đúng pool theo kiểu thực tế. EffectSystem phải gọi Remove trước.
    /// Trả một runtime hai lần (hoặc runtime không thuê từ pool này) là lỗi logic — hai effect sẽ dùng chung một object — nên báo ngay.
    /// </summary>
    public void Return(EffectRuntime runtime)
    {
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));
        bool returned;
        switch (runtime)
        {
            case SlowEffectRuntime slow: returned = slowPool.Return(slow); break;
            case DamageOverTimeEffectRuntime damageOverTime: returned = damageOverTimePool.Return(damageOverTime); break;
            default: throw new InvalidOperationException($"No runtime pool for {runtime.GetType().Name}.");
        }
        if (!returned) throw new InvalidOperationException("Effect runtime was returned twice or was not rented from this pool.");
    }

    #endregion
}
