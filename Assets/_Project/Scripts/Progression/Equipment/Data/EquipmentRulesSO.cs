using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Luật tăng trưởng dùng chung cho mọi trang bị: chỉ số chính theo ô × độ hiếm, số dòng phụ ban đầu, level tối đa,
/// mốc dòng phụ, khoảng roll dòng phụ và giá nâng cấp. Chỉ chứa tham số; <see cref="EquipmentGrowth"/> áp dụng.
/// </summary>
/// <remarks>
/// Chỉ số chính ở Lv.L = Base + Per Level × (L − 1). Mốc thứ k (Lv = k × Milestone Interval): nếu món đang có ít hơn
/// k dòng phụ thì mở dòng mới, đủ rồi thì cộng một lần roll vào một dòng phụ ngẫu nhiên.
/// </remarks>
[CreateAssetMenu(fileName = "EquipmentRules", menuName = "Shooter Game/Data/Equipment Rules")]
public sealed class EquipmentRulesSO : ScriptableObject
{
    #region Entry Types

    /// <summary>Chỉ số chính của một độ hiếm: giá trị Lv.1 và lượng tăng mỗi cấp.</summary>
    [Serializable]
    public sealed class MainStatValue
    {
        [SerializeField] private EquipmentRarity rarity;
        [Tooltip("Giá trị chỉ số chính ở Lv.1.")]
        [SerializeField] private float baseValue;
        [Tooltip("Lượng cộng thêm mỗi lần lên cấp.")]
        [SerializeField] private float perLevel;

        public EquipmentRarity Rarity => rarity;
        public float BaseValue => baseValue;
        public float PerLevel => perLevel;
    }

    /// <summary>Luật của một ô: chỉ số chính và giá trị theo từng độ hiếm.</summary>
    [Serializable]
    public sealed class SlotRule
    {
        [SerializeField] private EquipmentSlot slot;
        [Tooltip("Chỉ số chính cố định của ô này.")]
        [SerializeField] private StatType mainStat;
        [Tooltip("Mỗi độ hiếm một mục.")]
        [SerializeField] private List<MainStatValue> values = new List<MainStatValue>();

        public EquipmentSlot Slot => slot;
        public StatType MainStat => mainStat;
        public IReadOnlyList<MainStatValue> Values => values;
    }

    /// <summary>Số dòng phụ có sẵn khi nhận đồ của một độ hiếm.</summary>
    [Serializable]
    public sealed class RarityRule
    {
        [SerializeField] private EquipmentRarity rarity;
        [Tooltip("Số dòng phụ được roll khi nhận đồ (0..Max Substats).")]
        [SerializeField, Min(0)] private int initialSubstats;

        public EquipmentRarity Rarity => rarity;
        public int InitialSubstats => initialSubstats;
    }

    /// <summary>Khoảng giá trị một lần roll của một chỉ số phụ (dùng cả khi mở dòng mới lẫn khi cộng vào dòng cũ).</summary>
    [Serializable]
    public sealed class SubstatRoll
    {
        [SerializeField] private StatType stat;
        [SerializeField] private float min;
        [SerializeField] private float max;

        public StatType Stat => stat;
        public float Min => min;
        public float Max => max;
    }

    #endregion

    #region Configuration

    [Tooltip("Đủ 6 ô, mỗi ô một lần.")]
    [SerializeField] private List<SlotRule> slots = new List<SlotRule>();
    [Tooltip("Đủ 5 độ hiếm, mỗi độ hiếm một lần.")]
    [SerializeField] private List<RarityRule> rarities = new List<RarityRule>();
    [Tooltip("Level tối đa của trang bị.")]
    [SerializeField, Min(1)] private int maxLevel = 20;
    [Tooltip("Cứ mỗi bấy nhiêu cấp là một mốc dòng phụ (mở dòng mới hoặc cộng vào dòng cũ).")]
    [SerializeField, Min(1)] private int milestoneInterval = 5;
    [Tooltip("Số dòng phụ tối đa của một món.")]
    [SerializeField, Min(0)] private int maxSubstats = 4;
    [Tooltip("Các chỉ số có thể ra ở dòng phụ, kèm khoảng roll. Chỉ số chính của ô không bao giờ ra ở dòng phụ.")]
    [SerializeField] private List<SubstatRoll> substatRolls = new List<SubstatRoll>();

    [Header("Level Up Cost (TODO: chưa trừ tiền)")]
    [Tooltip("Loại tiền hiện trên nút Level Up.")]
    [SerializeField] private CurrencyType levelUpCurrency = CurrencyType.Gold;
    [Tooltip("Giá nâng từ Lv.1 lên Lv.2.")]
    [SerializeField, Min(0)] private int levelUpCostBase = 100;
    [Tooltip("Giá tăng thêm cho mỗi cấp hiện tại sau Lv.1.")]
    [SerializeField, Min(0)] private int levelUpCostPerLevel = 50;

    /// <summary>Level tối đa của trang bị.</summary>
    public int MaxLevel => maxLevel;

    /// <summary>Khoảng cách giữa hai mốc dòng phụ.</summary>
    public int MilestoneInterval => milestoneInterval;

    /// <summary>Số dòng phụ tối đa.</summary>
    public int MaxSubstats => maxSubstats;

    /// <summary>Các chỉ số có thể ra ở dòng phụ.</summary>
    public IReadOnlyList<SubstatRoll> SubstatRolls => substatRolls;

    /// <summary>Loại tiền của giá nâng cấp.</summary>
    public CurrencyType LevelUpCurrency => levelUpCurrency;

    #endregion

    #region Lookups

    /// <summary>Chỉ số chính cố định của ô.</summary>
    public StatType GetMainStat(EquipmentSlot slot) => GetSlot(slot).MainStat;

    /// <summary>Giá trị chỉ số chính của ô và độ hiếm ở <paramref name="level"/>.</summary>
    public float GetMainValue(EquipmentSlot slot, EquipmentRarity rarity, int level)
    {
        foreach (MainStatValue value in GetSlot(slot).Values)
        {
            if (value != null && value.Rarity == rarity)
                return value.BaseValue + value.PerLevel * (Mathf.Clamp(level, 1, maxLevel) - 1);
        }
        throw new KeyNotFoundException($"{name}: thiếu chỉ số chính của {slot} / {rarity}.");
    }

    /// <summary>Số dòng phụ có sẵn khi nhận đồ.</summary>
    public int GetInitialSubstats(EquipmentRarity rarity)
    {
        foreach (RarityRule rule in rarities)
        {
            if (rule != null && rule.Rarity == rarity)
                return rule.InitialSubstats;
        }
        throw new KeyNotFoundException($"{name}: thiếu luật độ hiếm {rarity}.");
    }

    /// <summary>Khoảng roll của một chỉ số phụ; false nếu chỉ số đó không được ra ở dòng phụ.</summary>
    public bool TryGetRoll(StatType stat, out SubstatRoll roll)
    {
        foreach (SubstatRoll entry in substatRolls)
        {
            if (entry != null && entry.Stat == stat)
            {
                roll = entry;
                return true;
            }
        }
        roll = null;
        return false;
    }

    /// <summary>Giá nâng từ <paramref name="currentLevel"/> lên cấp kế tiếp.</summary>
    public long GetLevelUpCost(int currentLevel) => levelUpCostBase + (long)levelUpCostPerLevel * (Math.Max(1, currentLevel) - 1);

    /// <summary>Số mốc dòng phụ đã qua ở <paramref name="level"/> (Lv.12 với mốc 5 → 2).</summary>
    public int GetMilestonesReached(int level) => Mathf.Clamp(level, 1, maxLevel) / milestoneInterval;

    /// <summary>Số dòng phụ đúng theo luật của một món ở độ hiếm và level đã cho.</summary>
    public int GetExpectedSubstats(EquipmentRarity rarity, int level)
        => Math.Min(maxSubstats, Math.Max(GetInitialSubstats(rarity), GetMilestonesReached(level)));

    /// <summary>Level là mốc dòng phụ (mở hoặc cộng) hay không.</summary>
    public bool IsMilestone(int level) => level >= milestoneInterval && level % milestoneInterval == 0 && level <= maxLevel;

    /// <summary>
    /// Nơi DUY NHẤT quyết định mốc <paramref name="level"/> mở dòng phụ mới (true) hay cộng vào dòng cũ (false):
    /// mốc thứ k mở dòng mới khi món đang có ít hơn k dòng và chưa chạm Max Substats. EquipmentGrowth và popup cùng gọi.
    /// </summary>
    public bool WillUnlockSubstat(int level, int currentSubstats)
    {
        int milestone = level / milestoneInterval;
        return currentSubstats < milestone && currentSubstats < maxSubstats;
    }

    /// <summary>Level mở dòng phụ thứ <paramref name="substatNumber"/> (bắt đầu từ 1); false nếu không bao giờ mở.</summary>
    public bool TryGetSubstatUnlockLevel(int substatNumber, out int level)
    {
        level = substatNumber * milestoneInterval;
        return substatNumber >= 1 && substatNumber <= maxSubstats && level <= maxLevel;
    }

    private SlotRule GetSlot(EquipmentSlot slot)
    {
        foreach (SlotRule rule in slots)
        {
            if (rule != null && rule.Slot == slot)
                return rule;
        }
        throw new KeyNotFoundException($"{name}: thiếu luật của ô {slot}.");
    }

    #endregion

    #region Validation

    /// <summary>Đủ 6 ô và 5 độ hiếm (không trùng), khoảng roll hợp lệ, đủ chỉ số phụ cho mọi ô.</summary>
    public void Validate()
    {
        ValidateSlots();
        ValidateRarities();
        ValidateRolls();
        foreach (EquipmentSlot slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
        {
            // Dòng phụ không trùng nhau và không trùng chỉ số chính: cần đủ chỉ số khác chỉ số chính.
            int available = 0;
            foreach (SubstatRoll roll in substatRolls)
            {
                if (roll.Stat != GetMainStat(slot))
                    available++;
            }
            if (available < maxSubstats)
                throw new InvalidOperationException($"{name}: ô {slot} chỉ có {available} chỉ số phụ, cần ít nhất {maxSubstats}.");
        }
    }

    private void ValidateSlots()
    {
        var seen = new HashSet<EquipmentSlot>();
        foreach (SlotRule rule in slots)
        {
            if (rule == null || !Enum.IsDefined(typeof(EquipmentSlot), rule.Slot) || !seen.Add(rule.Slot))
                throw new InvalidOperationException($"{name}: luật ô trống, sai hoặc trùng.");
            if (!Enum.IsDefined(typeof(StatType), rule.MainStat))
                throw new InvalidOperationException($"{name}: chỉ số chính của ô {rule.Slot} không hợp lệ.");
            foreach (EquipmentRarity rarity in (EquipmentRarity[])Enum.GetValues(typeof(EquipmentRarity)))
            {
                float value = GetMainValue(rule.Slot, rarity, maxLevel);
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidOperationException($"{name}: chỉ số chính của {rule.Slot} / {rarity} không hợp lệ.");
            }
        }
        foreach (EquipmentSlot slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
            GetSlot(slot);
    }

    private void ValidateRarities()
    {
        var seen = new HashSet<EquipmentRarity>();
        foreach (RarityRule rule in rarities)
        {
            if (rule == null || !seen.Add(rule.Rarity) || rule.InitialSubstats > maxSubstats)
                throw new InvalidOperationException($"{name}: luật độ hiếm trống, trùng hoặc vượt Max Substats.");
        }
        foreach (EquipmentRarity rarity in (EquipmentRarity[])Enum.GetValues(typeof(EquipmentRarity)))
            GetInitialSubstats(rarity);
    }

    private void ValidateRolls()
    {
        var seen = new HashSet<StatType>();
        foreach (SubstatRoll roll in substatRolls)
        {
            if (roll == null || !Enum.IsDefined(typeof(StatType), roll.Stat) || !seen.Add(roll.Stat))
                throw new InvalidOperationException($"{name}: khoảng roll trống, sai hoặc trùng chỉ số.");
            if (float.IsNaN(roll.Min) || float.IsNaN(roll.Max) || roll.Min < 0f || roll.Min > roll.Max)
                throw new InvalidOperationException($"{name}: khoảng roll của {roll.Stat} phải có 0 <= Min <= Max.");
        }
    }

    #endregion
}
