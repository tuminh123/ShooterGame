using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Cấu hình hiển thị của màn Character: tên, icon và cách định dạng từng chỉ số, icon tiền.
/// Nhãn và màu theo độ hiếm nằm ở phần Character của <see cref="RarityStyleDatabaseSO"/> (theme chỉ tham chiếu).
/// Màn Equipment dùng chung phần chỉ số (qua EquipmentThemeSO) để mỗi chỉ số chỉ khai báo một chỗ.
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
        [Tooltip("Tên hiển thị (\"HP\", \"Damage\"...), dùng ở dòng sao và popup trang bị.")]
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private StatFormat format = StatFormat.Integer;

        public StatType Stat => stat;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public StatFormat Format => format;
    }

    /// <summary>Icon và tên hiển thị của một loại tiền, dùng cho giá trên nút UPGRADE, thanh mảnh và thông báo thiếu tiền.</summary>
    [Serializable]
    public sealed class CurrencyEntry
    {
        [SerializeField] private CurrencyType currency;
        [SerializeField] private Sprite icon;
        [Tooltip("Tên hiện cho người chơi (\"Gold\", \"Rare Shard\"...). Để trống thì dùng tên enum.")]
        [SerializeField] private string displayName;

        public CurrencyType Currency => currency;
        public Sprite Icon => icon;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? currency.ToString() : displayName;
    }

    #endregion

    #region Configuration

    [Tooltip("Đủ 8 StatType, mỗi loại một lần. Thứ tự = thứ tự StatRow_1..8 trên màn Detail.")]
    [SerializeField] private List<StatEntry> stats = new List<StatEntry>();
    [Tooltip("Màu và nhãn theo độ hiếm: sửa ở phần Character của asset RarityStyleDatabase (dùng chung một asset với màn Equipment).")]
    [SerializeField] private RarityStyleDatabaseSO rarityStyles;
    [Tooltip("Mỗi CurrencyType một mục.")]
    [SerializeField] private List<CurrencyEntry> currencies = new List<CurrencyEntry>();

    /// <summary>Danh sách chỉ số theo thứ tự hiển thị.</summary>
    public IReadOnlyList<StatEntry> Stats => stats;

    #endregion

    #region Lookups

    /// <summary>Tên, icon và định dạng của một chỉ số; ném lỗi nếu thiếu cấu hình (Validate đã chặn trước).</summary>
    public StatEntry GetStat(StatType stat)
    {
        foreach (StatEntry entry in stats)
        {
            if (entry != null && entry.Stat == stat)
                return entry;
        }
        throw new KeyNotFoundException($"Character Screen Theme is missing stat {stat}.");
    }

    /// <summary>Nhãn và màu của độ hiếm nhân vật (đọc từ RarityStyleDatabase); ném lỗi nếu thiếu (Validate đã chặn trước).</summary>
    public RarityStyleDatabaseSO.CharacterStyle GetRarity(CharacterRarity rarity) => rarityStyles.GetCharacter(rarity);

    /// <summary>Icon của loại tiền; null nếu thiếu.</summary>
    public Sprite GetCurrencyIcon(CurrencyType currency)
    {
        foreach (CurrencyEntry entry in currencies)
            if (entry != null && entry.Currency == currency) return entry.Icon;
        return null;
    }

    /// <summary>Tên hiển thị của loại tiền ("Gold", "Rare Shard"...); thiếu mục thì dùng tên enum.</summary>
    public string GetCurrencyName(CurrencyType currency)
    {
        foreach (CurrencyEntry entry in currencies)
            if (entry != null && entry.Currency == currency) return entry.DisplayName;
        return currency.ToString();
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

    /// <summary>Kiểm tra đủ mọi chỉ số gốc (không trùng, có tên), đủ mọi CharacterRarity và mọi CurrencyType.</summary>
    public void Validate()
    {
        var seen = new HashSet<StatType>();
        foreach (StatEntry entry in stats)
            if (entry == null || !seen.Add(entry.Stat) || string.IsNullOrWhiteSpace(entry.DisplayName))
                throw new InvalidOperationException("Character Screen Theme has an empty, duplicate or unnamed stat entry.");
        // Chỉ chỉ số gốc: màn Character không hiển thị bonus chiến đấu (chúng chỉ có trong trận, từ skill).
        foreach (StatType type in StatTypes.Base)
            if (!seen.Contains(type))
                throw new InvalidOperationException($"Character Screen Theme is missing stat {type}.");
        if (rarityStyles == null)
            throw new InvalidOperationException("Assign Rarity Styles (RarityStyleDatabase) on Character Screen Theme.");
        rarityStyles.Validate();
        foreach (CurrencyType currency in (CurrencyType[])Enum.GetValues(typeof(CurrencyType)))
            if (GetCurrencyIcon(currency) == null)
                throw new InvalidOperationException($"Character Screen Theme is missing icon for currency {currency}.");
    }

    #endregion
}
