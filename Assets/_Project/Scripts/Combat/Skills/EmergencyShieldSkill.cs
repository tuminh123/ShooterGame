using System;
using UnityEngine;

[Serializable]
public sealed class EmergencyShieldSkill : ISkill
{
    [SerializeField, Range(0f, 100f)] private float healthThreshold = 20f;
    [SerializeField, Min(0f)] private float shieldAmount = 30f;
    [NonSerialized] private readonly CharacterSkills owner;

    public float HealthThreshold => healthThreshold;
    public float ShieldAmount => shieldAmount;

    public EmergencyShieldSkill() { }

    public EmergencyShieldSkill(CharacterSkills owner, float healthThreshold, float shieldAmount)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.healthThreshold = healthThreshold;
        this.shieldAmount = shieldAmount;
    }

    public bool TryExecute()
    {
        if (owner == null || !owner.CanRun || shieldAmount <= 0f) return false;

        float healthPercent = owner.Identity.CurrentHealth / owner.Identity.FinalStats.maxHealth * 100f;
        if (healthPercent > healthThreshold) return false;

        owner.Owner.GrantShield(shieldAmount);
        return true;
    }
}
