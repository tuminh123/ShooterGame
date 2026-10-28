using System;
using UnityEngine;
[CreateAssetMenu(menuName = "Shooter Game/Skills/Projectile Skill")]
public sealed class ProjectileSkillDataSO : SkillDataSO
{
    [SerializeField] private ProjectileDataSO projectile;
    [SerializeField] private ShootingPatternDataSO shootingPattern;
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f);
    [SerializeField] private bool requiresTarget = true;
    [SerializeField] private Vector2 defaultDirection = Vector2.right;
    public ProjectileDataSO Projectile => projectile;
    public ShootingPatternDataSO ShootingPattern => shootingPattern;
    public Vector2 MuzzleOffset => muzzleOffset;
    public bool RequiresTarget => requiresTarget;
    public Vector2 DefaultDirection => defaultDirection;
    public override void Validate()
    {
        base.Validate();
        if (StrategyType != typeof(ProjectileSkill) || Category != SkillCategory.Active ||
            projectile == null || shootingPattern == null ||
            !Finite(defaultDirection.x) || !Finite(defaultDirection.y) ||
            !Finite(muzzleOffset.x) || !Finite(muzzleOffset.y) ||
            defaultDirection.sqrMagnitude < 0.000001f)
            throw new InvalidOperationException($"Projectile skill needs projectile, pattern and direction: {name}.");
        projectile.Validate();
        shootingPattern.Validate();
    }
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
