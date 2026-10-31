using UnityEngine;

// Standalone shooting pipeline while projectile attacks are kept outside SkillManager.
public sealed class ShootingService
{
    private readonly ProjectileService projectiles;
    private readonly ShootingPatternFactory patterns;
    public ShootingService(ProjectileService projectiles, ShootingPatternFactory patterns)
    { this.projectiles = projectiles; this.patterns = patterns; }

    public int Fire(ShootingDataSO skill, ShootingRuntime runtime, CharacterRuntimeData source,
        Vector2 position, Vector2 aim, CharacterTarget target)
    {
        IShootingPattern pattern = patterns.Resolve(skill.ShootingPattern);
        int fired = 0;
        for (int i = 0; i < skill.ShootingPattern.ProjectileCount; i++)
        {
            Vector2 direction = pattern.GetDirection(skill.ShootingPattern, runtime, aim, i);
            if (projectiles.Fire(skill.Projectile, position, direction, target, source)) fired++;
        }
        if (fired > 0)
        {
            runtime.CastCount++;
            // Phase belongs to this shot's runtime, never to a shared pattern asset or strategy.
            runtime.Phase = Mathf.Repeat(runtime.Phase + skill.ShootingPattern.RotationPerCast, 360f);
        }
        return fired;
    }

    public void Tick(CharacterView owner, ShootingDataSO[] definitions, ShootingRuntime[] states, float deltaTime)
    {
        if (deltaTime <= 0f || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return;
        CharacterRuntimeData identity = owner.RuntimeData;
        for (int i = 0; i < definitions.Length; i++)
        {
            if (owner.RuntimeData != identity || !identity.IsAlive || !owner.isActiveAndEnabled) return;
            ShootingDataSO shot = definitions[i];
            ShootingRuntime state = states[i];
            state.CooldownRemaining = Mathf.Max(0f, state.CooldownRemaining - deltaTime);
            state.RetryRemaining = Mathf.Max(0f, state.RetryRemaining - deltaTime);
            if (!shot.AutoCast || state.CooldownRemaining > 0f || state.RetryRemaining > 0f) continue;
            CharacterTarget target = default;
            Vector2 position = (Vector2)owner.HitPosition + shot.MuzzleOffset;
            Vector2 direction = shot.DefaultDirection.normalized;
            if (shot.RequiresTarget)
            {
                target = CharacterSearch.FindNearest(owner.HitPosition, identity.FinalStats.range,
                    CharacterSearch.OpponentOf(identity.Team), owner);
                if (!target.TryGetCharacter(out CharacterView character))
                { state.RetryRemaining = shot.RetryInterval; continue; }
                Vector2 toward = (Vector2)character.HitPosition - position;
                if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
            }
            if (Fire(shot, state, identity, position, direction, target) > 0)
                state.CooldownRemaining = shot.Cooldown;
            else state.RetryRemaining = shot.RetryInterval;
        }
    }
}
