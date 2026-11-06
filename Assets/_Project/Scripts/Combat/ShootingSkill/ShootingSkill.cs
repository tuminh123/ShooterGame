using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShootingSkill : ISkillData
{
    [Header("Shooting")]
    [SerializeReference] private IShooter shooter = new StraightShooter(); // Kiểu bắn và thông số riêng của nó.
    [SerializeField] private BulletType bulletType; // Chọn hình ảnh viên đạn trong CombatAssetsSO.
    [SerializeField, Min(1)] private int projectileCount = 1; // Số viên tạo trong một lượt bắn.
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f); // Độ lệch điểm bắn so với tâm nhân vật.
    [SerializeField] private bool requiresTarget = true; // Cần tìm được đối thủ mới thực hiện lượt bắn.
    [SerializeField] private Vector2 defaultDirection = Vector2.right; // Hướng dùng khi không cần mục tiêu.

    [SerializeReference] private IProjectileMove projectileMove = new StraightProjectileMove(); // Cách cập nhật hướng bay.

    [Header("Projectile")]
    [SerializeField, Min(0.01f)] private float speed = 2.4f; // Tốc độ world unit/giây, gửi cho MovementManager.
    [SerializeField, Min(0.001f)] private float hitRadius = 0.035f; // Bán kính kiểm tra trúng quái.
    [SerializeField, Min(0.01f)] private float lifetime = 3f; // Số giây tối đa trước khi thu hồi đạn.
    [SerializeField, Min(0f)] private float damage = 10f; // Sát thương mỗi viên, chưa tính effect.
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>(); // Cấu hình effect; đạn không giữ ShootingSkill.

    public IShooter Shooter => shooter;
    public IProjectileMove ProjectileMove => projectileMove;
    public BulletType BulletType => bulletType;
    public int ProjectileCount => projectileCount;
    public Vector2 MuzzleOffset => muzzleOffset;
    public bool RequiresTarget => requiresTarget;
    public Vector2 DefaultDirection => defaultDirection;
    public float Speed => speed;
    public float HitRadius => hitRadius;
    public float Lifetime => lifetime;
    public float Damage => damage;
    public IReadOnlyList<IGameplayEffect> Effects => effects;

    // Mỗi skill đang chạy có shooter riêng; không thay đổi trạng thái của bản lưu trong asset.
    internal ShootingSkill CreateRuntime()
    {
        var runtimeSkill = (ShootingSkill)MemberwiseClone();
        runtimeSkill.shooter = shooter.CreateRuntime();
        return runtimeSkill;
    }

    // Thực hiện trọn một lượt bắn; bên gọi đã chuẩn bị vị trí, hướng và phe.
    public int ExecuteSkill(CombatSpawner spawner, ShootBehaviorFactory shootFactory,
        ProjectileMoveFactory moveFactory, CharacterTeam sourceTeam, Vector2 spawnPosition,
        Vector2 aimDirection, CharacterTarget target = default, float effectHit = 0f)
    {
        if (spawner == null) throw new ArgumentNullException(nameof(spawner));
        if (shootFactory == null) throw new ArgumentNullException(nameof(shootFactory));
        if (moveFactory == null) throw new ArgumentNullException(nameof(moveFactory));
        Validate();

        IProjectileMove movement = moveFactory.Create(projectileMove);
        var projectileData = new ProjectileSpawnData(bulletType, new DamageInfo(damage, sourceTeam), speed, lifetime, hitRadius, movement, effects, effectHit);
        
        int spawnedBulletCount = 0;
        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            Vector2 bulletDirection = shootFactory.ShootingHandle(shooter, aimDirection.normalized, projectileIndex, projectileCount);
            if (spawner.SpawnBullet(projectileData, spawnPosition, bulletDirection, target))
                spawnedBulletCount++;
        }

        // Chỉ thông báo kết quả; kiểu bắn tự cập nhật trạng thái riêng nếu cần.
        if (spawnedBulletCount > 0) shooter.OnShotCompleted();
        return spawnedBulletCount;
    }

    public void Validate()
    {
        if (shooter == null || projectileMove == null ||
            !Enum.IsDefined(typeof(BulletType), bulletType) || projectileCount < 1 || projectileCount > 1024 ||
            !IsFiniteNumber(muzzleOffset.x) || !IsFiniteNumber(muzzleOffset.y) ||
            !IsFiniteNumber(defaultDirection.x) || !IsFiniteNumber(defaultDirection.y) || defaultDirection.sqrMagnitude < 0.000001f ||
            !IsPositiveFiniteNumber(speed) || !IsPositiveFiniteNumber(hitRadius) || !IsPositiveFiniteNumber(lifetime) ||
            !IsFiniteNumber(damage) || damage < 0f)
            throw new InvalidOperationException("Invalid shooting skill configuration.");
        shooter.Validate();
        if (effects == null) throw new InvalidOperationException("Effect list cannot be null.");
        foreach (IGameplayEffect effect in effects)
        {
            if (effect == null) throw new InvalidOperationException("Select a type for every effect entry.");
            effect.Validate();
        }
    }

    // Góc/offset có thể âm hoặc bằng 0; chỉ loại NaN và vô cực.
    private static bool IsFiniteNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    // Tốc độ, bán kính và tuổi thọ phải hữu hạn và lớn hơn 0.
    private static bool IsPositiveFiniteNumber(float value) => value > 0f && IsFiniteNumber(value);
}
