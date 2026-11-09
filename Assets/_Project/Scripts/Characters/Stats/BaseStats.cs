using System;
using UnityEngine;
using UnityEngine.Serialization;

// Chỉ số cấu hình/final; các giá trị % dùng thang 100 = 100%, defense là giáp tiêu hao.
[Serializable]
public struct BaseStats
{
    #region Stat Values

    [Min(1f)] public float maxHealth; // Max HP; current thuộc CharacterRuntimeData.
    [Min(0f)] public float damage; // Sát thương đầu ra trước critical.
    [Min(0f)] public float defense; // Max giáp hấp thụ, không phải giảm damage theo %.
    [Min(0f)] public float moveSpeed; // Tốc độ sau buff/debuff.
    [Min(0f)] public float critChance; // Tỷ lệ crit, có luật chuyển phần vượt 100 sang crit damage.
    [Min(0f)] public float critDamageMultiplier; // Phần damage cộng thêm khi crit.
    [FormerlySerializedAs("dodge"), Min(0f)] public float effectResistance; // Kháng xác suất effect.
    [FormerlySerializedAs("accuracy"), Min(0f)] public float effectHit; // Chính xác effect.

    public static BaseStats Default => new BaseStats
    {
        maxHealth = 100f, damage = 10f, moveSpeed = 3f,
        critChance = 5f, critDamageMultiplier = 50f
    };

    #endregion

    #region Typed Stat Access

    // Modifier dùng enum để đọc đúng field, không dùng reflection hoặc string.
    public float Get(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return maxHealth;
            case StatType.Damage: return damage;
            case StatType.Defense: return defense;
            case StatType.MoveSpeed: return moveSpeed;
            case StatType.CritChance: return critChance;
            case StatType.CritDamageMultiplier: return critDamageMultiplier;
            case StatType.EffectResistance: return effectResistance;
            case StatType.EffectHit: return effectHit;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    // Ghi vào bản struct đang tính; caller kiểm tra và áp dụng giới hạn trước khi công bố FinalStats.
    public void Set(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHealth: maxHealth = value; break;
            case StatType.Damage: damage = value; break;
            case StatType.Defense: defense = value; break;
            case StatType.MoveSpeed: moveSpeed = value; break;
            case StatType.CritChance: critChance = value; break;
            case StatType.CritDamageMultiplier: critDamageMultiplier = value; break;
            case StatType.EffectResistance: effectResistance = value; break;
            case StatType.EffectHit: effectHit = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    #endregion

    #region Validation

    // Template và kết quả cuối phải hữu hạn, không âm; HP có ít nhất một điểm.
    public void Validate()
    {
        if (!Finite(maxHealth) || !Finite(damage) || !Finite(defense) || !Finite(moveSpeed) ||
            !Finite(critChance) || !Finite(critDamageMultiplier) || !Finite(effectResistance) ||
            !Finite(effectHit))
            throw new ArgumentException("Character stats must be finite.");
        if (maxHealth < 1f || damage < 0f || defense < 0f || moveSpeed < 0f || critChance < 0f ||
            critDamageMultiplier < 0f || effectResistance < 0f || effectHit < 0f)
            throw new ArgumentException("Invalid character stat range.");
    }

    // Kiểm tra float trước khi truyền sang UI, movement hoặc damage.
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion
}
