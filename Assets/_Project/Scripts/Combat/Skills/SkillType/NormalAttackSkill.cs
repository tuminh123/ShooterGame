using System;

[Serializable]
public sealed class NormalAttackSkill : ISkill, ISkillData
{
    [NonSerialized] private readonly CharacterSkills owner;
    [NonSerialized] private readonly float range;

    public NormalAttackSkill() { }

    public NormalAttackSkill(CharacterSkills owner, float range)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.range = range;
    }

    public bool TryExecute()
    {
        if (owner == null || !owner.CanRun) return false;

        CharacterView character = owner.Owner;
        CharacterRuntimeData runtime = owner.Identity;
        CharacterTarget target = character.Search.FindNearest(character.HitPosition,
            range, CharacterSearch.OpponentOf(runtime.Team), character);

        if (!target.TryGetCharacter(out CharacterView victim) || !CharacterSearch.Overlaps(character, victim))
            return false;

        return character.DealDamage(target).Accepted;
    }
}
