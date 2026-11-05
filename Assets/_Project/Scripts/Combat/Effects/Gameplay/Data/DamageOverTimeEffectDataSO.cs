using System;
using UnityEngine;

// Separate Burn/Poison assets use the same periodic-damage strategy.
[CreateAssetMenu(menuName = "Shooter Game/Effects/Damage Over Time")]
public sealed class DamageOverTimeEffectDataSO : StatusEffectDataSO
{
    [SerializeField, Min(0.01f)] private float tickInterval = 1f;
    [SerializeField, Min(0f)] private float damageMultiplierPerTick = 0.2f;
    public float TickInterval => tickInterval;
    public float DamageMultiplierPerTick => damageMultiplierPerTick;
    public override void Validate()
    {
        base.Validate();
        if (!(tickInterval > 0f) || float.IsInfinity(tickInterval) ||
            float.IsNaN(damageMultiplierPerTick) || float.IsInfinity(damageMultiplierPerTick) ||
            damageMultiplierPerTick < 0f)
            throw new InvalidOperationException($"Invalid damage over time: {name}.");
    }
}
