using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cấu hình hiển thị của màn Equipment: màu khung và nhãn theo độ hiếm, icon và tên theo ô, tên chỉ số.
/// Icon và cách định dạng số của chỉ số lấy từ <see cref="CharacterScreenThemeSO"/> để không khai báo hai lần.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentTheme", menuName = "Shooter Game/UI/Equipment Theme")]
public sealed class EquipmentThemeSO : ScriptableObject
{
    #region Entry Types

    /// <summary>Màu của khung thẻ đồ (các lớp ItemFrame03) và nhãn độ hiếm.</summary>
    [Serializable]
    public sealed class RarityEntry
    {
        [SerializeField] private EquipmentRarity rarity;
        [SerializeField] private string text = "COMMON";
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color background = Color.white;
        [SerializeField] private Color light = Color.white;
        [SerializeField] private Color border = Color.white;
        [SerializeField] private Color glow = Color.white;
        [SerializeField] private Color cornerDeco = Color.white;
        [Tooltip("Màu viền tròn quanh icon ô.")]
        [SerializeField] private Color slotCircle = Color.white;

        public EquipmentRarity Rarity => rarity;
        public string Text => text;
        public Color TextColor => textColor;
        public Color Background => background;
        public Color Light => light;
        public Color Border => border;
        public Color Glow => glow;
        public Color CornerDeco => cornerDeco;
        public Color SlotCircle => slotCircle;
    }

    /// <summary>Tên, icon nhỏ (góc thẻ, tab) và hình mờ (ô trống) của một ô mặc.</summary>
    [Serializable]
    public sealed class SlotEntry
    {
        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private string text = "Weapon";
        [SerializeField] private Sprite icon;
        [Tooltip("Hình mờ hiện trong ô đang trống.")]
        [SerializeField] private Sprite emptyIcon;

        public EquipmentSlot Slot => slot;
        public string Text => text;
        public Sprite Icon => icon;
        public Sprite EmptyIcon => emptyIcon;
    }

    /// <summary>Tên hiển thị của một chỉ số trong popup.</summary>
    [Serializable]
    public sealed class StatNameEntry
    {
        [SerializeField] private StatType stat;
        [SerializeField] private string text;

        public StatType Stat => stat;
        public string Text => text;
    }

    #endregion

    #region Configuration

    [Tooltip("Icon và định dạng số của 8 chỉ số, dùng chung với màn Character.")]
    [SerializeField] private CharacterScreenThemeSO statTheme;
    [Tooltip("Mỗi EquipmentRarity một mục.")]
    [SerializeField] private List<RarityEntry> rarities = new List<RarityEntry>();
    [Tooltip("Mỗi EquipmentSlot một mục.")]
    [SerializeField] private List<SlotEntry> slots = new List<SlotEntry>();
    [Tooltip("Mỗi StatType một mục.")]
    [SerializeField] private List<StatNameEntry> statNames = new List<StatNameEntry>();

    /// <summary>Theme chỉ số dùng chung với màn Character.</summary>
    public CharacterScreenThemeSO StatTheme => statTheme;

    #endregion

    #region Lookups

    /// <summary>Màu và nhãn của độ hiếm.</summary>
    public RarityEntry GetRarity(EquipmentRarity rarity)
    {
        foreach (RarityEntry entry in rarities)
        {
            if (entry != null && entry.Rarity == rarity)
                return entry;
        }
        throw new KeyNotFoundException($"Equipment Theme is missing rarity {rarity}.");
    }

    /// <summary>Tên và icon của ô.</summary>
    public SlotEntry GetSlot(EquipmentSlot slot)
    {
        foreach (SlotEntry entry in slots)
        {
            if (entry != null && entry.Slot == slot)
                return entry;
        }
        throw new KeyNotFoundException($"Equipment Theme is missing slot {slot}.");
    }

    /// <summary>Tên hiển thị của chỉ số.</summary>
    public string GetStatName(StatType stat)
    {
        foreach (StatNameEntry entry in statNames)
        {
            if (entry != null && entry.Stat == stat)
                return entry.Text;
        }
        throw new KeyNotFoundException($"Equipment Theme is missing stat name {stat}.");
    }

    /// <summary>Icon của chỉ số (từ theme màn Character).</summary>
    public Sprite GetStatIcon(StatType stat) => GetStatEntry(stat).Icon;

    /// <summary>Giá trị chỉ số đã định dạng theo kiểu của chỉ số (số nguyên, thập phân, phần trăm).</summary>
    public string FormatStat(StatType stat, float value) => CharacterScreenThemeSO.FormatValue(value, GetStatEntry(stat).Format);

    private CharacterScreenThemeSO.StatEntry GetStatEntry(StatType stat)
    {
        foreach (CharacterScreenThemeSO.StatEntry entry in statTheme.Stats)
        {
            if (entry != null && entry.Stat == stat)
                return entry;
        }
        throw new KeyNotFoundException($"Character Screen Theme is missing stat {stat}.");
    }

    #endregion

    #region Validation

    /// <summary>Đủ mọi độ hiếm, ô và tên chỉ số; theme chỉ số dùng chung hợp lệ.</summary>
    public void Validate()
    {
        if (statTheme == null)
            throw new InvalidOperationException("Assign Stat Theme on Equipment Theme.");
        statTheme.Validate();
        foreach (EquipmentRarity rarity in (EquipmentRarity[])Enum.GetValues(typeof(EquipmentRarity)))
            GetRarity(rarity);
        foreach (EquipmentSlot slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
            GetSlot(slot);
        foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
            GetStatName(stat);
    }

    #endregion
}
