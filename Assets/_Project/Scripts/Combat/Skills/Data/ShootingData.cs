using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thông số của <see cref="ShootingSkill"/> tại một cấp: kiểu bắn, quỹ đạo, số viên, tốc độ, effect khi trúng.</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class ShootingData : SkillBehaviourData
{
    #region Shooting Configuration

    [SerializeField][Tooltip("Thời gian tối thiểu giữa hai lần bắn thành công.")][Min(0f)] private float cooldown = 0.8f;
    [SerializeField][Tooltip("% Damage của nhân vật mỗi viên trúng. 100 = 1× Damage (đã gồm level, trang bị, buff).")]
    [Min(0f)] private float damagePercent = 100f;
    [Tooltip("Luật chọn chỗ bắn trong tầm chiến đấu của nhân vật (CharacterDataSO > Combat Range): gần nhất, ngẫu nhiên, chỗ đông quái, hướng nhìn hoặc hướng cố định.")]
    [SerializeReference] private ITargetSelector target = new NearestTarget();
    [Tooltip("Chọn cách tạo và phát projectile.")][SerializeReference] private IShooter shooter = new StraightShooter();
    [Tooltip("Chọn cách projectile di chuyển sau khi được tạo.")][SerializeReference] private IProjectileMove projectileMove = new StraightProjectileMove();
    [Tooltip("Loại visual projectile lấy từ CombatAssetsSO.")]
    [SerializeField] private BulletType bulletType;
    [SerializeField][Tooltip("Số projectile tạo trong một lần bắn.")][Min(1)] private int projectileCount = 1;
    [Tooltip("Độ lệch điểm xuất hiện so với vị trí nhân vật.")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f);
    [Tooltip("Bật: luật chọn không tìm được chỗ bắn thì không bắn (không tiêu cooldown). Tắt: bắn theo Default Direction.")]
    [SerializeField] private bool requiresTarget = true;
    [Tooltip("Hướng dùng khi không bắt buộc có mục tiêu.")]
    [SerializeField] private Vector2 defaultDirection = Vector2.right;
    [SerializeField][Tooltip("Tốc độ projectile theo world unit mỗi giây.")][Min(0.01f)] private float speed = 2.4f;
    [SerializeField][Tooltip("Thời gian tối đa projectile tồn tại trước khi thu hồi.")][Min(0.01f)] private float lifetime = 3f;
    [Tooltip("Các hiệu ứng được áp dụng khi projectile đánh trúng.")][SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [Tooltip("Xuyên, kích thước, xoay hình, tác động khi trúng / khi hết đường bay / theo nhịp.")]
    [SerializeField] private ProjectileTraits traits = new ProjectileTraits();
    [Tooltip("Quãng bay tối đa: Lifetime = chỉ theo thời gian; CombatRange = tới mép tầm chiến đấu (× Range Scale); TargetPoint = đúng tới điểm nhắm.")]
    [SerializeField] private ProjectileRange rangeMode = ProjectileRange.Lifetime;
    [Tooltip("Hệ số nhân tầm chiến đấu khi Range Mode = CombatRange.")][SerializeField, Min(0.05f)] private float rangeScale = 1f;
    [Tooltip("Đẩy lùi mục tiêu khi trúng (world unit); âm = kéo về phía người bắn.")][SerializeField] private float knockback;
    [Tooltip("Bonus chiến đấu riêng của skill này (cộng vào bonus nhân vật).")][SerializeField] private SkillHitBonus hitBonus;
    [Tooltip("> 0: các viên trong một lượt bắn NỐI ĐUÔI, cách nhau số giây này (dao ném). 0 = bắn cùng lúc.")]
    [SerializeField, Min(0f)] private float burstInterval;

    /// <summary>Hệ số sát thương mỗi viên (1 = 100% Damage của nhân vật).</summary>
    public float DamageMultiplier => damagePercent / 100f;
    /// <summary>Luật chọn chỗ bắn; asset cũ chưa có field này dùng <see cref="NearestTarget"/>.</summary>
    public ITargetSelector Target => target;
    /// <summary>Chọn cách tạo và phát projectile.</summary>
    public IShooter Shooter => shooter;
    /// <summary>Chọn cách projectile di chuyển sau khi được tạo.</summary>
    public IProjectileMove ProjectileMove => projectileMove;
    /// <summary>Loại visual projectile lấy từ CombatAssetsSO.</summary>
    public BulletType BulletType => bulletType;
    /// <summary>Số projectile tạo trong một lần bắn.</summary>
    public int ProjectileCount => projectileCount;
    /// <summary>Độ lệch điểm xuất hiện so với vị trí nhân vật.</summary>
    public Vector2 MuzzleOffset => muzzleOffset;
    /// <summary>Bật để chỉ bắn khi tìm được mục tiêu hợp lệ.</summary>
    public bool RequiresTarget => requiresTarget;
    /// <summary>Hướng dùng khi không bắt buộc có mục tiêu.</summary>
    public Vector2 DefaultDirection => defaultDirection;
    /// <summary>Tốc độ projectile theo world unit mỗi giây.</summary>
    public float Speed => speed;
    /// <summary>Thời gian tối đa projectile tồn tại trước khi thu hồi.</summary>
    public float Lifetime => lifetime;
    /// <summary>Các hiệu ứng được áp dụng khi projectile đánh trúng.</summary>
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    /// <summary>Đặc tính thêm của đạn; asset cũ chưa có thì dùng mặc định.</summary>
    public ProjectileTraits Traits => traits ?? ProjectileTraits.Default;
    /// <summary>Cách giới hạn quãng bay.</summary>
    public ProjectileRange RangeMode => rangeMode;
    /// <summary>Hệ số nhân tầm chiến đấu.</summary>
    public float RangeScale => rangeScale;
    /// <summary>Đẩy lùi (dương) / kéo (âm) khi trúng.</summary>
    public float Knockback => knockback;
    /// <summary>Bonus chiến đấu riêng của skill.</summary>
    public SkillHitBonus HitBonus => hitBonus;
    /// <summary>Khoảng cách giữa các viên bắn nối đuôi; 0 = cùng lúc.</summary>
    public float BurstInterval => burstInterval;

    public override SkillBehaviourType Type => SkillBehaviourType.Shooting;
    public override float Cooldown => cooldown;

    #endregion

    #region Validation

    // Không có ShootingLevelTable: mỗi cấp đã chứa trọn cấu hình bắn của nó.
    public override void Validate()
    {
        if (!NonNegative(cooldown) || !NonNegative(damagePercent) || target == null || shooter == null || projectileMove == null ||
            !Enum.IsDefined(typeof(BulletType), bulletType) || projectileCount < 1 || projectileCount > 1024 ||
            !Finite(muzzleOffset.x) || !Finite(muzzleOffset.y) || !Finite(defaultDirection.x) ||
            !Finite(defaultDirection.y) || defaultDirection.sqrMagnitude < 0.000001f ||
            !Positive(speed) || !Positive(lifetime))
            throw new InvalidOperationException("Invalid shooting data.");
        // Không cần whitelist kiểu bắn/quỹ đạo: [SerializeReference] chỉ cho chọn class implement đúng interface.
        target.Validate();
        // Đạn đuổi cần mục tiêu để bay theo; luật chỉ trả hướng (Facing/Fixed) không dùng được.
        if (projectileMove.TracksTarget && !target.ProvidesTarget)
            throw new InvalidOperationException("Homing projectiles need a target selector that returns a target.");
        shooter.Validate();
        projectileMove.Validate();
        ValidateEffects(effects);
        Traits.Validate();
        hitBonus.Validate();
        if (!Enum.IsDefined(typeof(ProjectileRange), rangeMode) || !Positive(rangeScale) || !Finite(knockback) || !NonNegative(burstInterval))
            throw new InvalidOperationException("Invalid shooting range/burst data.");
    }

    // Offset/hướng có thể âm; chỉ loại NaN và vô cực.
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion
}
