using System;
using UnityEngine;

// Action riêng để scheduler có thể đổi RunLevel; không còn closure che mất shooting runtime.
internal sealed class ShootingSkillAction : ISkill, IRunLevelSkill
{
    private readonly CharacterSkills owner;
    private readonly ShootingSkill data;
    private readonly CombatSpawner spawner;
    private readonly ShootBehaviorFactory shooters;
    private readonly ProjectileMoveFactory moves;
    private readonly float range;

    internal ShootingSkillAction(ShootingSkill template, CharacterSkills owner, CombatSpawner spawner,
        ShootBehaviorFactory shooters, ProjectileMoveFactory moves, float range, int maxLevel)
    {
        this.owner = owner;
        this.spawner = spawner;
        this.shooters = shooters;
        this.moves = moves;
        this.range = range;
        data = template.CreateRuntime(shooters, maxLevel);
    }

    public void SetRunLevel(int level) => data.ApplyRunLevel(level, shooters);

    public bool TryExecute()
    {
        if (!owner.CanRun) return false;
        CharacterTarget selected = default;
        Vector2 position = (Vector2)owner.Owner.HitPosition + data.MuzzleOffset;
        Vector2 direction = data.DefaultDirection.normalized;
        if (data.RequiresTarget || data.UsesHoming)
        {
            selected = owner.Owner.Search.FindNearest(owner.Owner.HitPosition, range,
                CharacterSearch.OpponentOf(owner.Identity.Team), owner.Owner);
            if (selected.TryGetCharacter(out CharacterView target))
            {
                Vector2 offset = (Vector2)target.HitPosition - position;
                if (offset.sqrMagnitude > 0.000001f) direction = offset.normalized;
            }
            else if (data.RequiresTarget) return false;
        }
        return data.ExecuteSkill(spawner, shooters, moves, owner.Identity, position, direction, selected) > 0;
    }
}
