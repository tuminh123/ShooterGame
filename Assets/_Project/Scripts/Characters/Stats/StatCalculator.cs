using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Công thức chỉ số duy nhất của game. Chỉ tính toán: không biết owner, UI, thời hạn effect hay HP/giáp đã mất.
/// </summary>
public static class StatCalculator
{
    #region Stat Evaluation

    private static readonly StatType[] statTypes = (StatType[])Enum.GetValues(typeof(StatType)); // Cache để không cấp phát enum array mỗi lần tính.

    /// <summary>
    /// Mọi chỉ số dùng cùng công thức: <c>(base + tổng Add) × (1 + tổng Percent / 100)</c>, rồi kẹp trong giới hạn hợp lệ.
    /// Bonus lúc spawn, level và trang bị là các nguồn Add như mọi nguồn khác (xem <see cref="StatBonus.ToModifiers"/>).
    /// </summary>
    public static BaseStats Calculate(BaseStats baseStats, IEnumerable<StatModifier[]> groups)
    {
        BaseStats additions = default;
        BaseStats percentages = default;
        foreach (StatModifier[] group in groups) Accumulate(group, ref additions, ref percentages);
        return Finish(baseStats, additions, percentages);
    }

    /// <summary>
    /// Cùng công thức, cho bảng nguồn của <see cref="StatSheet"/>: duyệt thẳng ValueCollection (enumerator struct) nên
    /// không cấp phát — được gọi mỗi khi chỉ số đổi (buff, slow, level).
    /// </summary>
    internal static BaseStats Calculate(BaseStats baseStats, Dictionary<long, StatModifier[]>.ValueCollection groups)
    {
        BaseStats additions = default;
        BaseStats percentages = default;
        foreach (StatModifier[] group in groups) Accumulate(group, ref additions, ref percentages);
        return Finish(baseStats, additions, percentages);
    }

    private static void Accumulate(StatModifier[] group, ref BaseStats additions, ref BaseStats percentages)
    {
        foreach (StatModifier modifier in group)
        {
            if (modifier.operation == StatOperation.Add)
                additions.Set(modifier.stat, additions.Get(modifier.stat) + modifier.value);
            else
                percentages.Set(modifier.stat, percentages.Get(modifier.stat) + modifier.value);
        }
    }

    private static BaseStats Finish(BaseStats baseStats, BaseStats additions, BaseStats percentages)
    {
        BaseStats stats = baseStats;
        foreach (StatType type in statTypes)
        {
            float value = (stats.Get(type) + additions.Get(type)) * (1f + percentages.Get(type) / 100f);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Stat totals must be finite.");
            stats.Set(type, value);
        }
        stats.maxHealth = Mathf.Max(1f, stats.maxHealth);
        stats.defense = Mathf.Max(0f, stats.defense);
        stats.damage = Mathf.Max(0f, stats.damage);
        stats.moveSpeed = Mathf.Clamp(stats.moveSpeed, 0f, baseStats.moveSpeed * 2f);
        stats.critChance = Mathf.Max(0f, stats.critChance);
        stats.critDamageMultiplier = Mathf.Max(0f, stats.critDamageMultiplier);
        stats.effectResistance = Mathf.Clamp(stats.effectResistance, 0f, 80f);
        stats.effectHit = Mathf.Max(0f, stats.effectHit);
        return stats;
    }

    #endregion
}
