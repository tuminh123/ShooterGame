using UnityEngine;

// * Tạo đạn và VFX bằng pool hiện có; không tính damage hoặc quyết định ai cần tấn công.
public sealed class ProjectileService
{
    private readonly PoolManager pools;

    public ProjectileService(PoolManager pools) => this.pools = pools;

    public void Fire(ProjectileView prefab, Vector2 position, CharacterTarget target, DamageInfo damage)
    {
        if (!target.TryGetCharacter(out CharacterView character))
            return;
        Vector2 direction = (Vector2)character.HitPosition - position;
        if (direction.sqrMagnitude < 0.000001f)
            direction = Vector2.right;
        // * Chuẩn bị trạng thái khi object còn inactive để không chạy một frame với damage/hướng cũ.
        pools.Spawn(prefab.gameObject, position, Quaternion.identity, PoolGroup.Bullets,
            beforeActivate: clone => clone.GetComponent<ProjectileView>()
                .Initialize(this, target, damage, direction.normalized));
    }

    internal void Release(ProjectileView projectile) => pools.Despawn(projectile.gameObject);

    internal void PlayImpact(ImpactVfx prefab, Vector3 position)
    {
        if (prefab == null)
            return;
        pools.Spawn(prefab.gameObject, position, Quaternion.identity, PoolGroup.Other,
            beforeActivate: clone => clone.GetComponent<ImpactVfx>().Initialize(pools));
    }
}
