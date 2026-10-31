using System;
using UnityEngine;
[Serializable]
public sealed class StatBonusSkill : ISkill
{
    [SerializeField] private StatBonus bonus;

    public (bool succeeded, IDisposable lifetime) Execute(
        CharacterView owner, CharacterTarget target, in SkillData data)
        => (true, owner.RuntimeData.AddTemporaryBonus(bonus));
}
