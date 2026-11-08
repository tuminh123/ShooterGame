using System;

// Passive marker. Equipped bonuses live in SkillDataSO.StatBonus and are applied by the owner.
[Serializable]
public sealed class StatBonusSkill : ISkill, ISkillData
{
    [NonSerialized] private readonly CharacterSkills owner;

    public StatBonusSkill() { }

    public StatBonusSkill(CharacterSkills owner)
        => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public bool TryExecute() => owner != null && owner.CanRun;
}
