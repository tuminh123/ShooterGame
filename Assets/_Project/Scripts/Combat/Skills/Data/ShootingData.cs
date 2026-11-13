using System;
using System.Collections.Generic;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class ShootingData : SkillBehaviourData
{
    #region Shooting Configuration

    [Tooltip("Thời gian tối thiểu giữa hai lần bắn thành công.")][Min(0f)] public float cooldown = 0.8f;
    [Tooltip("Khoảng cách tối đa để tìm mục tiêu.")][Min(0f)] public float range = 2f;
    [Tooltip("Chọn cách tạo và phát projectile.")][SerializeReference] public IShooter shooter = new StraightShooter();
    [Tooltip("Chọn cách projectile di chuyển sau khi được tạo.")][SerializeReference] public IProjectileMove projectileMove = new StraightProjectileMove();
    [Tooltip("Loại visual projectile lấy từ CombatAssetsSO.")]
    public BulletType bulletType;
    [Tooltip("Số projectile tạo trong một lần bắn.")][Min(1)] public int projectileCount = 1;
    [Tooltip("Độ lệch điểm xuất hiện so với vị trí nhân vật.")]
    public Vector2 muzzleOffset = new Vector2(0f, 0.02f);
    [Tooltip("Bật để chỉ bắn khi tìm được mục tiêu hợp lệ.")]
    public bool requiresTarget = true;
    [Tooltip("Hướng dùng khi không bắt buộc có mục tiêu.")]
    public Vector2 defaultDirection = Vector2.right;
    [Tooltip("Tốc độ projectile theo world unit mỗi giây.")][Min(0.01f)] public float speed = 2.4f;
    [Tooltip("Thời gian tối đa projectile tồn tại trước khi thu hồi.")][Min(0.01f)] public float lifetime = 3f;
    [Tooltip("Các hiệu ứng được áp dụng khi projectile đánh trúng.")][SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>();
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
        shooter.Validate();
        ShootBehaviorFactory.ValidateSupported(shooter);
        ProjectileMoveFactory.ValidateSupported(projectileMove);
        ValidateEffects(effects);
    }

    // Offset/hướng có thể âm; chỉ loại NaN và vô cực.
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion
}
