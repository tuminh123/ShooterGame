using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Luật tăng trưởng của trang bị, áp tham số của <see cref="EquipmentRulesSO"/>: chỉ số chính theo level, roll dòng phụ lúc nhận,
/// mốc dòng phụ khi lên cấp và sửa dòng phụ đã lưu khi luật đổi.
/// </summary>
/// <remarks>
/// Class concrete (một hành vi duy nhất nên không cần interface). Nhận <see cref="System.Random"/> qua constructor
/// để kết quả test được bằng seed cố định. Do <see cref="EquipmentInventory"/> tạo; không bind Zenject.
/// </remarks>
public sealed class EquipmentGrowth
{
    #region Fields

    private readonly EquipmentRulesSO rules;
    private readonly System.Random random;
    private readonly List<StatType> candidates = new List<StatType>(8); // Dùng lại khi chọn chỉ số cho dòng mới.

    #endregion

    #region Constructor

    public EquipmentGrowth(EquipmentRulesSO rules, System.Random random)
    {
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    #endregion

    #region Queries

    /// <summary>Level tối đa của trang bị.</summary>
    public int MaxLevel => rules.MaxLevel;

    /// <summary>Chỉ số chính cố định của ô.</summary>
    public StatType GetMainStat(EquipmentSlot slot) => rules.GetMainStat(slot);

    /// <summary>Giá trị chỉ số chính.</summary>
    public float GetMainValue(EquipmentSlot slot, EquipmentRarity rarity, int level) => rules.GetMainValue(slot, rarity, level);

    /// <summary>Giá nâng một món độ hiếm <paramref name="rarity"/> từ level hiện tại lên cấp kế tiếp.</summary>
    public CurrencyCost GetLevelUpCost(int currentLevel, EquipmentRarity rarity)
        => new CurrencyCost(rules.LevelUpCurrency, rules.GetLevelUpCost(currentLevel, rarity));

    #endregion

    #region Growth

    /// <summary>Roll các dòng phụ có sẵn của một bản đồ mới theo độ hiếm.</summary>
    public List<StatModifier> RollInitialSubstats(EquipmentSlot slot, EquipmentRarity rarity)
    {
        var substats = new List<StatModifier>(rules.MaxSubstats);
        int count = rules.GetInitialSubstats(rarity);
        for (int i = 0; i < count; i++)
            AddNewSubstat(substats, rules.GetMainStat(slot));
        return substats;
    }

    /// <summary>Dòng phụ của một bản đồ mới ở <paramref name="level"/>: roll ban đầu rồi áp lần lượt mọi mốc tới level đó.</summary>
    public List<StatModifier> RollSubstats(EquipmentSlot slot, EquipmentRarity rarity, int level)
    {
        List<StatModifier> substats = RollInitialSubstats(slot, rarity);
        for (int next = 2; next <= level; next++)
            ApplyMilestone(substats, rules.GetMainStat(slot), next);
        return substats;
    }

    /// <summary>
    /// Tăng một level: cập nhật chỉ số chính và áp mốc dòng phụ nếu level mới là mốc.
    /// Bên gọi đã kiểm tra chưa chạm level tối đa.
    /// </summary>
    public void LevelUp(EquipmentItem item)
    {
        int level = item.Level + 1;
        EquipmentSlot slot = item.Definition.Slot;
        item.SetLevel(level, rules.GetMainValue(slot, item.Rarity, level));
        ApplyMilestone(item.MutableSubstats, item.MainStat, level);
    }

    /// <summary>
    /// Sửa dòng phụ đã lưu cho khớp luật hiện tại (luật có thể đổi sau khi người chơi đã lưu):
    /// bỏ dòng trùng chỉ số chính hoặc chỉ số không còn được roll, roll thêm nếu thiếu, bỏ dòng cuối nếu thừa.
    /// Trả về true nếu có sửa (bên gọi ghi warning và lưu lại).
    /// </summary>
    public bool Repair(EquipmentItem item)
    {
        List<StatModifier> substats = item.MutableSubstats;
        int before = substats.Count;
        bool changed = substats.RemoveAll(substat => substat.stat == item.MainStat || !rules.TryGetRoll(substat.stat, out _)) > 0;
        int expected = rules.GetExpectedSubstats(item.Rarity, item.Level);
        while (substats.Count < expected)
            AddNewSubstat(substats, item.MainStat);
        if (substats.Count > expected)
            substats.RemoveRange(expected, substats.Count - expected);
        return changed || substats.Count != before;
    }

    #endregion

    #region Private Helpers

    // Mốc dòng phụ: mở dòng mới hoặc cộng vào một dòng ngẫu nhiên; level thường thì không làm gì.
    private void ApplyMilestone(List<StatModifier> substats, StatType mainStat, int level)
    {
        if (!rules.IsMilestone(level))
            return;
        if (rules.WillUnlockSubstat(level, substats.Count))
            AddNewSubstat(substats, mainStat);
        else
            BoostRandomSubstat(substats);
    }

    // Chỉ số mới: ngẫu nhiên trong các chỉ số được roll, khác chỉ số chính và khác mọi dòng đang có.
    private void AddNewSubstat(List<StatModifier> substats, StatType mainStat)
    {
        candidates.Clear();
        foreach (EquipmentRulesSO.SubstatRoll roll in rules.SubstatRolls)
        {
            if (roll.Stat != mainStat && !Contains(substats, roll.Stat))
                candidates.Add(roll.Stat);
        }
        // Validate của luật đảm bảo đủ chỉ số; vẫn phòng cấu hình sai để không treo vòng lặp.
        if (candidates.Count == 0)
            throw new InvalidOperationException("Equipment Rules has no substat left to roll.");
        StatType stat = candidates[random.Next(candidates.Count)];
        substats.Add(new StatModifier(stat, StatOperation.Add, Roll(stat)));
    }

    private void BoostRandomSubstat(List<StatModifier> substats)
    {
        if (substats.Count == 0)
            return;
        int index = random.Next(substats.Count);
        StatModifier substat = substats[index];
        substat.value = Round(substat.value + Roll(substat.stat));
        substats[index] = substat;
    }

    private float Roll(StatType stat)
    {
        rules.TryGetRoll(stat, out EquipmentRulesSO.SubstatRoll roll);
        float value = roll.Min + (float)random.NextDouble() * (roll.Max - roll.Min);
        return Round(value);
    }

    // Làm tròn hai chữ số thập phân để số trên UI gọn và cộng dồn không lệch (MoveSpeed có giá trị nhỏ như 0.05).
    private static float Round(float value) => Mathf.Round(value * 100f) / 100f;

    private static bool Contains(List<StatModifier> substats, StatType stat)
    {
        foreach (StatModifier substat in substats)
        {
            if (substat.stat == stat)
                return true;
        }
        return false;
    }

    #endregion
}
