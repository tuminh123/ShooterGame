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

    internal void Return(EffectRuntime runtime)
    {
        runtime.Reset();
        pools[runtime.GetType()].Despawn(runtime);
    }
}
