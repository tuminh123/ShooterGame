using System;
using System.Collections.Generic;
using UnityEngine;

// Thông số của một viên đạn; AI/nút bấm cũng có thể tạo trực tiếp mà không cần skill.
public readonly struct ProjectileSpawnData
{
    public BulletType BulletType { get; } // Chọn hình ảnh trong catalog.
    public DamageInfo Damage { get; } // Sát thương và phe nguồn.
    public float Speed { get; } // World unit/giây.
    public float Lifetime { get; } // Tuổi thọ tính bằng giây.
    public IProjectileMove Movement { get; } // Cách tính hướng bay.
    public IReadOnlyList<IGameplayEffect> Effects { get; } // Cấu hình dùng chung, không chứa trạng thái runtime.
    public float EffectHit { get; } // Chính xác hiệu ứng của nguồn, chốt tại thời điểm bắn.

    public ProjectileSpawnData(BulletType bulletType, DamageInfo damage, float speed,
        float lifetime, IProjectileMove movement,
        IReadOnlyList<IGameplayEffect> effects = null, float effectHit = 0f)
    {
        BulletType = bulletType;
        Damage = damage;
        Speed = speed;
        Lifetime = lifetime;
        Movement = movement;
        Effects = effects ?? Array.Empty<IGameplayEffect>();
        EffectHit = effectHit;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(BulletType), BulletType) || Movement == null ||
            !IsPositiveFiniteNumber(Speed) || !IsPositiveFiniteNumber(Lifetime) ||
            float.IsNaN(EffectHit) || float.IsInfinity(EffectHit))
            throw new ArgumentException("Invalid projectile spawn data.");
        if (Effects == null) throw new ArgumentException("Initialize projectile spawn data before use.");
        // Duyệt theo index để không boxing enumerator của danh sách qua IReadOnlyList.
        for (int index = 0; index < Effects.Count; index++)
        {
            IGameplayEffect effect = Effects[index];
            if (effect == null) throw new ArgumentException("Projectile effects cannot contain null entries.");
            effect.Validate();
        }
    }

    // Chặn dữ liệu không hợp lệ từ các bên tạo đạn trực tiếp.
    private static bool IsPositiveFiniteNumber(float value)
        => value > 0f && !float.IsInfinity(value);
}

public sealed class CombatSpawner : IDisposable
{
    private readonly PoolManager poolManager;
    private readonly CombatAssetsSO combatAssets;
    private readonly HashSet<EffectView> activeEffects = new HashSet<EffectView>();
    private bool disposed;

    public CombatSpawner(PoolManager poolManager, CombatAssetsSO combatAssets)
    {
        this.poolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));
        this.combatAssets = combatAssets != null ? combatAssets : throw new ArgumentNullException(nameof(combatAssets));
        combatAssets.Validate();
        GameUIEventBus.StateChanged += OnGameStateChanged;
    }

    // Chỉ tạo một viên và gắn thông số; không xử lý số viên hay kiểu phát đạn.
    public bool SpawnBullet(ProjectileSpawnData projectileData, Vector2 spawnPosition,
        Vector2 shootingDirection, CharacterTarget target = default)
    {
        projectileData.Validate();
        if (!IsFiniteNumber(shootingDirection.x) || !IsFiniteNumber(shootingDirection.y) || shootingDirection.sqrMagnitude < 0.000001f)
            throw new ArgumentException("Bullet direction must be finite and nonzero.", nameof(shootingDirection));
        CombatAssetsSO.BulletVisual bulletVisual = combatAssets.GetBullet(projectileData.BulletType);
        return poolManager.Spawn(combatAssets.BulletPrefab.gameObject, spawnPosition, Quaternion.identity, PoolGroup.Bullets,
            beforeActivate: pooledObject =>
            {
                pooledObject.transform.localScale = combatAssets.BulletPrefab.transform.localScale;
                var view = pooledObject.GetComponent<ProjectileView>();
                try { view.Initialize(this, bulletVisual, projectileData, shootingDirection, target); }
                catch { view.ClearVisual(); throw; }
            }) != null;
    }

    public void SpawnEffect(EffectType effectType, Vector3 spawnPosition)
    {
        CombatAssetsSO.EffectEntry entry = combatAssets.GetEffect(effectType);
        if (effectType == EffectType.None) return;
        if (disposed) return;
        poolManager.Spawn(combatAssets.EffectPrefab.gameObject, spawnPosition, Quaternion.identity, PoolGroup.Other,
            beforeActivate: pooledObject =>
            {
                EffectView view = pooledObject.GetComponent<EffectView>();
                view.Initialize(this, entry.visualPrefab);
                activeEffects.Add(view);
            });
    }

    // Thu hồi các VFX một lần đang chạy khi trận kết thúc.
    public void ClearEffects()
    {
        foreach (EffectView effect in new List<EffectView>(activeEffects))
            if (effect != null) Release(effect.gameObject);
        activeEffects.Clear();
    }

    private void OnGameStateChanged(GameState previous, GameState current)
    {
        if (current == GameState.EndingWin || current == GameState.EndingLose)
            ClearEffects();
    }

    // Dọn visual con trước khi pool tắt/reparent vỏ; đạn vẫn dùng cùng API thu hồi.
    public void Release(GameObject instance)
    {
        if (instance == null) return;
        try
        {
            if (instance.TryGetComponent(out EffectView effect))
            {
                activeEffects.Remove(effect);
                effect.Clear();
            }
            if (instance.TryGetComponent(out ProjectileView projectile)) projectile.ClearVisual();
        }
        finally { poolManager.Despawn(instance); }
    }

    // Chặn NaN/vô cực trước khi dùng hướng bắn để di chuyển hoặc quét hitbox.
    private static bool IsFiniteNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        GameUIEventBus.StateChanged -= OnGameStateChanged;
        ClearEffects();
    }
}
