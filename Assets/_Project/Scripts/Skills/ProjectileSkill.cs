using System;
using UnityEngine;
[Serializable]
public sealed class ProjectileSkill : ISkill
{
    public SkillExecution Execute(SkillContext context, SkillRuntime runtime)
    {
        var data = (ProjectileSkillDataSO)runtime.Data;
        CharacterView owner = context.Owner;
        CharacterTarget target = default;
        Vector2 position = (Vector2)owner.HitPosition + data.MuzzleOffset;
        Vector2 direction = data.DefaultDirection.normalized;
        if (data.RequiresTarget)
        {
            target = CharacterSearch.FindNearest(owner.HitPosition, owner.RuntimeData.FinalStats.range,
                CharacterSearch.OpponentOf(owner.RuntimeData.Team), owner);
            if (!target.TryGetCharacter(out CharacterView character)) return default;
            Vector2 toward = (Vector2)character.HitPosition - position;
            if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
        }
        return new SkillExecution(context.Shooting.Fire(data, runtime.Shooting, owner.RuntimeData,
            position, direction, target) > 0);
    }
}
