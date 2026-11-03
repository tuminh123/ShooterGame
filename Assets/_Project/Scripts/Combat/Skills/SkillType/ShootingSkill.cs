using System;
using UnityEngine;

[Serializable]
public sealed class ShootingSkill : ISkill
{
    [SerializeField] private ShootingDataSO shooting;
    [NonSerialized] private readonly CharacterSkills owner;
    [NonSerialized] private readonly ShootingService service;
    [NonSerialized] private readonly ShootingRuntime state;

    public ShootingDataSO Shooting => shooting;

    public ShootingSkill() { }

    public ShootingSkill(CharacterSkills owner, ShootingDataSO shooting, ShootingService service)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.shooting = shooting != null ? shooting : throw new ArgumentNullException(nameof(shooting));
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        state = new ShootingRuntime();
    }

    public bool TryExecute()
    {
        if (owner == null || !owner.CanRun) return false;

        CharacterView character = owner.Owner;
        CharacterRuntimeData runtime = owner.Identity;
        CharacterTarget target = default;
        Vector2 position = (Vector2)character.HitPosition + shooting.MuzzleOffset;
        Vector2 direction = shooting.DefaultDirection.normalized;

        if (shooting.RequiresTarget)
        {
            target = CharacterSearch.FindNearest(character.HitPosition, runtime.FinalStats.range,
                CharacterSearch.OpponentOf(runtime.Team), character);
            if (!target.TryGetCharacter(out CharacterView victim)) return false;

            Vector2 toward = (Vector2)victim.HitPosition - position;
            if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
        }

        return service.Fire(shooting, state, runtime, position, direction, target) > 0;
    }
}
