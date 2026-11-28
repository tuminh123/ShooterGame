using System;

/// <summary>
/// Nhóm bonus chiến đấu của một lượt spawn (thêm sát thương lên boss, kết liễu, hút máu, tầm nhặt…), tách khỏi
/// <see cref="BaseStats"/>: không có giá trị gốc trong CharacterDataSO, chỉ đến từ skill/buff qua cùng đường
/// <see cref="StatSheet.AddSource(StatModifier)"/> như mọi chỉ số khác.
/// </summary>
/// <remarks>Giá trị theo phần trăm, chỉ cộng thẳng (modifier <see cref="StatOperation.Add"/>); kẹp ở <see cref="StatCalculator"/>.</remarks>
public struct CombatBonuses
{
    #region Values

    /// <summary>% sát thương cộng thêm lên boss.</summary>
    public float bossDamage;
    /// <summary>% HP: quái thường có HP hiện tại / HP tối đa dưới ngưỡng này bị kết liễu.</summary>
    public float executeThreshold;
    /// <summary>% lượng HP thật đã trừ được hồi cho người đánh.</summary>
    public float lifeSteal;
    /// <summary>% tầm nhặt loot cộng thêm.</summary>
    public float pickupRange;
    /// <summary>% tầm chiến đấu cộng thêm: mọi skill của nhân vật tìm mục tiêu và chọn chỗ xuất hiện xa hơn.</summary>
    public float combatRange;
    /// <summary>% sát thương nhận thêm (debuff trên mục tiêu: dấu săn…).</summary>
    public float damageTaken;
    /// <summary>% sát thương thêm lên mục tiêu đang cháy.</summary>
    public float burningDamage;
    /// <summary>% cơ hội đòn chí mạng gây choáng ngắn.</summary>
    public float critStun;
    /// <summary>% sát thương thêm lên mục tiêu ở nửa ngoài tầm chiến đấu.</summary>
    public float farDamage;
    /// <summary>Giây giữa hai lần hút toàn bộ EXP trên màn; 0 = không có. Lấy giá trị nhỏ nhất có nghĩa nên dùng cộng thẳng từ một nguồn.</summary>
    public float magnetPulse;

    #endregion

    #region Typed Access

    /// <summary>Đọc theo <see cref="StatType"/> thuộc dải bonus chiến đấu.</summary>
    public float Get(StatType type)
    {
        switch (type)
        {
            case StatType.BossDamage: return bossDamage;
            case StatType.ExecuteThreshold: return executeThreshold;
            case StatType.LifeSteal: return lifeSteal;
            case StatType.PickupRange: return pickupRange;
            case StatType.CombatRange: return combatRange;
            case StatType.DamageTaken: return damageTaken;
            case StatType.BurningDamage: return burningDamage;
            case StatType.CritStun: return critStun;
            case StatType.FarDamage: return farDamage;
            case StatType.MagnetPulse: return magnetPulse;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "Not a combat bonus stat.");
        }
    }

    /// <summary>Ghi theo <see cref="StatType"/> thuộc dải bonus chiến đấu.</summary>
    public void Set(StatType type, float value)
    {
        switch (type)
        {
            case StatType.BossDamage: bossDamage = value; break;
            case StatType.ExecuteThreshold: executeThreshold = value; break;
            case StatType.LifeSteal: lifeSteal = value; break;
            case StatType.PickupRange: pickupRange = value; break;
            case StatType.CombatRange: combatRange = value; break;
            case StatType.DamageTaken: damageTaken = value; break;
            case StatType.BurningDamage: burningDamage = value; break;
            case StatType.CritStun: critStun = value; break;
            case StatType.FarDamage: farDamage = value; break;
            case StatType.MagnetPulse: magnetPulse = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "Not a combat bonus stat.");
        }
    }

    #endregion
}

/// <summary>Phân nhóm <see cref="StatType"/> theo dải số: chỉ số gốc (0–99) và bonus chiến đấu (100+).</summary>
public static class StatTypes
{
    /// <summary>Số đầu tiên của dải bonus chiến đấu.</summary>
    public const int CombatBonusStart = 100;

    /// <summary>Mọi chỉ số gốc (thuộc <see cref="BaseStats"/>), cache một lần để không cấp phát khi tính chỉ số.</summary>
    public static readonly StatType[] Base = Filter(false);

    /// <summary>Mọi bonus chiến đấu (thuộc <see cref="CombatBonuses"/>).</summary>
    public static readonly StatType[] Bonus = Filter(true);

    /// <summary>Chỉ số thuộc nhóm bonus chiến đấu (không nằm trong BaseStats).</summary>
    public static bool IsCombatBonus(StatType type) => (int)type >= CombatBonusStart;

    private static StatType[] Filter(bool combatBonus)
    {
        var values = (StatType[])Enum.GetValues(typeof(StatType));
        int count = 0;
        foreach (StatType value in values) if (IsCombatBonus(value) == combatBonus) count++;
        var result = new StatType[count];
        int index = 0;
        foreach (StatType value in values) if (IsCombatBonus(value) == combatBonus) result[index++] = value;
        return result;
    }
}
