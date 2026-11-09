using System;
using System.Collections.Generic;
using Zenject;

// Tra pool theo loại runtime; cấu hình effect tự chọn runtime, manager không switch theo loại effect.
public sealed class EffectRuntimePool
{
    #region Pool Registry

    private readonly Dictionary<Type, IMemoryPool> pools; // Mỗi loại class runtime có một pool đã bind trong installer.

    #endregion

    #region Initialization

    // Burn/Poison dùng chung pool DamageOverTimeEffectRuntime vì cùng kiểu hành vi runtime.
    public EffectRuntimePool(ClassPool<SlowEffectRuntime> slowPool,
        ClassPool<DamageOverTimeEffectRuntime> periodicDamagePool)
    {
        pools = new Dictionary<Type, IMemoryPool>
        {
            { typeof(SlowEffectRuntime), slowPool },
            { typeof(DamageOverTimeEffectRuntime), periodicDamagePool }
        };
    }

    #endregion

    #region Rent And Prewarm

    // Chỉ thuê object; cấu hình gán thông số riêng, manager gán context và kích hoạt sau đó.
    public T Rent<T>() where T : EffectRuntime
    {
        if (!pools.TryGetValue(typeof(T), out IMemoryPool pool))
            throw new InvalidOperationException($"Register a runtime pool for {typeof(T).Name}.");
        return ((IMemoryPool<T>)pool).Spawn();
    }

    // Bảo đảm có ít nhất count runtime đang rảnh; chỉ mở rộng phần thiếu, không thu hồi runtime đang chạy.
    public void Prewarm<T>(int count) where T : EffectRuntime
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!pools.TryGetValue(typeof(T), out IMemoryPool pool))
            throw new InvalidOperationException($"Register a runtime pool for {typeof(T).Name}.");
        int additional = count - pool.NumInactive;
        if (additional > 0) pool.ExpandBy(additional);
    }

    #endregion

    #region Return And Reset

    // Manager phải gọi Remove trước. Reset xóa context rồi trả đúng pool theo kiểu runtime thực tế.
    internal void Return(EffectRuntime runtime)
    {
        runtime.Reset();
        pools[runtime.GetType()].Despawn(runtime);
    }

    #endregion
}
