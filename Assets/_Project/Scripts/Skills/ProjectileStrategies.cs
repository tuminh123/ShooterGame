using System;
using System.Collections.Generic;
using UnityEngine;

public interface IProjectileFlight
{
    Vector2 GetDirection(ProjectileRuntimeData runtime, Vector2 position);
}
[Serializable]
public sealed class StraightProjectileFlight : IProjectileFlight
{
    public Vector2 GetDirection(ProjectileRuntimeData runtime, Vector2 position) => runtime.Direction;
}
[Serializable]
public sealed class HomingProjectileFlight : IProjectileFlight
{
    public Vector2 GetDirection(ProjectileRuntimeData runtime, Vector2 position)
    {
        if (runtime.Target.TryGetCharacter(out CharacterView target))
        {
            Vector2 toward = (Vector2)target.HitPosition - position;
            if (toward.sqrMagnitude > 0.000001f) return toward.normalized;
        }
        return runtime.Direction;
    }
}
public interface IProjectileHit
{
    bool Apply(ProjectileRuntimeData runtime, CharacterTarget target);
}
[Serializable]
public sealed class SingleTargetProjectileHit : IProjectileHit
{
    public bool Apply(ProjectileRuntimeData runtime, CharacterTarget target)
    {
        if (!target.TryGetCharacter(out CharacterView character)) return false;
        DamageResult result = character.ReceiveDamage(runtime.Damage);
        bool applied = result.Accepted;
        // Recheck lifetime identity: the damage callback may despawn and reuse this shell.
        if (target.TryGetCharacter(out character))
            foreach (StatusEffectDataSO effect in runtime.Data.Effects)
                applied |= character.Effects.TryApply(effect, runtime.Damage.SourceTeam, runtime.SourceDamage,
                    runtime.SourceAccuracy);
        return applied;
    }
}
public sealed class ProjectileStrategyFactory
{
    private readonly Dictionary<Type, IProjectileFlight> flights = new Dictionary<Type, IProjectileFlight>();
    private readonly Dictionary<Type, IProjectileHit> hits = new Dictionary<Type, IProjectileHit>();
    public ProjectileStrategyFactory()
    {
        Register(new StraightProjectileFlight());
        Register(new HomingProjectileFlight());
        Register(new SingleTargetProjectileHit());
    }
    public void Register(IProjectileFlight strategy) => flights.Add(strategy.GetType(), strategy);
    public void Register(IProjectileHit strategy) => hits.Add(strategy.GetType(), strategy);
    public IProjectileFlight Flight(ProjectileDataSO data)
    {
        if (!flights.TryGetValue(data.FlightType, out IProjectileFlight strategy))
            throw new InvalidOperationException($"Unregistered projectile flight: {data.FlightType}.");
        return strategy;
    }
    public IProjectileHit Hit(ProjectileDataSO data)
    {
        if (!hits.TryGetValue(data.HitType, out IProjectileHit strategy))
            throw new InvalidOperationException($"Unregistered projectile hit: {data.HitType}.");
        return strategy;
    }
}
