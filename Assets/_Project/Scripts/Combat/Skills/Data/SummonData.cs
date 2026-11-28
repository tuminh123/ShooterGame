using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thông số của <see cref="SummonSkill"/>: pet không có HP, không bị quái nhắm (sói, drone, trụ, thiên thần).
/// Pet tìm mục tiêu trong tầm chiến đấu của CHỦ (tính từ vị trí pet) và đánh bằng chỉ số của chủ.
/// </summary>
/// <remarks>
/// Kiểu di chuyển và kiểu đánh chọn bằng enum (<see cref="PetMovement"/>, <see cref="PetAttack"/>) và một switch trong skill:
/// mỗi kiểu chỉ vài dòng, tách strategy riêng không làm dễ đọc hơn. Không đổi tên class hoặc field: Unity lưu chúng trong asset.
/// </remarks>
[Serializable]
public sealed class SummonData : SkillBehaviourData
{
    #region Configuration

    [Tooltip("0 = pet thường trực (gọi ra một lần, đi theo cả trận). > 0 = mỗi lần hết hồi chiêu đặt pet mới (trụ).")]
    [SerializeField, Min(0f)] private float cooldown;
    [SerializeField] private Sprite sprite;
    [Tooltip("Kích thước hình pet (world unit theo chiều cao sprite).")][SerializeField, Min(0.02f)] private float spriteSize = 0.18f;
    [SerializeField, Range(1, 8)] private int count = 1;
    [Tooltip("Thời gian sống của mỗi pet (giây); 0 = thường trực.")][SerializeField, Min(0f)] private float lifetime;
    [SerializeField] private PetMovement movement = PetMovement.Follow;
    [Tooltip("Bán kính bay quanh chủ (Follow/Lunge lúc nghỉ).")][SerializeField, Min(0.05f)] private float orbitRadius = 0.22f;
    [Tooltip("Tốc độ di chuyển của pet (world unit/giây).")][SerializeField, Min(0.1f)] private float moveSpeed = 2f;
    [SerializeField] private PetAttack attack = PetAttack.Shoot;
    [Tooltip("Giây giữa hai đòn của mỗi pet.")][SerializeField, Min(0.05f)] private float attackInterval = 0.5f;
    [Tooltip("% Damage của chủ mỗi đòn.")][SerializeField, Min(0f)] private float damagePercent = 40f;
    [Tooltip("Tầm tìm mục tiêu = tầm chiến đấu của chủ × hệ số này, tính từ vị trí pet.")][SerializeField, Min(0.1f)] private float rangeScale = 1f;
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [Header("Shoot")]
    [SerializeField] private BulletType bulletType = BulletType.Lightning;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 2.6f;
    [SerializeField, Range(1, 8)] private int projectileCount = 1;
    [SerializeField, Range(0f, 90f)] private float spreadAngle = 12f;
    [Tooltip("Đạn đuổi mục tiêu (thiên thần) thay vì bay thẳng.")][SerializeField] private bool homing;
    [Header("Chain")]
    [SerializeField, Range(0, 16)] private int chainJumps = 2;
    [SerializeField, Min(0.05f)] private float chainRange = 0.5f;
    [Header("Missile (đòn phụ theo nhịp riêng)")]
    [Tooltip("Giây giữa hai tên lửa; 0 = không có.")][SerializeField, Min(0f)] private float missileInterval;
    [SerializeField, Min(0f)] private float missileDamagePercent = 150f;
    [Tooltip("Đặc tính tên lửa (nổ khi trúng…).")][SerializeField] private ProjectileTraits missileTraits = new ProjectileTraits();

    public override SkillBehaviourType Type => SkillBehaviourType.Summon;
    public override float Cooldown => cooldown;
    public Sprite Sprite => sprite;
    public float SpriteSize => spriteSize;
    public int Count => count;
    public float Lifetime => lifetime;
    public PetMovement Movement => movement;
    public float OrbitRadius => orbitRadius;
    public float MoveSpeed => moveSpeed;
    public PetAttack Attack => attack;
    public float AttackInterval => attackInterval;
    public float DamageMultiplier => damagePercent / 100f;
    public float RangeScale => rangeScale;
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    public BulletType BulletType => bulletType;
    public float ProjectileSpeed => projectileSpeed;
    public int ProjectileCount => projectileCount;
    public float SpreadAngle => spreadAngle;
    public bool Homing => homing;
    public int ChainJumps => chainJumps;
    public float ChainRange => chainRange;
    public float MissileInterval => missileInterval;
    public float MissileDamageMultiplier => missileDamagePercent / 100f;
    public ProjectileTraits MissileTraits => missileTraits ?? ProjectileTraits.Default;

    /// <summary>Pet thường trực: gọi ra một lần, không theo hồi chiêu.</summary>
    public bool Permanent => cooldown <= 0f;

    #endregion

    #region Validation

    public override void Validate()
    {
        if (!NonNegative(cooldown) || !Positive(spriteSize) || count < 1 || !NonNegative(lifetime) || !Positive(orbitRadius) ||
            !Positive(moveSpeed) || !Positive(attackInterval) || !NonNegative(damagePercent) || !Positive(rangeScale) ||
            !Positive(projectileSpeed) || !Positive(chainRange) || !NonNegative(missileInterval) || !NonNegative(missileDamagePercent) ||
            !Enum.IsDefined(typeof(PetMovement), movement) || !Enum.IsDefined(typeof(PetAttack), attack) ||
            !Enum.IsDefined(typeof(BulletType), bulletType) || (cooldown > 0f && lifetime <= 0f))
            throw new InvalidOperationException("Invalid summon data.");
        ValidateEffects(effects);
        MissileTraits.Validate();
    }

    #endregion
}
