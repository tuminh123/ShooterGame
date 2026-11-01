using System;
using UnityEngine;

// Projectile and pattern configuration; skill assets own timing.
[CreateAssetMenu(menuName = "Shooter Game/Shooting/Shot")]
public sealed class ShootingDataSO : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private ProjectileDataSO projectile;
    [SerializeField] private ShootingPatternDataSO shootingPattern;
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f);
    [SerializeField] private bool requiresTarget = true;
    [SerializeField] private Vector2 defaultDirection = Vector2.right;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public ProjectileDataSO Projectile => projectile;
    public ShootingPatternDataSO ShootingPattern => shootingPattern;
    public Vector2 MuzzleOffset => muzzleOffset;
    public bool RequiresTarget => requiresTarget;
    public Vector2 DefaultDirection => defaultDirection;

    public void Validate()
    {
        if (projectile == null || shootingPattern == null ||
            !Finite(defaultDirection.x) || !Finite(defaultDirection.y) ||
            !Finite(muzzleOffset.x) || !Finite(muzzleOffset.y) || defaultDirection.sqrMagnitude < 0.000001f)
            throw new InvalidOperationException($"Invalid standalone shot: {name}.");
        projectile.Validate();
        shootingPattern.Validate();
    }
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
