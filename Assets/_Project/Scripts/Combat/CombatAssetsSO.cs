using System;
using System.Collections.Generic;
using UnityEngine;

public enum BulletType { Holy, Fire, Ice, Poison }
// Giữ ID 1 cho impact cũ để dữ liệu đã serialize không bị đổi loại.
public enum EffectType { None = 0, HolyImpact = 1, FireImpact = 2, IceImpact = 3, PoisonImpact = 4, LightningImpact = 5, WindImpact = 6 }

// Catalog sở hữu visual và hitbox theo loại đạn; skill giữ thông số bắn, view giữ state runtime.
[CreateAssetMenu(menuName = "Shooter Game/Combat Assets")]
public sealed class CombatAssetsSO : ScriptableObject
{
    [Serializable]
    public struct BulletVisual
    {
        public BulletType type;
        [Tooltip("Prefab hình ảnh con của đạn. Chỉnh màu, scale, sprite/animation/particle ngay trong prefab này; trục +X là hướng bay.")]
        public GameObject visualPrefab;
        [Tooltip("Hiệu ứng hình ảnh khi đạn trúng mục tiêu; không phải hiệu ứng gameplay như cháy hoặc độc.")]
        public EffectType impactEffect;
        [Min(0.001f)] public float hitRadius;
        [Tooltip("Tâm hitbox theo hướng bay: X phía trước, Y bên trái; world unit, không theo scale visual.")]
        public Vector2 hitOffset;
    }

    [Serializable]
    public struct EffectEntry
    {
        public EffectType type; // ID chọn VFX; độc lập hiệu ứng gameplay như slow/cháy/độc.
        [Tooltip("Prefab visual con có AnimationEffectVisual hoặc ParticleEffectVisual trên root. Visual báo Completed để vỏ tự thu hồi.")]
        public GameObject visualPrefab;
    }

    [Header("Shared view prefabs")]
    [Tooltip("Vỏ ProjectileView dùng chung cho mọi loại đạn, không chứa SpriteRenderer.")]
    [SerializeField, InspectorName("Projectile View Prefab")] private ProjectileView bulletPrefab;
    [Tooltip("Vỏ EffectView dùng chung; mỗi entry bên dưới chọn một visual riêng.")]
    [SerializeField, InspectorName("Effect View Prefab")] private EffectView effectPrefab;
    [Header("Visual prefabs by type")]
    [SerializeField] private BulletVisual[] bullets = Array.Empty<BulletVisual>();
    [SerializeField] private EffectEntry[] effects = Array.Empty<EffectEntry>();

    public ProjectileView BulletPrefab => bulletPrefab;
    public EffectView EffectPrefab => effectPrefab;
    public IReadOnlyList<BulletVisual> BulletEntries => bullets;
    public IReadOnlyList<EffectEntry> EffectEntries => effects;

    public BulletVisual GetBullet(BulletType type)
    {
        foreach (BulletVisual bullet in bullets)
            if (bullet.type == type) return bullet;
        throw new InvalidOperationException($"Missing bullet visual: {type} in {name}.");
    }

    public EffectEntry GetEffect(EffectType type)
    {
        if (type == EffectType.None) return default;
        foreach (EffectEntry effect in effects)
            if (effect.type == type) return effect;
        throw new InvalidOperationException($"Missing effect prefab: {type} in {name}.");
    }

    public void Validate()
    {
        if (bulletPrefab == null || effectPrefab == null || bullets == null || effects == null)
            throw new InvalidOperationException($"Assign combat resources: {name}.");
        var effectTypes = new HashSet<EffectType>();
        foreach (EffectEntry effect in effects)
            if (effect.type == EffectType.None || !Enum.IsDefined(typeof(EffectType), effect.type) || effect.visualPrefab == null ||
                effect.visualPrefab.GetComponent<IEffectVisual>() == null || !effectTypes.Add(effect.type))
                throw new InvalidOperationException($"Missing or duplicate effect in {name}.");
        var bulletTypes = new HashSet<BulletType>();
        foreach (BulletVisual bullet in bullets)
        {
            if (!Enum.IsDefined(typeof(BulletType), bullet.type) || !bulletTypes.Add(bullet.type) ||
                bullet.visualPrefab == null || !(bullet.hitRadius > 0f) || float.IsInfinity(bullet.hitRadius) ||
                float.IsNaN(bullet.hitOffset.x) || float.IsInfinity(bullet.hitOffset.x) ||
                float.IsNaN(bullet.hitOffset.y) || float.IsInfinity(bullet.hitOffset.y) ||
                (bullet.impactEffect != EffectType.None && !effectTypes.Contains(bullet.impactEffect)))
                throw new InvalidOperationException($"Invalid bullet visual: {bullet.type} in {name}.");
        }
    }
}
