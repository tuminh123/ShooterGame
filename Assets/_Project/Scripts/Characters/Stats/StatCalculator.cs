using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Công thức chỉ số duy nhất của game. Chỉ tính toán: không biết owner, UI, thời hạn effect hay HP/giáp đã mất.
/// </summary>
public static class StatCalculator
{
    #region Stat Evaluation

    // Bonus chiến đấu: kẹp trên để luật không vượt ý đồ thiết kế (kết liễu quá nửa máu, hút máu quá 100%).
    private const float MaxExecuteThreshold = 50f;
    private const float MaxLifeSteal = 100f;

    /// <summary>
    /// Mọi chỉ số dùng cùng công thức: <c>(base + tổng Add) × (1 + tổng Percent / 100)</c>, rồi kẹp trong giới hạn hợp lệ.
    /// Bonus lúc spawn, level và trang bị là các nguồn Add như mọi nguồn khác (xem <see cref="StatBonus.ToModifiers"/>).
    /// </summary>
    /// <remarks>Chỉ trả chỉ số gốc; modifier bonus chiến đấu (nếu có) được bỏ qua ở đây (UI chỉ hiển thị chỉ số gốc).</remarks>
    public static BaseStats Calculate(BaseStats baseStats, IEnumerable<StatModifier[]> groups)
    {
        BaseStats additions = default;
        BaseStats percentages = default;
        CombatBonuses bonuses = default;
        foreach (StatModifier[] group in groups) Accumulate(group, ref additions, ref percentages, ref bonuses);
        return Finish(baseStats, additions, percentages);
    }

    /// <summary>
    /// Cùng công thức, cho bảng nguồn của <see cref="StatSheet"/>: duyệt thẳng ValueCollection (enumerator struct) nên
    /// không cấp phát — được gọi mỗi khi chỉ số đổi (buff, slow, level).
    /// </summary>
    /// <remarks>Tính cả hai nhóm từ cùng danh sách nguồn: chỉ số gốc và bonus chiến đấu (<paramref name="bonuses"/>).</remarks>
    internal static BaseStats Calculate(BaseStats baseStats, Dictionary<long, StatModifier[]>.ValueCollection groups,
        out CombatBonuses bonuses)
    {
        BaseStats additions = default;
        BaseStats percentages = default;
        bonuses = default;
        foreach (StatModifier[] group in groups) Accumulate(group, ref additions, ref percentages, ref bonuses);
        bonuses = FinishBonuses(bonuses);
        return Finish(baseStats, additions, percentages);
    }

    // Bonus chiến đấu có gốc 0 và chỉ cộng thẳng (StatModifier.Validate chặn Percent), nên cộng thẳng vào nhóm riêng.
    private static void Accumulate(StatModifier[] group, ref BaseStats additions, ref BaseStats percentages,
        ref CombatBonuses bonuses)
    {
        foreach (StatModifier modifier in group)
        {
            if (StatTypes.IsCombatBonus(modifier.stat))
                bonuses.Set(modifier.stat, bonuses.Get(modifier.stat) + modifier.value);
            else if (modifier.operation == StatOperation.Add)
                additions.Set(modifier.stat, additions.Get(modifier.stat) + modifier.value);
            else
                percentages.Set(modifier.stat, percentages.Get(modifier.stat) + modifier.value);
        }
    }

    private static BaseStats Finish(BaseStats baseStats, BaseStats additions, BaseStats percentages)
    {
        BaseStats stats = baseStats;
        foreach (StatType type in StatTypes.Base)
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

    private static CombatBonuses FinishBonuses(CombatBonuses bonuses)
    {
        foreach (StatType type in StatTypes.Bonus)
            if (float.IsNaN(bonuses.Get(type)) || float.IsInfinity(bonuses.Get(type))) throw new ArgumentException("Stat totals must be finite.");
        bonuses.bossDamage = Mathf.Max(0f, bonuses.bossDamage);
        bonuses.executeThreshold = Mathf.Clamp(bonuses.executeThreshold, 0f, MaxExecuteThreshold);
        bonuses.lifeSteal = Mathf.Clamp(bonuses.lifeSteal, 0f, MaxLifeSteal);
        bonuses.pickupRange = Mathf.Max(0f, bonuses.pickupRange);
        bonuses.combatRange = Mathf.Max(0f, bonuses.combatRange);
        bonuses.damageTaken = Mathf.Max(0f, bonuses.damageTaken);
        bonuses.burningDamage = Mathf.Max(0f, bonuses.burningDamage);
        bonuses.critStun = Mathf.Clamp(bonuses.critStun, 0f, 100f);
        bonuses.farDamage = Mathf.Max(0f, bonuses.farDamage);
        bonuses.magnetPulse = Mathf.Max(0f, bonuses.magnetPulse);
        return bonuses;
    }

    #endregion
}
