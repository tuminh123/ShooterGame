using System;
using UnityEngine;

[Serializable]
public sealed class EmergencyShieldSkill : ISkill
{
    [SerializeField, Range(0f, 100f)] private float healthThreshold = 20f;
    [SerializeField, Min(0f)] private float shieldAmount = 30f;
    [NonSerialized] private readonly Func<float> getHealthPercent;
    [NonSerialized] private readonly Action<float> grantShield;
    public float HealthThreshold => healthThreshold;
    public float ShieldAmount => shieldAmount;
    public EmergencyShieldSkill() { }
    public EmergencyShieldSkill(float threshold, float amount, Func<float> healthPercent, Action<float> grantShield)
    { healthThreshold = threshold; shieldAmount = amount; getHealthPercent = healthPercent; this.grantShield = grantShield; }
    public bool TryExecute()
    {
        if (getHealthPercent == null || grantShield == null || shieldAmount <= 0f ||
            getHealthPercent() > healthThreshold) return false;
        grantShield(shieldAmount);
        return true;
    }
}
