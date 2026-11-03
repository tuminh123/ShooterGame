using System.Collections.Generic;
using UnityEngine;

// One pooled shell. Copy gameplay values at spawn; never keep the owning skill alive.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ProjectileView : MonoBehaviour
{
    private CombatSpawner spawner;
    private SpriteRenderer sprite;
    private Vector2 direction;
    private CharacterTarget target;
    private DamageInfo damage;
    private IReadOnlyList<StatusEffectDataSO> effects;
    private EffectType impactEffect;
    private float sourceDamage;
    private float sourceEffectHit;
    private float speed;
    private float hitRadius;
    private float remaining;
    private bool homing;
    private bool flying;

    internal void Initialize(CombatSpawner spawner, CombatAssetsSO.BulletVisual visual,
        ShootingSkill skill, CharacterRuntimeData source, Vector2 direction, CharacterTarget target)
    {
        this.spawner = spawner;
        this.direction = direction.normalized;
        this.target = target;
        damage = source.CreateDamage(skill.DamageMultiplier);
        sourceDamage = source.FinalStats.damage;
        sourceEffectHit = source.FinalStats.effectHit;
        effects = skill.Effects;
        impactEffect = visual.impactEffect;
        speed = skill.Speed;
        hitRadius = skill.HitRadius;
        remaining = skill.Lifetime;
        homing = skill.Homing;
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        sprite.sprite = visual.sprite;
        sprite.color = visual.color;
        transform.localScale = Vector3.one * (visual.visualWidth / sprite.sprite.bounds.size.x);
        flying = true;
        FaceFlightDirection();
    }

    private void Update()
    {
        if (!flying || Time.deltaTime <= 0f) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            flying = false;
            spawner.Release(gameObject);
            return;
        }
        if (homing && target.TryGetCharacter(out CharacterView victim))
        {
            Vector2 toward = (Vector2)victim.HitPosition - (Vector2)transform.position;
            if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
        }
        FaceFlightDirection();

        CharacterTeam opponent = CharacterSearch.OpponentOf(damage.SourceTeam);
        CharacterTarget hit = CharacterSearch.FindNearest(transform.position, hitRadius, opponent);
        if (hit.TryGetCharacter(out _))
        {
            Hit(hit);
            return;
        }

        float distance = speed * Time.deltaTime;
        hit = CharacterSearch.FindFirstOnPath(transform.position, hitRadius, direction,
            distance, opponent, out Vector2 hitPosition);
        if (hit.TryGetCharacter(out _))
        {
            transform.position = new Vector3(hitPosition.x, hitPosition.y, transform.position.z);
            Hit(hit);
            return;
        }
        transform.position += (Vector3)(direction * distance);
    }

    private void Hit(CharacterTarget hit)
    {
        flying = false;
        CombatSpawner owner = spawner;
        Vector3 position = transform.position;
        try
        {
            if (!hit.TryGetCharacter(out CharacterView character)) return;
            bool applied = character.ReceiveDamage(damage).Accepted;
            // Damage can despawn/reuse the target shell. Check spawn identity again.
            if (hit.TryGetCharacter(out character))
                foreach (StatusEffectDataSO effect in effects)
                {
                    if (!hit.TryGetCharacter(out character)) break;
                    applied |= character.Effects.TryApply(effect, damage.SourceTeam, sourceDamage, sourceEffectHit);
                }
            if (applied) owner.SpawnEffect(impactEffect, position);
        }
        finally { owner.Release(gameObject); }
    }

    private void FaceFlightDirection()
        => transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

    private void OnDisable()
    {
        flying = false;
        spawner = null;
        effects = null;
        target = default;
        damage = default;
        direction = default;
        impactEffect = EffectType.None;
        sourceDamage = sourceEffectHit = speed = hitRadius = remaining = 0f;
        homing = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!flying) return;
        using (new UnityEditor.Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, hitRadius);
    }
#endif
}
