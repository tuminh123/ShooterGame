using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterEffectHandler : IDisposable
{
    private readonly CharacterView owner;
    private readonly CharacterRuntimeData identity;
    private readonly StatusEffectFactory factory;
    private readonly List<StatusEffectRuntime> effects = new List<StatusEffectRuntime>();
    private readonly Dictionary<long, float> speedMultipliers = new Dictionary<long, float>();
    private long nextModifierId;
    private bool disposed;
    public int Count => effects.Count;

    public CharacterEffectHandler(CharacterView owner, StatusEffectFactory factory)
    { this.owner = owner; identity = owner.RuntimeData; this.factory = factory; }

    // All inputs and the result are percentages: 100 = 100%.
    public static float ApplicationChance(float baseChance, float effectHit, float effectResistance)
    {
        float resistance = Mathf.Clamp(effectResistance, 0f, 80f);
        return Mathf.Clamp(baseChance * (1f + Mathf.Max(0f, effectHit) / 100f) *
            (1f - resistance / 100f), 0f, 100f);
    }

    public bool TryApply(StatusEffectDataSO data, CharacterTeam sourceTeam, float sourceDamage, float effectHit)
    {
        if (disposed || owner.RuntimeData != identity || !identity.IsAlive || sourceTeam == identity.Team)
            return false;
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.Validate();
        if (float.IsNaN(sourceDamage) || float.IsInfinity(sourceDamage) || sourceDamage < 0f ||
            float.IsNaN(effectHit) || float.IsInfinity(effectHit))
            throw new ArgumentOutOfRangeException(nameof(sourceDamage));
        float chance = data.UseEffectStats
            ? ApplicationChance(data.ApplicationChance, effectHit, identity.FinalStats.effectResistance)
            : data.ApplicationChance;
        if (chance <= 0f || (chance < 100f && UnityEngine.Random.value * 100f >= chance)) return false;
        foreach (StatusEffectRuntime runtime in effects)
        {
            if (runtime.Data != data) continue;
            runtime.Refresh(sourceTeam, sourceDamage);
            return true;
        }
        StatusEffectRuntime created = factory.Create(data, this, sourceTeam, sourceDamage);
        if (disposed || owner.RuntimeData != identity || !identity.IsAlive)
        {
            created.Dispose();
            return false;
        }
        effects.Add(created);
        return true;
    }

    // Slow ownership and strongest-slow selection belong to Effect, not character data.
    internal IDisposable AddSpeedMultiplier(float multiplier)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterEffectHandler));
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f || multiplier > 1f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        long id = checked(++nextModifierId);
        speedMultipliers.Add(id, multiplier);
        try { RefreshMovementScale(); }
        catch
        {
            speedMultipliers.Remove(id);
            RefreshMovementScale();
            throw;
        }
        return new StatModifierLease(() =>
        {
            if (speedMultipliers.Remove(id)) RefreshMovementScale();
        });
    }

    private void RefreshMovementScale()
    {
        float scale = 1f;
        foreach (float multiplier in speedMultipliers.Values) scale = Mathf.Min(scale, multiplier);
        identity.SetMovementScale(scale);
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
