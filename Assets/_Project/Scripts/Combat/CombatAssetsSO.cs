using System;
using System.Collections.Generic;
using UnityEngine;

public enum BulletType { Holy, Fire, Ice, Poison }
public enum EffectType { None, Impact }

// Resources only. Skill assets own combat settings; pooled views own live state.
[CreateAssetMenu(menuName = "Shooter Game/Combat Assets")]
public sealed class CombatAssetsSO : ScriptableObject
{
    [Serializable]
    public struct BulletVisual
    {
        public BulletType type;
        public Sprite sprite;
        public Color color;
        [Min(0.01f)] public float visualWidth;
        public EffectType impactEffect;
    }

    [Serializable]
    public struct EffectEntry
    {
        public EffectType type; // ID chọn VFX; độc lập hiệu ứng gameplay như slow/cháy/độc.
        public EffectView prefab; // Vỏ quản lý tuổi thọ; có thể dùng chung cho nhiều entry.
        public GameObject visualPrefab; // Prefab con có component IEffectVisual trên gốc.
    }

    [SerializeField] private ProjectileView bulletPrefab;
    [SerializeField] private BulletVisual[] bullets = Array.Empty<BulletVisual>();
    [SerializeField] private EffectEntry[] effects = Array.Empty<EffectEntry>();

    public ProjectileView BulletPrefab => bulletPrefab;

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
        if (bulletPrefab == null || bullets == null || effects == null)
            throw new InvalidOperationException($"Assign combat resources: {name}.");
        var effectTypes = new HashSet<EffectType>();
        foreach (EffectEntry effect in effects)
            if (effect.type == EffectType.None || effect.prefab == null || effect.visualPrefab == null ||
                effect.visualPrefab.GetComponent<IEffectVisual>() == null || !effectTypes.Add(effect.type))
                throw new InvalidOperationException($"Missing or duplicate effect in {name}.");
        var bulletTypes = new HashSet<BulletType>();
        foreach (BulletVisual bullet in bullets)
        {
            if (!Enum.IsDefined(typeof(BulletType), bullet.type) || !bulletTypes.Add(bullet.type) ||
                bullet.sprite == null || !(bullet.visualWidth > 0f) || float.IsInfinity(bullet.visualWidth) ||
                (bullet.impactEffect != EffectType.None && !effectTypes.Contains(bullet.impactEffect)))
                throw new InvalidOperationException($"Invalid bullet visual: {bullet.type} in {name}.");
        }
    }
}
