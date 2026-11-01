using System;
using UnityEngine;

[Serializable]
public sealed class RegenerationSkill : ISkill
{
    [SerializeField, Min(0f)] private float healAmount = 1f;
    [NonSerialized] private readonly Func<bool> canHeal;
    [NonSerialized] private readonly Action<float> heal;
    public float HealAmount => healAmount;
    public RegenerationSkill() { }
    public RegenerationSkill(float amount, Func<bool> canHeal, Action<float> heal)
    { healAmount = amount; this.canHeal = canHeal; this.heal = heal; }
    public bool TryExecute()
    {
        if (canHeal == null || heal == null || !canHeal() || healAmount <= 0f) return false;
        heal(healAmount);
        return true;
    }
}
