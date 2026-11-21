using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Chỉ số cấu hình/final; các giá trị % dùng thang 100 = 100%, defense là giáp tiêu hao.</summary>
[Serializable]
public struct BaseStats
{
    #region Stat Values

    /// <summary>Max HP; current thuộc CharacterRuntimeData.</summary>
    [Tooltip("Máu tối đa gốc; tối thiểu 1. Máu hiện tại được lưu riêng ở runtime.")]
    [Min(1f)] public float maxHealth;
    /// <summary>Sát thương đầu ra trước critical.</summary>
    [Tooltip("Sát thương gốc trước hệ số skill và chí mạng; tối thiểu 0.")]
    [Min(0f)] public float damage;
    /// <summary>Max giáp hấp thụ, không phải giảm damage theo %.</summary>
    [Tooltip("Lượng giáp có thể hấp thụ sát thương và bị tiêu hao; không phải % giảm sát thương.")]
    [Min(0f)] public float defense;
    /// <summary>Tốc độ sau buff/debuff.</summary>
    [Tooltip("Tốc độ di chuyển theo world unit/giây, trước các bonus và hiệu ứng.")]
    [Min(0f)] public float moveSpeed;
    /// <summary>Tỷ lệ crit, có luật chuyển phần vượt 100 sang crit damage.</summary>
    [Tooltip("Tỷ lệ chí mạng theo thang 100 = 100%. Phần vượt 100 được runtime xử lý theo luật chí mạng.")]
    [Min(0f)] public float critChance;
    /// <summary>Phần damage cộng thêm khi crit.</summary>
    [Tooltip("Phần sát thương cộng thêm khi chí mạng theo thang 100: 50 = thêm 50%, tổng 150% khi chưa có điều chỉnh khác.")]
    [Min(0f)] public float critDamageMultiplier;
    /// <summary>Kháng xác suất effect.</summary>
    [Tooltip("Chỉ số kháng xác suất hiệu ứng theo thang phần trăm; không phải né sát thương đòn đánh.")]
    [FormerlySerializedAs("dodge"), Min(0f)] public float effectResistance;
    /// <summary>Chính xác effect.</summary>
    [Tooltip("Chỉ số tăng xác suất áp dụng hiệu ứng theo thang phần trăm; không phải độ chính xác của đạn.")]
    [FormerlySerializedAs("accuracy"), Min(0f)] public float effectHit;

    /// <summary>Bộ chỉ số mặc định cho template mới; các chỉ số không gán bằng 0.</summary>
    public static BaseStats Default => new BaseStats
    {
        maxHealth = 100f, damage = 10f, moveSpeed = 3f,
        critChance = 5f, critDamageMultiplier = 50f
    };

    #endregion

    #region Typed Stat Access

    /// <summary>Modifier dùng enum để đọc đúng field, không dùng reflection hoặc string.</summary>
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

    /// <summary>Ghi vào bản struct đang tính; caller kiểm tra và áp dụng giới hạn trước khi công bố FinalStats.</summary>
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

    /// <summary>Template và kết quả cuối phải hữu hạn, không âm; HP có ít nhất một điểm.</summary>
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
