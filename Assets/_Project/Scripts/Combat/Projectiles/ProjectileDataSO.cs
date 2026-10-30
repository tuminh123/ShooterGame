using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Projectiles/Projectile")]
public sealed class ProjectileDataSO : ScriptableObject
{
    [SerializeField] private Sprite sprite;
    [SerializeField] private Color color = Color.white;
    [SerializeField, Min(0.01f)] private float visualWidth = 0.18f;
    [SerializeField, Min(0.01f)] private float speed = 2.4f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.035f;
    [SerializeField, Min(0.01f)] private float lifetime = 3f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField] private ImpactVfx impactPrefab;
    [SerializeReference] private IProjectileFlight flight;
    [SerializeReference] private IProjectileHit hit;
    [SerializeField] private StatusEffectDataSO[] effects = Array.Empty<StatusEffectDataSO>();
    public Sprite Sprite => sprite;
    public Color Color => color;
    public float VisualWidth => visualWidth;
    public float Speed => speed;
    public float HitRadius => hitRadius;
    public float Lifetime => lifetime;
    public float DamageMultiplier => damageMultiplier;
    public ImpactVfx ImpactPrefab => impactPrefab;
    public Type FlightType => flight?.GetType();
    public Type HitType => hit?.GetType();
    public System.Collections.Generic.IReadOnlyList<StatusEffectDataSO> Effects => effects;
    public void Validate()
    {
        if (sprite == null || flight == null || hit == null || !Positive(visualWidth) ||
            !Positive(speed) || !Positive(hitRadius) || !Positive(lifetime) ||
            float.IsNaN(damageMultiplier) || float.IsInfinity(damageMultiplier) || damageMultiplier < 0f ||
            effects == null)
            throw new InvalidOperationException($"Invalid projectile configuration: {name}.");
        foreach (StatusEffectDataSO effect in effects)
        {
            if (effect == null) throw new InvalidOperationException($"Missing projectile effect: {name}.");
            effect.Validate();
        }
    }
    private static bool Positive(float v) => v > 0f && !float.IsInfinity(v);
}
