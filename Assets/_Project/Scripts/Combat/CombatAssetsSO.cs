using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catalog hình ảnh của combat: prefab vỏ đạn, visual và hitbox theo <see cref="BulletType"/>,
/// prefab vỏ VFX và visual theo <see cref="EffectType"/>.
/// </summary>
/// <remarks>Skill giữ thông số bắn, view giữ state runtime; asset này chỉ chọn hình ảnh.</remarks>
[CreateAssetMenu(menuName = "Shooter Game/Combat Assets")]
public sealed class CombatAssetsSO : ScriptableObject
{
    [Serializable]
    public struct BulletVisual
    {
        [Tooltip("Loại đạn được skill dùng để tra visual và hitbox. Mỗi BulletType chỉ khai báo một lần.")]
        [SerializeField] private BulletType type;
        [Tooltip("Prefab hình ảnh con của đạn. Chỉnh màu, scale, sprite/animation/particle ngay trong prefab này; trục +X là hướng bay.")]
        [SerializeField] private GameObject visualPrefab;
        [Tooltip("Hiệu ứng hình ảnh khi đạn trúng mục tiêu; không phải hiệu ứng gameplay như cháy hoặc độc.")]
        [SerializeField] private EffectType impactEffect;
        [Tooltip("Bán kính hitbox quét va chạm của đạn, theo world unit; phải lớn hơn 0, độc lập kích thước visual.")]
        [SerializeField][Min(0.001f)] private float hitRadius;
        [Tooltip("Tâm hitbox theo hướng bay: X phía trước, Y bên trái; world unit, không theo scale visual.")]
        [SerializeField] private Vector2 hitOffset;

        /// <summary>Loại đạn được skill dùng để tra visual và hitbox. Mỗi BulletType chỉ khai báo một lần.</summary>
        public BulletType Type => type;
        /// <summary>Prefab hình ảnh con của đạn. Chỉnh màu, scale, sprite/animation/particle ngay trong prefab này; trục +X là hướng bay.</summary>
        public GameObject VisualPrefab => visualPrefab;
        /// <summary>Hiệu ứng hình ảnh khi đạn trúng mục tiêu; không phải hiệu ứng gameplay như cháy hoặc độc.</summary>
        public EffectType ImpactEffect => impactEffect;
        /// <summary>Bán kính hitbox quét va chạm của đạn, theo world unit; phải lớn hơn 0, độc lập kích thước visual.</summary>
        public float HitRadius => hitRadius;
        /// <summary>Tâm hitbox theo hướng bay: X phía trước, Y bên trái; world unit, không theo scale visual.</summary>
        public Vector2 HitOffset => hitOffset;
    }

    [Serializable]
    public struct EffectEntry
    {
        [Tooltip("ID VFX được Bullet Impact hoặc hệ gameplay yêu cầu. Không dùng None làm entry; mỗi ID xuất hiện một lần.")]
        [SerializeField] private EffectType type; // ID chọn VFX; độc lập hiệu ứng gameplay như slow/cháy/độc.
        [Tooltip("Prefab visual con có IEffectVisual (hiện là ParticleEffectVisual) trên root. CombatVfx hỏi visual mỗi frame và thu hồi khi phát xong.")]
        [SerializeField] private GameObject visualPrefab;

        /// <summary>ID VFX được Bullet Impact hoặc hệ gameplay yêu cầu. Không dùng None làm entry; mỗi ID xuất hiện một lần.</summary>
        public EffectType Type => type;
        /// <summary>Prefab visual con có IEffectVisual (hiện là ParticleEffectVisual) trên root. CombatVfx hỏi visual mỗi frame và thu hồi khi phát xong.</summary>
        public GameObject VisualPrefab => visualPrefab;
    }

    [Tooltip("Vỏ ProjectileView dùng chung cho mọi loại đạn, không chứa SpriteRenderer.")]
    [SerializeField, InspectorName("Projectile View Prefab")] private ProjectileView bulletPrefab;
    [Tooltip("Vỏ EffectView dùng chung; mỗi entry bên dưới chọn một visual riêng.")]
    [SerializeField, InspectorName("Effect View Prefab")] private EffectView effectPrefab;
    [Tooltip("Visual và hitbox cho từng BulletType. Skill giữ tốc độ/sát thương; catalog này chỉ giữ tài nguyên hiển thị và hình học va chạm.")]
    [SerializeField] private BulletVisual[] bullets = Array.Empty<BulletVisual>();
    [Tooltip("Danh sách VFX theo EffectType; prefab phải có IEffectVisual trên root để báo hoàn tất và thu hồi về pool.")]
    [SerializeField] private EffectEntry[] effects = Array.Empty<EffectEntry>();
    [Tooltip("Visual hình tròn đứng yên (vùng trên đất, báo vùng đòn từ trời, vòng sóng). SkillAreaVisual không bật Saw Blade, không có object con.")]
    [SerializeField] private SkillAreaVisual circlePrefab;
    [Tooltip("Visual đường thẳng (sét nảy, tia) có SkillLineVisual.")]
    [SerializeField] private SkillLineVisual linePrefab;
    [Tooltip("Vỏ hình pet (Summon): GameObject có SpriteRenderer; sprite lấy từ data của skill.")]
    [SerializeField] private SpriteRenderer petPrefab;
    [Tooltip("Số VFX (impact) mới tối đa trong một frame; vượt thì bỏ qua (chỉ là hình, gameplay không đổi). 0 = không giới hạn.")]
    [SerializeField, Min(0)] private int maxNewEffectsPerFrame = 8;
    [Tooltip("Số VFX đang phát tối đa cùng lúc; vượt thì bỏ qua cái mới. Nên bằng tổng vỏ EffectView tạo sẵn để không Instantiate giữa trận. 0 = không giới hạn.")]
    [SerializeField, Min(0)] private int maxActiveEffects = 64;

    public ProjectileView BulletPrefab => bulletPrefab;
    /// <summary>Visual hình tròn đứng yên (vùng, báo vùng, vòng sóng).</summary>
    public SkillAreaVisual CirclePrefab => circlePrefab;
    /// <summary>Visual đường thẳng (sét nảy, tia).</summary>
    public SkillLineVisual LinePrefab => linePrefab;
    /// <summary>Vỏ hình pet.</summary>
    public SpriteRenderer PetPrefab => petPrefab;
    public EffectView EffectPrefab => effectPrefab;
    public IReadOnlyList<BulletVisual> BulletEntries => bullets;
    public IReadOnlyList<EffectEntry> EffectEntries => effects;

    /// <summary>Số VFX mới tối đa trong một frame; 0 = không giới hạn.</summary>
    /// <remarks>
    /// Đo trong Editor: 200 impact một lúc tạo frame 260 ms (Instantiate khi vượt pool + render 5–7 lớp mỗi impact).
    /// VFX chỉ là hình nên bỏ bớt khi quá đông không ảnh hưởng gameplay, mà giữ frame mượt.
    /// </remarks>
    public int MaxNewEffectsPerFrame => maxNewEffectsPerFrame;
    /// <summary>Số VFX đang phát tối đa cùng lúc; 0 = không giới hạn.</summary>
    public int MaxActiveEffects => maxActiveEffects;

    public BulletVisual GetBullet(BulletType type)
    {
        foreach (BulletVisual bullet in bullets)
            if (bullet.Type == type) return bullet;
        throw new InvalidOperationException($"Missing bullet visual: {type} in {name}.");
    }

    public EffectEntry GetEffect(EffectType type)
    {
        if (type == EffectType.None) return default;
        foreach (EffectEntry effect in effects)
            if (effect.Type == type) return effect;
        throw new InvalidOperationException($"Missing effect prefab: {type} in {name}.");
    }

    public void Validate()
    {
        if (bulletPrefab == null || effectPrefab == null || bullets == null || effects == null)
            throw new InvalidOperationException($"Assign combat resources: {name}.");
        var effectTypes = new HashSet<EffectType>();
        foreach (EffectEntry effect in effects)
            if (effect.Type == EffectType.None || !Enum.IsDefined(typeof(EffectType), effect.Type) || effect.VisualPrefab == null ||
                effect.VisualPrefab.GetComponent<IEffectVisual>() == null || !effectTypes.Add(effect.Type))
                throw new InvalidOperationException($"Missing or duplicate effect in {name}.");
        var bulletTypes = new HashSet<BulletType>();
        foreach (BulletVisual bullet in bullets)
        {
            if (!Enum.IsDefined(typeof(BulletType), bullet.Type) || !bulletTypes.Add(bullet.Type) ||
                bullet.VisualPrefab == null || !(bullet.HitRadius > 0f) || float.IsInfinity(bullet.HitRadius) ||
                float.IsNaN(bullet.HitOffset.x) || float.IsInfinity(bullet.HitOffset.x) ||
                float.IsNaN(bullet.HitOffset.y) || float.IsInfinity(bullet.HitOffset.y) ||
                (bullet.ImpactEffect != EffectType.None && !effectTypes.Contains(bullet.ImpactEffect)))
                throw new InvalidOperationException($"Invalid bullet visual: {bullet.Type} in {name}.");
        }
    }
}
