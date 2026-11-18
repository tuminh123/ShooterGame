/// <summary>
/// Các dịch vụ mà một viên đạn cần trong suốt vòng đời của nó. Mọi viên đạn dùng chung một instance.
/// </summary>
/// <remarks>
/// Trước đây <see cref="ProjectileView"/> tự inject 6 hệ thống qua Zenject khi pool tạo vỏ. Như vậy các
/// hệ thống nội bộ của Combat phải được bind công khai vào container. Giờ <see cref="ProjectileSystem"/>
/// truyền context này vào <see cref="ProjectileView.Initialize"/>, nên đạn không cần biết tới Zenject,
/// và Combat chỉ cần bind <see cref="CombatManager"/>.
/// </remarks>
internal sealed class ProjectileContext
{
    #region Simulation (miền khác, dùng qua interface)

    /// <summary>Đăng ký Transform của đạn vào job di chuyển.</summary>
    public IMovementRegistration MovementRegistration { get; }

    /// <summary>Đặt hướng và tốc độ cho đạn sau mỗi lần quét va chạm.</summary>
    public IMovementCommand MovementCommand { get; }

    /// <summary>Tạo query quét va chạm; SpatialSystem gom mọi viên vào cùng một job.</summary>
    public ISpatialQueryBuilder QueryBuilder { get; }

    /// <summary>Thuê và trả visual con của đạn.</summary>
    public IGameObjectPool Pools { get; }

    #endregion

    #region Combat (cùng miền, dùng class trực tiếp)

    /// <summary>Đăng ký đạn để cập nhật chung một vòng, và trả vỏ đạn về pool (kèm thu hồi visual con trước).</summary>
    public ProjectileSystem Projectiles { get; }

    /// <summary>Áp effect gameplay của đạn lên mục tiêu khi trúng.</summary>
    public EffectSystem Effects { get; }

    /// <summary>Phát VFX trúng đòn.</summary>
    public CombatVfx Vfx { get; }

    #endregion

    #region Constructor

    public ProjectileContext(
        IMovementRegistration movementRegistration,
        IMovementCommand movementCommand,
        ISpatialQueryBuilder queryBuilder,
        IGameObjectPool pools,
        ProjectileSystem projectiles,
        EffectSystem effects,
        CombatVfx vfx)
    {
        MovementRegistration = movementRegistration;
        MovementCommand = movementCommand;
        QueryBuilder = queryBuilder;
        Pools = pools;
        Projectiles = projectiles;
        Effects = effects;
        Vfx = vfx;
    }

    #endregion
}
