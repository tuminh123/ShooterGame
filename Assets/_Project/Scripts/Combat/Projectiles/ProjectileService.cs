using System;
using UnityEngine;

public sealed class ProjectileService
{
    public sealed class Settings
    {
        public readonly ProjectileView Prefab;
        public Settings(ProjectileView prefab) => Prefab = prefab;
    }
    private readonly PoolManager pools;
    private readonly Settings settings;
    private readonly ProjectileStrategyFactory strategies;
    public ProjectileService(PoolManager pools, Settings settings, ProjectileStrategyFactory strategies)
    {
        this.pools = pools;
        this.settings = settings;
        this.strategies = strategies;
        if (settings == null || settings.Prefab == null)
            throw new InvalidOperationException("Assign the shared projectile shell.");
    }
    public bool Fire(ProjectileDataSO data, Vector2 position, Vector2 direction,
        CharacterTarget target, CharacterRuntimeData source)
    {
        if (data == null || source == null || !source.IsAlive) return false;
        data.Validate();
        if (float.IsNaN(direction.x) || float.IsNaN(direction.y) ||
            float.IsInfinity(direction.x) || float.IsInfinity(direction.y) ||
            direction.sqrMagnitude < 0.000001f)
            throw new ArgumentException("Projectile direction must be finite and nonzero.");
        var runtime = new ProjectileRuntimeData(data, source, target, direction,
            strategies.Flight(data), strategies.Hit(data));
        return pools.Spawn(settings.Prefab.gameObject, position, Quaternion.identity, PoolGroup.Bullets,
            beforeActivate: clone => clone.GetComponent<ProjectileView>().Initialize(this, runtime)) != null;
    }
    internal void Release(ProjectileView projectile) => pools.Despawn(projectile.gameObject);
    internal void PlayImpact(ImpactVfx prefab, Vector3 position)
    {
        if (prefab == null) return;
        pools.Spawn(prefab.gameObject, position, Quaternion.identity, PoolGroup.Other,
            beforeActivate: clone => clone.GetComponent<ImpactVfx>().Initialize(pools));
    }
}
