using System;
using UnityEngine;
[CreateAssetMenu(menuName = "Shooter Game/Skills/Passive Stat Bonus")]
public sealed class StatBonusSkillDataSO : SkillDataSO
{
    [SerializeField] private StatBonus bonus;
    public StatBonus Bonus => bonus;
    public override void Validate()
    {
        base.Validate();
        if (Category != SkillCategory.Passive || StrategyType != typeof(StatBonusSkill))
            throw new InvalidOperationException($"Stat bonus skill must be Passive and use StatBonusSkill: {name}.");
    }
}
