using System;
using System.Collections.Generic;
using UnityEngine;

// Chỉ tính toán: không biết owner, UI, thời hạn effect hoặc lượng HP/giáp đã mất.
public static class StatCalculator
{
    #region Stat Evaluation

    private static readonly StatType[] statTypes = (StatType[])Enum.GetValues(typeof(StatType)); // Cache để không cấp phát enum array mỗi lần tính.

    // Mọi loại stat dùng cùng công thức: (base + external + tổng Add) * (1 + tổng Percent / 100).
    public static BaseStats Calculate(BaseStats baseStats, StatBonus externalBonus, IEnumerable<StatModifier[]> groups)
    {
        BaseStats stats = externalBonus.ApplyTo(baseStats);
        BaseStats additions = default;
        BaseStats percentages = default;
        foreach (StatModifier[] group in groups)
            foreach (StatModifier modifier in group)
            {
                if (modifier.operation == StatOperation.Add)
                    additions.Set(modifier.stat, additions.Get(modifier.stat) + modifier.value);
                else
                    percentages.Set(modifier.stat, percentages.Get(modifier.stat) + modifier.value);
            }
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
