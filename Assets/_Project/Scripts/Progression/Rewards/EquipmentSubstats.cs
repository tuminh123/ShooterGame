using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cách một bản trang bị thưởng có dòng phụ: roll theo luật hay nhập sẵn.</summary>
/// <remarks>Họ strategy chỉ chứa tham số, chọn bằng <c>[SerializeReference]</c>.</remarks>
public interface IEquipmentSubstats
{
    /// <summary>Kiểm tra với luật trang bị; ném lỗi nếu món tạo ra sẽ bị sửa lại lúc load.</summary>
    void Validate(EquipmentRulesSO rules, EquipmentSlot slot, EquipmentRarity rarity, int level);

    /// <summary>Danh sách dòng phụ mới, không chia sẻ với asset.</summary>
    List<StatModifier> Create(EquipmentGrowth growth, EquipmentSlot slot, EquipmentRarity rarity, int level);
}

/// <summary>Roll như đồ thường rồi áp mọi mốc tới level đã chọn.</summary>
[Serializable]
public sealed class RolledSubstats : IEquipmentSubstats
{
    /// <inheritdoc/>
    public void Validate(EquipmentRulesSO rules, EquipmentSlot slot, EquipmentRarity rarity, int level) { }

    /// <inheritdoc/>
    public List<StatModifier> Create(EquipmentGrowth growth, EquipmentSlot slot, EquipmentRarity rarity, int level)
        => growth.RollSubstats(slot, rarity, level);
}

/// <summary>
/// Dòng phụ nhập sẵn. Số dòng và loại chỉ số phải đúng luật vì <see cref="EquipmentGrowth.Repair"/> sửa món lệch luật lúc load;
/// riêng giá trị được đặt tự do (kể cả cao hơn mức roll).
/// </summary>
[Serializable]
public sealed class FixedSubstats : IEquipmentSubstats
{
    /// <summary>Một dòng phụ nhập sẵn.</summary>
    [Serializable]
    public struct Line
    {
        public StatType stat;
        public float value;
    }

    [SerializeField] private List<Line> lines = new List<Line>();

    public FixedSubstats() { }

    public FixedSubstats(List<Line> lines) => this.lines = lines;

    /// <inheritdoc/>
    public void Validate(EquipmentRulesSO rules, EquipmentSlot slot, EquipmentRarity rarity, int level)
    {
        int expected = rules.GetExpectedSubstats(rarity, level);
        if (lines.Count != expected)
            throw new InvalidOperationException($"Fixed substats need exactly {expected} lines for {rarity} Lv.{level}, found {lines.Count}.");
        var seen = new HashSet<StatType>();
        foreach (Line line in lines)
        {
            if (line.stat == rules.GetMainStat(slot) || !rules.TryGetRoll(line.stat, out _) || !seen.Add(line.stat))
                throw new InvalidOperationException($"Fixed substat {line.stat} is the main stat, duplicated or not allowed by Equipment Rules.");
            if (float.IsNaN(line.value) || float.IsInfinity(line.value) || line.value <= 0f)
                throw new InvalidOperationException($"Fixed substat {line.stat} needs a positive value.");
        }
    }

    /// <inheritdoc/>
    public List<StatModifier> Create(EquipmentGrowth growth, EquipmentSlot slot, EquipmentRarity rarity, int level)
    {
        var result = new List<StatModifier>(lines.Count);
        foreach (Line line in lines)
            result.Add(new StatModifier(line.stat, StatOperation.Add, line.value));
        return result;
    }
}
