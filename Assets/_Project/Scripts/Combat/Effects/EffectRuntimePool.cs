using System;
using System.Collections.Generic;

/// <summary>
/// Pool runtime của effect gameplay: mỗi loại runtime một Stack, thuê bằng hàm có kiểu rõ ràng.
/// </summary>
/// <remarks>
/// Do <see cref="CombatManager"/> tạo bằng <c>new</c>; không bind vào Zenject.
/// Thêm loại runtime mới = thêm một Stack, một hàm Rent và một nhánh trong <see cref="Return"/>.
/// Burn và Poison dùng chung <see cref="DamageOverTimeEffectRuntime"/> vì cùng kiểu hành vi.
/// </remarks>
public sealed class EffectRuntimePool
{
    #region Pools

    private readonly Stack<SlowEffectRuntime> slowPool = new Stack<SlowEffectRuntime>();
    private readonly Stack<DamageOverTimeEffectRuntime> damageOverTimePool = new Stack<DamageOverTimeEffectRuntime>();

    #endregion

    #region Rent

    /// <summary>Thuê runtime làm chậm; cấu hình gán thông số, EffectSystem gán context và kích hoạt sau đó.</summary>
    public SlowEffectRuntime RentSlow() => slowPool.Count > 0 ? slowPool.Pop() : new SlowEffectRuntime();

    /// <summary>Thuê runtime sát thương theo thời gian (burn, poison).</summary>
    public DamageOverTimeEffectRuntime RentDamageOverTime()
        => damageOverTimePool.Count > 0 ? damageOverTimePool.Pop() : new DamageOverTimeEffectRuntime();

    #endregion

    #region Prewarm

    /// <summary>Bảo đảm có sẵn ít nhất số runtime rảnh cho mỗi loại; chỉ tạo thêm phần còn thiếu.</summary>
    public void Prewarm(int slowCount, int damageOverTimeCount)
    {
        Fill(slowPool, slowCount);
        Fill(damageOverTimePool, damageOverTimeCount);
    }

    private static void Fill<T>(Stack<T> pool, int count) where T : new()
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        while (pool.Count < count) pool.Push(new T());
    }

    #endregion

    #region Return

    /// <summary>
    /// Xóa state rồi trả runtime về đúng Stack theo kiểu thực tế. EffectSystem phải gọi Remove trước.
    /// Trả một runtime hai lần là lỗi logic (hai effect sẽ dùng chung một object), nên báo ngay.
    /// </summary>
    public void Return(EffectRuntime runtime)
    {
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));
        runtime.Reset();
        switch (runtime)
        {
            case SlowEffectRuntime slow: Push(slowPool, slow); break;
            case DamageOverTimeEffectRuntime damageOverTime: Push(damageOverTimePool, damageOverTime); break;
            default: throw new InvalidOperationException($"No runtime pool for {runtime.GetType().Name}.");
        }
    }

    private static void Push<T>(Stack<T> pool, T runtime)
    {
        if (pool.Contains(runtime)) throw new InvalidOperationException("Effect runtime was returned twice.");
        pool.Push(runtime);
    }

    #endregion
}
