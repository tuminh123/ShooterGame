using System;

/// <summary>
/// Thông số của một viên đạn, chốt tại thời điểm bắn. Skill, AI hay bất kỳ nguồn nào cũng có thể
/// tạo struct này để bắn qua <see cref="ProjectileSystem.SpawnBullet"/>.
/// </summary>
public readonly struct ProjectileSpawnData
{
    #region Properties

    /// <summary>Loại hình ảnh đạn, tra trong <see cref="CombatAssetsSO"/>.</summary>
    public BulletType BulletType { get; }

    /// <summary>Thông tin đòn của bên bắn (chỉ số, phe, % sát thương, effect); sát thương tính lúc trúng.</summary>
    public HitPayload Hit { get; }

    /// <summary>Tốc độ bay, world unit mỗi giây.</summary>
    public float Speed { get; }

    /// <summary>Thời gian bay tối đa trước khi tự thu hồi (giây).</summary>
    public float Lifetime { get; }

    /// <summary>Strategy quyết định hướng bay mỗi frame.</summary>
    public IProjectileMove Movement { get; }

    /// <summary>Xuyên, kích thước, tác động phụ. Không bao giờ null (mặc định <see cref="ProjectileTraits.Default"/>).</summary>
    public ProjectileTraits Traits { get; }

    /// <summary>Quãng bay tối đa của lượt đi (world unit); vô cực = chỉ giới hạn bằng thời gian.</summary>
    public float MaxDistance { get; }

    /// <summary>Bộ tìm mục tiêu cho đạn xuyên/nảy và tác động phụ; null với đạn thường (quét theo lô).</summary>
    public ICharacterSearch Search { get; }

    #endregion

    #region Constructor

    public ProjectileSpawnData(BulletType bulletType, in HitPayload hit, float speed, float lifetime, IProjectileMove movement,
        ProjectileTraits traits = null, float maxDistance = float.PositiveInfinity, ICharacterSearch search = null)
    {
        BulletType = bulletType;
        Hit = hit;
        Speed = speed;
        Lifetime = lifetime;
        Movement = movement;
        Traits = traits ?? ProjectileTraits.Default;
        MaxDistance = maxDistance;
        Search = search;
    }

    #endregion

    #region Validation

    /// <summary>Chặn dữ liệu sai từ mọi nguồn bắn trước khi đạn được tạo. Ném <see cref="ArgumentException"/> nếu sai.</summary>
    public void Validate()
    {
        if (!IsDefined(BulletType) || Movement == null || !IsPositiveFinite(Speed) || !IsPositiveFinite(Lifetime) || !(MaxDistance > 0f))
            throw new ArgumentException("Invalid projectile spawn data.");
        Hit.Validate();
    }

    // NaN so sánh luôn false nên tự bị loại bởi "> 0".
    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

    // Enum.IsDefined nhận object nên boxing giá trị enum: đo được 2 lần cấp phát (40 B) mỗi viên đạn.
    // So với mảng giá trị lấy một lần thì không cấp phát, và giá trị mới thêm vào enum vẫn tự được nhận.
    private static readonly BulletType[] DefinedBulletTypes = (BulletType[])Enum.GetValues(typeof(BulletType));

    private static bool IsDefined(BulletType type)
    {
        foreach (BulletType defined in DefinedBulletTypes)
            if (defined == type) return true;
        return false;
    }

    #endregion
}
