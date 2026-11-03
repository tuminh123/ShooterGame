using System;
using UnityEngine;

public sealed class CombatSpawner
{
    private readonly PoolManager pools;
    private readonly CombatAssetsSO assets;

    public CombatSpawner(PoolManager pools, CombatAssetsSO assets)
    {
        this.pools = pools ?? throw new ArgumentNullException(nameof(pools));
        this.assets = assets != null ? assets : throw new ArgumentNullException(nameof(assets));
        assets.Validate();
    }

    public bool SpawnBullet(ShootingSkill skill, CharacterRuntimeData source,
        Vector2 position, Vector2 direction, CharacterTarget target)
    {
        if (source == null || !source.IsAlive) return false;
        if (!Finite(direction.x) || !Finite(direction.y) || direction.sqrMagnitude < 0.000001f)
            throw new ArgumentException("Bullet direction must be finite and nonzero.", nameof(direction));
        CombatAssetsSO.BulletVisual visual = assets.GetBullet(skill.BulletType);
        return pools.Spawn(assets.BulletPrefab.gameObject, position, Quaternion.identity, PoolGroup.Bullets,
            beforeActivate: clone => clone.GetComponent<ProjectileView>().Initialize(
                this, visual, skill, source, direction, target)) != null;
    }

    public void SpawnEffect(EffectType type, Vector3 position)
    {
        EffectView prefab = assets.GetEffect(type);
        if (prefab == null) return;
        pools.Spawn(prefab.gameObject, position, Quaternion.identity, PoolGroup.Other,
            beforeActivate: clone => clone.GetComponent<EffectView>().Initialize(this));
    }

    public void Release(GameObject instance) => pools.Despawn(instance);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
