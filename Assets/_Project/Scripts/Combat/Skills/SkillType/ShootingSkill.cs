using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShootingSkill : IRunLevelSkillData
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
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>(); // Cấu hình effect; đạn không giữ ShootingSkill.
    [SerializeField] private ShootingLevelTable upgrades = new ShootingLevelTable();

    [NonSerialized] private ShootingLevelLookup levelLookup;
    [NonSerialized] private IShooter selectedShooterTemplate;
    [NonSerialized] private IShooter liveShooter;
    [NonSerialized] private IProjectileMove liveMovement;
    [NonSerialized] private IReadOnlyList<IGameplayEffect> liveEffects;
    [NonSerialized] private int liveCount;

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
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    internal bool UsesHoming => liveMovement is HomingProjectileMove;

    // Mỗi skill đang chạy có shooter riêng; không thay đổi trạng thái của bản lưu trong asset.
    internal ShootingSkill CreateRuntime(ShootBehaviorFactory factory, int maxLevel)
    {
        var runtimeSkill = (ShootingSkill)MemberwiseClone();
        runtimeSkill.selectedShooterTemplate = null;
        runtimeSkill.liveShooter = null;
        runtimeSkill.levelLookup = upgrades.CreateLookup(maxLevel);
        runtimeSkill.ApplyRunLevel(1, factory);
        return runtimeSkill;
    }

    internal void ApplyRunLevel(int level, ShootBehaviorFactory factory)
    {
        if (levelLookup == null) throw new InvalidOperationException("Create a shooting runtime before applying levels.");
        if (level == 1) selectedShooterTemplate = null; // Trận mới bắt đầu lại góc Spiral, kể cả đang ở cấp 1.
        var selected = levelLookup.ResolveShooting(level, shooter, projectileMove, projectileCount);
        // Chỉ clone khi đổi TEMPLATE, không mỗi lần bắn hoặc khi chỉ tăng số viên.
        // Vì thế góc Spiral giữ nguyên khi tăng cấp không thay shooter.
        if (!ReferenceEquals(selectedShooterTemplate, selected.Shooter))
        {
            liveShooter = factory.CreateRuntime(selected.Shooter);
            selectedShooterTemplate = selected.Shooter;
        }
        liveMovement = selected.Movement;
        liveCount = selected.Count;
        liveEffects = levelLookup.ResolveEffects(level, effects);
    }

    public void ValidateLevels(int maxLevel) => upgrades.Validate(maxLevel);

    // Thực hiện trọn một lượt bắn; bên gọi đã chuẩn bị vị trí, hướng và phe.
    public int ExecuteSkill(CombatSpawner spawner, ShootBehaviorFactory shootFactory,
        ProjectileMoveFactory moveFactory, CharacterRuntimeData source, Vector2 spawnPosition,
        Vector2 aimDirection, CharacterTarget target = default)
    {
        if (spawner == null) throw new ArgumentNullException(nameof(spawner));
        if (shootFactory == null) throw new ArgumentNullException(nameof(shootFactory));
        if (moveFactory == null) throw new ArgumentNullException(nameof(moveFactory));
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (levelLookup == null) throw new InvalidOperationException("Execute the runtime copy, not the shared skill asset.");

        IProjectileMove movement = moveFactory.Create(liveMovement);
        // Snapshot stats for this cast. Each projectile rolls its own critical hit.
        BaseStats shotStats = source.FinalStats;
        CharacterTeam sourceTeam = source.Team;
        int count = liveCount;
        IShooter shotShooter = liveShooter;
        IReadOnlyList<IGameplayEffect> shotEffects = liveEffects;
        
        int spawnedBulletCount = 0;
        for (int projectileIndex = 0; projectileIndex < count; projectileIndex++)
        {
            Vector2 bulletDirection = shootFactory.ShootingHandle(shotShooter, aimDirection.normalized, projectileIndex, count);
            var projectileData = new ProjectileSpawnData(bulletType,
                CharacterRuntimeData.CreateDamage(shotStats, sourceTeam), speed, lifetime, hitRadius,
                movement, shotEffects, shotStats.effectHit);
            if (spawner.SpawnBullet(projectileData, spawnPosition, bulletDirection, target))
                spawnedBulletCount++;
        }

        // Chỉ thông báo kết quả; kiểu bắn tự cập nhật trạng thái riêng nếu cần.
        if (spawnedBulletCount > 0) shotShooter.OnShotCompleted();
        return spawnedBulletCount;
    }

    public void Validate()
    {
        if (shooter == null || projectileMove == null ||
            !Enum.IsDefined(typeof(BulletType), bulletType) || projectileCount < 1 || projectileCount > 1024 ||
            !IsFiniteNumber(muzzleOffset.x) || !IsFiniteNumber(muzzleOffset.y) ||
            !IsFiniteNumber(defaultDirection.x) || !IsFiniteNumber(defaultDirection.y) || defaultDirection.sqrMagnitude < 0.000001f ||
            !IsPositiveFiniteNumber(speed) || !IsPositiveFiniteNumber(hitRadius) || !IsPositiveFiniteNumber(lifetime))
            throw new InvalidOperationException("Invalid shooting skill configuration.");
        shooter.Validate();
        ShootBehaviorFactory.ValidateSupported(shooter);
        ProjectileMoveFactory.ValidateSupported(projectileMove);
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
