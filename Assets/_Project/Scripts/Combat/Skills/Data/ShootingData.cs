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
    [SerializeField][Tooltip("Khoảng cách tối đa để tìm mục tiêu.")][Min(0f)] private float range = 2f;
    [Tooltip("Chọn cách tạo và phát projectile.")][SerializeReference] private IShooter shooter = new StraightShooter();
    [Tooltip("Chọn cách projectile di chuyển sau khi được tạo.")][SerializeReference] private IProjectileMove projectileMove = new StraightProjectileMove();
    [Tooltip("Loại visual projectile lấy từ CombatAssetsSO.")]
    [SerializeField] private BulletType bulletType;
    [SerializeField][Tooltip("Số projectile tạo trong một lần bắn.")][Min(1)] private int projectileCount = 1;
    [Tooltip("Độ lệch điểm xuất hiện so với vị trí nhân vật.")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f);
    [Tooltip("Bật để chỉ bắn khi tìm được mục tiêu hợp lệ.")]
    [SerializeField] private bool requiresTarget = true;
    [Tooltip("Hướng dùng khi không bắt buộc có mục tiêu.")]
    [SerializeField] private Vector2 defaultDirection = Vector2.right;
    [SerializeField][Tooltip("Tốc độ projectile theo world unit mỗi giây.")][Min(0.01f)] private float speed = 2.4f;
    [SerializeField][Tooltip("Thời gian tối đa projectile tồn tại trước khi thu hồi.")][Min(0.01f)] private float lifetime = 3f;
    [Tooltip("Các hiệu ứng được áp dụng khi projectile đánh trúng.")][SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();

    /// <summary>Khoảng cách tối đa để tìm mục tiêu.</summary>
    public float Range => range;
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

    public override SkillBehaviourType Type => SkillBehaviourType.Shooting;
    public override float Cooldown => cooldown;

    #endregion

    #region Validation

    // Không có ShootingLevelTable: mỗi cấp đã chứa trọn cấu hình bắn của nó.
    public override void Validate()
    {
        if (!NonNegative(cooldown) || !NonNegative(range) || shooter == null || projectileMove == null ||
            !Enum.IsDefined(typeof(BulletType), bulletType) || projectileCount < 1 || projectileCount > 1024 ||
            !Finite(muzzleOffset.x) || !Finite(muzzleOffset.y) || !Finite(defaultDirection.x) ||
            !Finite(defaultDirection.y) || defaultDirection.sqrMagnitude < 0.000001f ||
            !Positive(speed) || !Positive(lifetime))
            throw new InvalidOperationException("Invalid shooting data.");
        // Không cần whitelist kiểu bắn/quỹ đạo: [SerializeReference] chỉ cho chọn class implement đúng interface.
        shooter.Validate();
        ValidateEffects(effects);
    }

    // Offset/hướng có thể âm; chỉ loại NaN và vô cực.
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion
}
