using System;
using UnityEngine;

// Temporary standalone shooting configuration; deliberately independent of the skill system.
[CreateAssetMenu(menuName = "Shooter Game/Shooting/Shot")]
public sealed class ShootingDataSO : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0.01f)] private float cooldown = 0.6f;
    [SerializeField] private bool autoCast = true;
    [SerializeField, Min(0.01f)] private float retryInterval = 0.1f;
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
    public float Cooldown => cooldown;
    public float RetryInterval => retryInterval;
    public bool AutoCast => autoCast;

    public void Validate()
    {
        if (!Positive(cooldown) || !Positive(retryInterval) || projectile == null || shootingPattern == null ||
            !Finite(defaultDirection.x) || !Finite(defaultDirection.y) ||
            !Finite(muzzleOffset.x) || !Finite(muzzleOffset.y) || defaultDirection.sqrMagnitude < 0.000001f)
            throw new InvalidOperationException($"Invalid standalone shot: {name}.");
        projectile.Validate();
        shootingPattern.Validate();
    }
    private static bool Positive(float v) => v > 0f && !float.IsInfinity(v);
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
