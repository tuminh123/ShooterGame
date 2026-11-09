using System;
using System.Collections.Generic;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class ShootingData : SkillBehaviourData
{
    #region Shooting Configuration

    [Min(0f)] public float cooldown = 0.8f; // Khoảng cách giữa hai lượt bắn thành công.
    [Min(0f)] public float range = 2f; // Tầm tìm mục tiêu.
    [SerializeReference] public IShooter shooter = new StraightShooter(); // Cách phát đạn.
    [SerializeReference] public IProjectileMove projectileMove = new StraightProjectileMove(); // Cách bay.
    public BulletType bulletType; // Hình ảnh lấy từ CombatAssetsSO.
    [Min(1)] public int projectileCount = 1; // Số viên mỗi lượt.
    public Vector2 muzzleOffset = new Vector2(0f, 0.02f); // Điểm phát đạn tương đối với owner.
    public bool requiresTarget = true; // Có mục tiêu mới cast.
    public Vector2 defaultDirection = Vector2.right; // Hướng khi không bắt buộc mục tiêu.
    [Min(0.01f)] public float speed = 2.4f; // Tốc độ world unit/giây.
    [Min(0.001f)] public float hitRadius = 0.035f; // Bán kính va chạm đạn.
    [Min(0.01f)] public float lifetime = 3f; // Thời gian tồn tại tối đa.
    [SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>(); // Effect của đạn ở đúng cấp này.
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
            !Positive(speed) || !Positive(hitRadius) || !Positive(lifetime))
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
