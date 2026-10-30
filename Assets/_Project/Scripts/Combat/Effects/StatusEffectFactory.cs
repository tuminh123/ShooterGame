using System;
using System.Collections.Generic;
using UnityEngine;

public interface IStatusEffect
{
    void Attach(StatusEffectRuntime runtime, CharacterRuntimeData target);
    void Tick(StatusEffectRuntime runtime, CharacterView target, float deltaTime);
}
public sealed class SlowStatusEffect : IStatusEffect
{
    public void Attach(StatusEffectRuntime runtime, CharacterRuntimeData target)
        => runtime.Modifier = target.AddSpeedMultiplier(1f - ((SlowEffectDataSO)runtime.Data).SlowFraction);
    public void Tick(StatusEffectRuntime runtime, CharacterView target, float deltaTime) { }
}
public sealed class PeriodicDamageStatusEffect : IStatusEffect
{
    public void Attach(StatusEffectRuntime runtime, CharacterRuntimeData target) { }
    public void Tick(StatusEffectRuntime runtime, CharacterView target, float deltaTime)
    {
        var data = (DamageOverTimeEffectDataSO)runtime.Data;
        runtime.TickElapsed += deltaTime;
        while (runtime.TickElapsed + 0.000001f >= data.TickInterval && !runtime.Disposed)
        {
            runtime.TickElapsed = Mathf.Max(0f, runtime.TickElapsed - data.TickInterval);
            target.ReceiveDamage(new DamageInfo(runtime.SourceDamage * data.DamageMultiplierPerTick,
                runtime.SourceTeam));
            if (target.RuntimeData == null || !target.RuntimeData.IsAlive) break;
        }
    }
}
public sealed class StatusEffectFactory
{
    private readonly Dictionary<Type, IStatusEffect> strategies = new Dictionary<Type, IStatusEffect>();
    public StatusEffectFactory()
    {
        Register(typeof(SlowEffectDataSO), new SlowStatusEffect());
        Register(typeof(DamageOverTimeEffectDataSO), new PeriodicDamageStatusEffect());
    }
    public void Register(Type dataType, IStatusEffect strategy) => strategies.Add(dataType, strategy);
    public StatusEffectRuntime Create(StatusEffectDataSO data, CharacterRuntimeData target,
        CharacterTeam sourceTeam, float sourceDamage)
    {
        data.Validate();
        if (!strategies.TryGetValue(data.GetType(), out IStatusEffect strategy))
            throw new InvalidOperationException($"Unregistered status effect: {data.GetType()}.");
        var runtime = new StatusEffectRuntime(data, strategy, sourceTeam, sourceDamage);
        try { strategy.Attach(runtime, target); return runtime; }
        catch { runtime.Dispose(); throw; }
    }
}
