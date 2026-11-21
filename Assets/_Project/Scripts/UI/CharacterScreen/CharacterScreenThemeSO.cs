using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Cấu hình hiển thị của màn Character: icon và cách định dạng từng chỉ số, nhãn độ hiếm, icon tiền.
/// Chỉ chứa dữ liệu trình bày, không chứa trạng thái runtime.
/// </summary>
[CreateAssetMenu(fileName = "CharacterScreenTheme", menuName = "Shooter Game/UI/Character Screen Theme")]
public sealed class CharacterScreenThemeSO : ScriptableObject
{
    #region Entry Types

    /// <summary>Hiển thị của một chỉ số. Thứ tự trong danh sách là thứ tự ô StatRow trên màn.</summary>
    [Serializable]
    public sealed class StatEntry
    {
        [SerializeField] private StatType stat;
        [SerializeField] private Sprite icon;
        [SerializeField] private StatFormat format = StatFormat.Integer;

        public StatType Stat => stat;
        public Sprite Icon => icon;
        public StatFormat Format => format;
    }

    /// <summary>Nhãn của một độ hiếm.</summary>
    [Serializable]
    public sealed class RarityEntry
    {
        [SerializeField] private CharacterRarity rarity;
        [SerializeField] private Sprite label;
        [SerializeField] private string text = "COMMON";
        [SerializeField] private Color textColor = Color.white;

        public CharacterRarity Rarity => rarity;
        public Sprite Label => label;
        public string Text => text;
        public Color TextColor => textColor;
    }

    /// <summary>Icon của một loại tiền, dùng cho giá trên nút UPGRADE.</summary>
    [Serializable]
    public sealed class CurrencyEntry
    {
        [SerializeField] private CurrencyType currency;
        [SerializeField] private Sprite icon;

        public CurrencyType Currency => currency;
        public Sprite Icon => icon;
    }

    #endregion

    #region Configuration

    [Tooltip("Đủ 8 StatType, mỗi loại một lần. Thứ tự = thứ tự StatRow_1..8 trên màn Detail.")]
    [SerializeField] private List<StatEntry> stats = new List<StatEntry>();
    [Tooltip("Mỗi CharacterRarity một mục.")]
    [SerializeField] private List<RarityEntry> rarities = new List<RarityEntry>();
    [Tooltip("Mỗi CurrencyType một mục.")]
    [SerializeField] private List<CurrencyEntry> currencies = new List<CurrencyEntry>();

    /// <summary>Danh sách chỉ số theo thứ tự hiển thị.</summary>
    public IReadOnlyList<StatEntry> Stats => stats;

    #endregion

    #region Lookups

    /// <summary>Nhãn của độ hiếm; ném lỗi nếu thiếu cấu hình (Validate đã chặn trước).</summary>
    public RarityEntry GetRarity(CharacterRarity rarity)
    {
        foreach (RarityEntry entry in rarities)
            if (entry != null && entry.Rarity == rarity) return entry;
        throw new KeyNotFoundException($"Character Screen Theme is missing rarity {rarity}.");
    }

    /// <summary>Icon của loại tiền; null nếu thiếu.</summary>
    public Sprite GetCurrencyIcon(CurrencyType currency)
    {
        foreach (CurrencyEntry entry in currencies)
            if (entry != null && entry.Currency == currency) return entry.Icon;
        return null;
    }

    /// <summary>Định dạng giá trị theo kiểu của chỉ số. Dùng InvariantCulture để dấu thập phân không đổi theo máy.</summary>
    public static string FormatValue(float value, StatFormat format)
    {
        switch (format)
        {
            case StatFormat.Integer: return Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture);
            case StatFormat.Decimal: return value.ToString("0.#", CultureInfo.InvariantCulture);
            case StatFormat.Percent: return value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    #endregion

    #region Validation

    /// <summary>Kiểm tra đủ 8 StatType (không trùng), đủ mọi CharacterRarity và mọi CurrencyType.</summary>
    public void Validate()
    {
        var seen = new HashSet<StatType>();
        foreach (StatEntry entry in stats)
            if (entry == null || !seen.Add(entry.Stat))
                throw new InvalidOperationException("Character Screen Theme has an empty or duplicate stat entry.");
        foreach (StatType type in (StatType[])Enum.GetValues(typeof(StatType)))
            if (!seen.Contains(type))
                throw new InvalidOperationException($"Character Screen Theme is missing stat {type}.");
        foreach (CharacterRarity rarity in (CharacterRarity[])Enum.GetValues(typeof(CharacterRarity)))
            GetRarity(rarity);
        foreach (CurrencyType currency in (CurrencyType[])Enum.GetValues(typeof(CurrencyType)))
            if (GetCurrencyIcon(currency) == null)
                throw new InvalidOperationException($"Character Screen Theme is missing icon for currency {currency}.");
    }

    #endregion
}
