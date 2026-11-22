using System;
using UnityEngine;
using UnityEngine.Serialization;

// Additive values only. Percentage bonuses are percentage points (5 = +5%).
[Serializable]
public struct StatBonus
{
    [Tooltip("Lượng HP tối đa cộng thêm; 10 = thêm 10 HP. Có thể âm để giảm chỉ số.")]
    public float maxHealth;
    [Tooltip("Sát thương cộng thẳng vào chỉ số Damage; không phải hệ số nhân.")]
    public float damage;
    [Tooltip("Lượng giáp hấp thụ cộng thêm; không phải % giảm sát thương.")]
    public float defense;
    [Tooltip("Tốc độ cộng thêm theo world unit/giây; 0.5 = tăng 0.5 đơn vị/giây.")]
    public float moveSpeed;
    [Tooltip("Điểm phần trăm chí mạng cộng thêm: 5 = cộng 5 điểm %, không nhân tỷ lệ hiện có.")]
    public float critChance;
    [Tooltip("Điểm phần trăm sát thương chí mạng cộng thêm: 20 = cộng thêm 20 điểm % vào phần damage tăng thêm.")]
    public float critDamageMultiplier;
    [Tooltip("Điểm phần trăm kháng hiệu ứng cộng thêm; không ảnh hưởng né sát thương trực tiếp.")]
    [FormerlySerializedAs("dodge")] public float effectResistance;
    [Tooltip("Điểm phần trăm chính xác hiệu ứng cộng thêm; không ảnh hưởng hitbox hay đường bay đạn.")]
    [FormerlySerializedAs("accuracy")] public float effectHit;

    public void Validate()
    {
        if (!Finite(maxHealth) || !Finite(damage) || !Finite(defense) || !Finite(moveSpeed) ||
            !Finite(critChance) || !Finite(critDamageMultiplier) ||
            !Finite(effectResistance) || !Finite(effectHit))
            throw new ArgumentException("Stat bonuses must be finite.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static StatBonus operator +(StatBonus left, StatBonus right) => new StatBonus
    {
        maxHealth = left.maxHealth + right.maxHealth,
        damage = left.damage + right.damage,
        defense = left.defense + right.defense,
        moveSpeed = left.moveSpeed + right.moveSpeed,
        critChance = left.critChance + right.critChance,
        critDamageMultiplier = left.critDamageMultiplier + right.critDamageMultiplier,
        effectResistance = left.effectResistance + right.effectResistance,
        effectHit = left.effectHit + right.effectHit
    };

    public static StatBonus operator *(StatBonus bonus, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return new StatBonus
        {
            maxHealth = bonus.maxHealth * count, damage = bonus.damage * count,
            defense = bonus.defense * count, moveSpeed = bonus.moveSpeed * count,
            critChance = bonus.critChance * count,
            critDamageMultiplier = bonus.critDamageMultiplier * count,
            effectResistance = bonus.effectResistance * count, effectHit = bonus.effectHit * count
        };
    }

    /// <summary>Bonus chỉ có một chỉ số (dùng khi cộng dồn chỉ số chính/phụ của trang bị theo <see cref="StatType"/>).</summary>
    public static StatBonus Of(StatType stat, float value)
    {
        switch (stat)
        {
            case StatType.MaxHealth: return new StatBonus { maxHealth = value };
            case StatType.Damage: return new StatBonus { damage = value };
            case StatType.Defense: return new StatBonus { defense = value };
            case StatType.MoveSpeed: return new StatBonus { moveSpeed = value };
            case StatType.CritChance: return new StatBonus { critChance = value };
            case StatType.CritDamageMultiplier: return new StatBonus { critDamageMultiplier = value };
            case StatType.EffectResistance: return new StatBonus { effectResistance = value };
            case StatType.EffectHit: return new StatBonus { effectHit = value };
            default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown stat type.");
        }
    }

    /// <summary>
    /// Chuyển thành một nguồn modifier cho <see cref="StatSheet"/>: mỗi chỉ số là một modifier <c>Add</c>.
    /// Công thức không đổi, vì bonus cộng thẳng và modifier Add đều được cộng trước khi nhân phần trăm.
    /// </summary>
    public StatModifier[] ToModifiers()
    {
        Validate();
        return new[]
        {
            new StatModifier(StatType.MaxHealth, StatOperation.Add, maxHealth),
            new StatModifier(StatType.Damage, StatOperation.Add, damage),
            new StatModifier(StatType.Defense, StatOperation.Add, defense),
            new StatModifier(StatType.MoveSpeed, StatOperation.Add, moveSpeed),
            new StatModifier(StatType.CritChance, StatOperation.Add, critChance),
            new StatModifier(StatType.CritDamageMultiplier, StatOperation.Add, critDamageMultiplier),
            new StatModifier(StatType.EffectResistance, StatOperation.Add, effectResistance),
            new StatModifier(StatType.EffectHit, StatOperation.Add, effectHit)
        };
    }
}
