using System;
using System.Collections.Generic;
using Zenject;

// Tra pool theo loại runtime; cấu hình effect tự chọn runtime, manager không switch theo loại effect.
public sealed class EffectRuntimePool
{
    private readonly Dictionary<Type, IMemoryPool> pools;

    public EffectRuntimePool(ClassPool<SlowEffectRuntime> slowPool,
        ClassPool<DamageOverTimeEffectRuntime> periodicDamagePool)
    {
        pools = new Dictionary<Type, IMemoryPool>
        {
            { typeof(SlowEffectRuntime), slowPool },
            { typeof(DamageOverTimeEffectRuntime), periodicDamagePool }
        };
    }

    public T Rent<T>() where T : EffectRuntime
    {
        if (!pools.TryGetValue(typeof(T), out IMemoryPool pool))
            throw new InvalidOperationException($"Register a runtime pool for {typeof(T).Name}.");
        return ((IMemoryPool<T>)pool).Spawn();
    }

    public void Prewarm<T>(int count) where T : EffectRuntime
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!pools.TryGetValue(typeof(T), out IMemoryPool pool))
            throw new InvalidOperationException($"Register a runtime pool for {typeof(T).Name}.");
        int additional = count - pool.NumInactive;
        if (additional > 0) pool.ExpandBy(additional);
    }

    internal void Return(EffectRuntime runtime)
    {
        runtime.Reset();
        pools[runtime.GetType()].Despawn(runtime);
    }
}
