using System;

[Serializable]
public sealed class NormalAttackSkill : ISkill
{
    public (bool succeeded, IDisposable lifetime) Execute(
        CharacterView owner, CharacterTarget target, in SkillData data)
    {
        if (owner == null || !owner.isActiveAndEnabled || owner.RuntimeData == null ||
            !owner.RuntimeData.IsAlive || !target.TryGetCharacter(out CharacterView victim) ||
            owner.RuntimeData.Team == victim.RuntimeData.Team || !CharacterSearch.Overlaps(owner, victim))
            return (false, null);

        // Damage and crit come exclusively from current character stats, not skill assets or level.
        return (victim.ReceiveDamage(owner.RuntimeData.CreateDamage()).Accepted, null);
    }
}
