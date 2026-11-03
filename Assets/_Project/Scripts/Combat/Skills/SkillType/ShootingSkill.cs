using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShootingSkill : ISkill
{
    [Header("Shooting")]
    [SerializeField, ShooterType] private string shooterType = "StraightShooter";
    [SerializeField] private BulletType bulletType;
    [SerializeField, Min(1)] private int projectileCount = 1;
    [SerializeField, Range(0f, 360f)] private float spreadAngle;
    [SerializeField] private float startAngle;
    [SerializeField] private float rotationPerCast = 15f;
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f);
    [SerializeField] private bool requiresTarget = true;
    [SerializeField] private Vector2 defaultDirection = Vector2.right;

    [Header("Bullet Gameplay")]
    [SerializeField, Min(0.01f)] private float speed = 2.4f;
    [SerializeField, Min(0.001f)] private float hitRadius = 0.035f;
    [SerializeField, Min(0.01f)] private float lifetime = 3f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField] private bool homing;
    [SerializeField] private StatusEffectDataSO[] effects = Array.Empty<StatusEffectDataSO>();

    [NonSerialized] private readonly CharacterSkills owner;
    [NonSerialized] private readonly CombatSpawner spawner;
    [NonSerialized] private readonly Dictionary<Type, IShooter> shooters;
    [NonSerialized] private readonly Type selectedType;
    [NonSerialized] private float phase;

    public BulletType BulletType => bulletType;
    public int ProjectileCount => projectileCount;
    public float SpreadAngle => spreadAngle;
    public float StartAngle => startAngle;
    public float Phase => phase;
    public float Speed => speed;
    public float HitRadius => hitRadius;
    public float Lifetime => lifetime;
    public float DamageMultiplier => damageMultiplier;
    public bool Homing => homing;
    public IReadOnlyList<StatusEffectDataSO> Effects => effects;

    public ShootingSkill() { }

    public ShootingSkill(CharacterSkills owner, ShootingSkill template, CombatSpawner spawner,
        Dictionary<Type, IShooter> shooters)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.shooters = shooters ?? throw new ArgumentNullException(nameof(shooters));
        if (template == null) throw new ArgumentNullException(nameof(template));
        template.Validate();
        selectedType = Type.GetType(template.shooterType);
        if (!shooters.ContainsKey(selectedType))
            throw new InvalidOperationException($"Unregistered shooter: {template.shooterType}.");

        shooterType = template.shooterType;
        bulletType = template.bulletType;
        projectileCount = template.projectileCount;
        spreadAngle = template.spreadAngle;
        startAngle = template.startAngle;
        rotationPerCast = template.rotationPerCast;
        muzzleOffset = template.muzzleOffset;
        requiresTarget = template.requiresTarget;
        defaultDirection = template.defaultDirection;
        speed = template.speed;
        hitRadius = template.hitRadius;
        lifetime = template.lifetime;
        damageMultiplier = template.damageMultiplier;
        homing = template.homing;
        effects = (StatusEffectDataSO[])template.effects.Clone();
    }

    public bool TryExecute()
    {
        if (owner == null || !owner.CanRun) return false;

        CharacterView character = owner.Owner;
        CharacterRuntimeData runtime = owner.Identity;
        CharacterTarget target = default;
        Vector2 position = (Vector2)character.HitPosition + muzzleOffset;
        Vector2 direction = defaultDirection.normalized;

        if (requiresTarget)
        {
            target = CharacterSearch.FindNearest(character.HitPosition, runtime.FinalStats.range,
                CharacterSearch.OpponentOf(runtime.Team), character);
            if (!target.TryGetCharacter(out CharacterView victim)) return false;
            Vector2 toward = (Vector2)victim.HitPosition - position;
            if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
        }

        int fired = shooters[selectedType].Shoot(this, position, direction, target);
        if (fired == 0) return false;
        phase = Mathf.Repeat(phase + rotationPerCast, 360f);
        return true;
    }

    internal bool SpawnBullet(Vector2 position, Vector2 direction, CharacterTarget target)
        => owner.CanRun && spawner.SpawnBullet(this, owner.Identity, position, direction, target);

    public void Validate()
    {
        Type type = string.IsNullOrEmpty(shooterType) ? null : Type.GetType(shooterType, false);
        if (type == null || type.IsAbstract || !typeof(IShooter).IsAssignableFrom(type) ||
            !Enum.IsDefined(typeof(BulletType), bulletType) || projectileCount < 1 || projectileCount > 1024 ||
            !Finite(spreadAngle) || spreadAngle < 0f || spreadAngle > 360f ||
            !Finite(startAngle) || !Finite(rotationPerCast) ||
            !Finite(muzzleOffset.x) || !Finite(muzzleOffset.y) ||
            !Finite(defaultDirection.x) || !Finite(defaultDirection.y) || defaultDirection.sqrMagnitude < 0.000001f ||
            !Positive(speed) || !Positive(hitRadius) || !Positive(lifetime) ||
            !Finite(damageMultiplier) || damageMultiplier < 0f || effects == null)
            throw new InvalidOperationException("Invalid shooting skill configuration.");
        foreach (StatusEffectDataSO effect in effects)
        {
            if (effect == null) throw new InvalidOperationException("Missing bullet status effect.");
            effect.Validate();
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Positive(float value) => value > 0f && Finite(value);
}
