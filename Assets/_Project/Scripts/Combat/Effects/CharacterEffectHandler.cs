using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterEffectHandler : IDisposable
{
    private readonly CharacterView owner;
    private readonly CharacterRuntimeData identity;
    private readonly StatusEffectFactory factory;
    private readonly List<StatusEffectRuntime> effects = new List<StatusEffectRuntime>();
    private bool disposed;
    public int Count => effects.Count;

    public CharacterEffectHandler(CharacterView owner, StatusEffectFactory factory)
    { this.owner = owner; identity = owner.RuntimeData; this.factory = factory; }

    public static float ApplicationChance(float baseChance, float accuracy, float dodge)
    {
        double attack = 100d + Mathf.Max(0f, accuracy);
        return Mathf.Clamp01((float)(baseChance * attack / (attack + Mathf.Max(0f, dodge))));
    }

    public bool TryApply(StatusEffectDataSO data, CharacterTeam sourceTeam, float sourceDamage, float accuracy)
    {
        if (disposed || owner.RuntimeData != identity || !identity.IsAlive || sourceTeam == identity.Team)
            return false;
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.Validate();
        if (float.IsNaN(sourceDamage) || float.IsInfinity(sourceDamage) || sourceDamage < 0f ||
            float.IsNaN(accuracy) || float.IsInfinity(accuracy))
            throw new ArgumentOutOfRangeException(nameof(sourceDamage));
        float chance = data.UseAccuracyAndDodge
            ? ApplicationChance(data.ApplicationChance, accuracy, identity.FinalStats.dodge)
            : data.ApplicationChance;
        if (chance <= 0f || (chance < 1f && UnityEngine.Random.value >= chance)) return false;
        foreach (StatusEffectRuntime runtime in effects)
        {
            if (runtime.Data != data) continue;
            runtime.Refresh(sourceTeam, sourceDamage);
            return true;
        }
        effects.Add(factory.Create(data, identity, sourceTeam, sourceDamage));
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (disposed || deltaTime <= 0f || owner.RuntimeData != identity) return;
        if (!identity.IsAlive) { Dispose(); return; }
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            StatusEffectRuntime runtime = effects[i];
            float activeTime = Mathf.Min(deltaTime, runtime.Remaining);
            runtime.Remaining = Mathf.Max(0f, runtime.Remaining - activeTime);
            runtime.Strategy.Tick(runtime, owner, activeTime);
            // Damage callbacks can despawn/reuse the owner and dispose this entire handler.
            if (disposed || owner.RuntimeData != identity) return;
            if (!identity.IsAlive) { Dispose(); return; }
            if (runtime.Remaining <= 0f)
            {
                effects.RemoveAt(i);
                runtime.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (StatusEffectRuntime runtime in effects) runtime.Dispose();
        effects.Clear();
    }
}
