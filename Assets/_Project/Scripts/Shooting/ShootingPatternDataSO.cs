using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Shooting/Pattern")]
public sealed class ShootingPatternDataSO : ScriptableObject
{
    [SerializeReference] private IShootingPattern pattern;
    [SerializeField, Min(1)] private int projectileCount = 1;
    [SerializeField, Range(0f, 360f)] private float spreadAngle = 30f;
    [SerializeField] private float startAngle;
    [SerializeField] private float rotationPerCast = 15f;
    public Type StrategyType => pattern?.GetType();
    public int ProjectileCount => projectileCount;
    public float SpreadAngle => spreadAngle;
    public float StartAngle => startAngle;
    public float RotationPerCast => rotationPerCast;
    public void Validate()
    {
        if (pattern == null || projectileCount < 1 || projectileCount > 1024 ||
            !Finite(spreadAngle) || spreadAngle < 0f || spreadAngle > 360f ||
            !Finite(startAngle) || !Finite(rotationPerCast))
            throw new InvalidOperationException($"Invalid shooting pattern: {name}.");
    }
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
