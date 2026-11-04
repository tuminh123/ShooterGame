using System;
using UnityEngine;

[Serializable]
public sealed class RegenerationSkill : ISkill, ISkillData
{
    [SerializeField, Min(0f)] private float healAmount = 1f;
    [NonSerialized] private readonly CharacterSkills owner;

    public float HealAmount => healAmount;

    public RegenerationSkill() { }

    public RegenerationSkill(CharacterSkills owner, float healAmount)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.healAmount = healAmount;
    }

    public bool TryExecute()
    {
        if (owner == null || !owner.CanRun || healAmount <= 0f) return false;
        if (owner.Identity.CurrentHealth >= owner.Identity.FinalStats.maxHealth) return false;

        owner.Owner.ChangeHealth(healAmount);
        return true;
    }
}
