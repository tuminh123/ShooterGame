using System;

[Serializable]
public sealed class NormalAttackSkill : ISkill
{
    [NonSerialized] private readonly CharacterSkills owner;

    public NormalAttackSkill() { }

    public NormalAttackSkill(CharacterSkills owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public bool TryExecute()
    {
        if (owner == null || !owner.CanRun) return false;

        CharacterView character = owner.Owner;
        CharacterRuntimeData runtime = owner.Identity;
        CharacterTarget target = CharacterSearch.FindNearest(character.HitPosition,
            runtime.FinalStats.range, CharacterSearch.OpponentOf(runtime.Team), character);

        if (!target.TryGetCharacter(out CharacterView victim) || !CharacterSearch.Overlaps(character, victim))
            return false;

        return character.DealDamage(target).Accepted;
    }
}
