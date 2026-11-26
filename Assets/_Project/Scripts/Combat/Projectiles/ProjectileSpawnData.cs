using System;
using System.Collections.Generic;

/// <summary>
/// Thông số của một viên đạn, chốt tại thời điểm bắn. Skill, AI hay bất kỳ nguồn nào cũng có thể
/// tạo struct này để bắn qua <see cref="ProjectileSystem.SpawnBullet"/>.
/// </summary>
public readonly struct ProjectileSpawnData
{
    #region Properties

    /// <summary>Loại hình ảnh đạn, tra trong <see cref="CombatAssetsSO"/>.</summary>
    public BulletType BulletType { get; }

    /// <summary>Sát thương và phe của bên bắn, đã tính sẵn chí mạng.</summary>
    public DamageInfo Damage { get; }

    /// <summary>Tốc độ bay, world unit mỗi giây.</summary>
    public float Speed { get; }

    /// <summary>Thời gian bay tối đa trước khi tự thu hồi (giây).</summary>
    public float Lifetime { get; }

    /// <summary>Strategy quyết định hướng bay mỗi frame.</summary>
    public IProjectileMove Movement { get; }

    /// <summary>Effect áp khi trúng. Cấu hình dùng chung, chỉ đọc; không bao giờ null.</summary>
    public IReadOnlyList<IGameplayEffect> Effects { get; }

    /// <summary>Chỉ số chính xác hiệu ứng của bên bắn, chốt lúc bắn.</summary>
    public float EffectHit { get; }

    #endregion

    #region Constructor

    public ProjectileSpawnData(
        BulletType bulletType,
        DamageInfo damage,
        float speed,
        float lifetime,
        IProjectileMove movement,
        IReadOnlyList<IGameplayEffect> effects = null,
        float effectHit = 0f)
    {
        BulletType = bulletType;
        Damage = damage;
        Speed = speed;
        Lifetime = lifetime;
        Movement = movement;
        Effects = effects ?? Array.Empty<IGameplayEffect>();
        EffectHit = effectHit;
    }

    #endregion

    #region Validation

    /// <summary>Chặn dữ liệu sai từ mọi nguồn bắn trước khi đạn được tạo. Ném <see cref="ArgumentException"/> nếu sai.</summary>
    public void Validate()
    {
        if (!IsDefined(BulletType) || Movement == null ||
            !IsPositiveFinite(Speed) || !IsPositiveFinite(Lifetime) ||
            float.IsNaN(EffectHit) || float.IsInfinity(EffectHit))
            throw new ArgumentException("Invalid projectile spawn data.");
        // default(ProjectileSpawnData) có Effects null vì bỏ qua constructor.
        if (Effects == null) throw new ArgumentException("Initialize projectile spawn data before use.");

        // Duyệt theo index để không boxing enumerator khi đi qua IReadOnlyList.
        for (int index = 0; index < Effects.Count; index++)
        {
            IGameplayEffect effect = Effects[index];
            if (effect == null) throw new ArgumentException("Projectile effects cannot contain null entries.");
            effect.Validate();
        }
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
