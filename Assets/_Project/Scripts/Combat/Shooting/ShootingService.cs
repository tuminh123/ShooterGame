using UnityEngine;

// One shooting pipeline for players, enemies and bosses. Skills select all shot definitions.
public sealed class ShootingService
{
    private readonly ProjectileService projectiles;
    private readonly ShootingPatternFactory patterns;
    public ShootingService(ProjectileService projectiles, ShootingPatternFactory patterns)
    { this.projectiles = projectiles; this.patterns = patterns; }

    public int Fire(ProjectileSkillDataSO skill, ShootingRuntime runtime, CharacterRuntimeData source,
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
            // Phase belongs to the skill runtime, never to a shared pattern asset or strategy.
            runtime.Phase = Mathf.Repeat(runtime.Phase + skill.ShootingPattern.RotationPerCast, 360f);
        }
        return fired;
    }
}
