using System;
using System.Collections.Generic;

/// <summary>
/// Thông tin của bên đánh cho một đòn, chốt lúc ra đòn (lúc bắn, lúc lưỡi cưa quét…) nhưng <b>chưa</b> tính ra con số.
/// <see cref="CombatHits.Apply"/> tính sát thương lúc trúng, khi đã biết trúng ai.
/// </summary>
/// <remarks>
/// Lý do không chốt <see cref="DamageInfo"/> ngay lúc bắn: luật phụ thuộc mục tiêu (thêm sát thương lên boss,
/// kết liễu theo HP còn lại…) chỉ áp được khi biết mục tiêu. Chỉ số vẫn chụp lúc ra đòn, nên một loạt đạn
/// không trộn hai mức chỉ số khi buff đổi giữa chừng.
/// <para>Struct readonly, truyền bằng <c>in</c>: không cấp phát mỗi viên đạn.</para>
/// </remarks>
public readonly struct HitPayload
{
    #region Properties

    /// <summary>Lượt spawn của bên đánh (để hút máu…). Có thể rỗng khi đòn không có chủ (ví dụ kiểm tra tự động).</summary>
    public CharacterHandle Attacker { get; }

    /// <summary>Chỉ số cuối của bên đánh tại lúc ra đòn.</summary>
    public BaseStats Stats { get; }

    /// <summary>Bonus chiến đấu của bên đánh lúc ra đòn (boss, kết liễu, hút máu).</summary>
    public CombatBonuses Bonuses { get; }

    /// <summary>Phe của bên đánh: bên nhận bỏ qua đòn cùng phe, đạn dùng để chọn phe đối địch khi quét.</summary>
    public CharacterTeam Team { get; }

    /// <summary>Hệ số sát thương của skill (1 = 100% Damage của bên đánh).</summary>
    public float DamageMultiplier { get; }

    /// <summary>Effect áp lên mục tiêu khi đòn được nhận. Cấu hình dùng chung, chỉ đọc; không bao giờ null.</summary>
    public IReadOnlyList<IGameplayEffect> Effects { get; }

    /// <summary>Quãng đẩy lùi mục tiêu khi trúng (world unit); âm = kéo về phía người đánh; 0 = không.</summary>
    public float Knockback { get; }

    /// <summary>Chỉ số chính xác hiệu ứng của bên đánh lúc ra đòn.</summary>
    public float EffectHit => Stats.effectHit;

    #endregion

    #region Constructor

    /// <summary>Tạo gói đòn; <paramref name="effects"/> null được coi là không có effect.</summary>
    public HitPayload(CharacterHandle attacker, BaseStats stats, CombatBonuses bonuses, CharacterTeam team,
        float damageMultiplier, IReadOnlyList<IGameplayEffect> effects, float knockback = 0f)
    {
        Knockback = knockback;
        Attacker = attacker;
        Stats = stats;
        Bonuses = bonuses;
        Team = team;
        DamageMultiplier = damageMultiplier;
        Effects = effects ?? Array.Empty<IGameplayEffect>();
    }

    #endregion

    #region Variants

    /// <summary>
    /// Cùng bên đánh và chỉ số đã chụp, nhưng đổi % sát thương, effect và đẩy lùi (nổ, đạn tách, vùng, pet…).
    /// Struct nên không cấp phát.
    /// </summary>
    public HitPayload With(float damageMultiplier, IReadOnlyList<IGameplayEffect> effects, float knockback)
        => new HitPayload(Attacker, Stats, Bonuses, Team, damageMultiplier, effects, knockback);

    #endregion

    #region Validation

    /// <summary>Chặn gói đòn sai trước khi đạn được tạo. Ném <see cref="ArgumentException"/> nếu sai.</summary>
    public void Validate()
    {
        // default(HitPayload) bỏ qua constructor nên Effects null.
        if (Effects == null) throw new ArgumentException("Initialize the hit payload before use.");
        if (float.IsNaN(Knockback) || float.IsInfinity(Knockback)) throw new ArgumentException("Knockback must be finite.");
        if (!(DamageMultiplier >= 0f) || float.IsInfinity(DamageMultiplier))
            throw new ArgumentException("Damage multiplier must be finite and not negative.");
        if (!(Stats.damage >= 0f) || float.IsInfinity(Stats.damage) || float.IsNaN(Stats.effectHit) || float.IsInfinity(Stats.effectHit))
            throw new ArgumentException("Attacker stats must be finite.");
        // Duyệt theo index để không boxing enumerator khi đi qua IReadOnlyList.
        for (int index = 0; index < Effects.Count; index++)
        {
            IGameplayEffect effect = Effects[index];
            if (effect == null) throw new ArgumentException("Hit effects cannot contain null entries.");
            effect.Validate();
        }
    }

    #endregion
}
