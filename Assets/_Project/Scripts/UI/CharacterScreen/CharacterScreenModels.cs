using System;
using System.Collections.Generic;
using UnityEngine;

// Dữ liệu hiển thị của màn Character (Collection + Detail), gom vào một file vì đều là struct/enum thuần.
// Controller dựng các model này từ service + Theme rồi đẩy xuống View; View chỉ đọc, không gọi service.

#region Enums

/// <summary>Màn con đang mở trong màn Character.</summary>
public enum CharacterScreenPage { Collection, Detail }

/// <summary>Tab đang mở ở màn Detail. Mỗi tab ứng với một loại nâng cấp.</summary>
public enum CharacterScreenTab { Level, Stars }

/// <summary>Trạng thái một thẻ trong lưới Collection.</summary>
public enum CharacterCardState { Normal, Selected, Empty }

#endregion

#region Collection Models

/// <summary>Dữ liệu một thẻ nhân vật. Thẻ Empty chỉ để lấp cho đủ hàng.</summary>
public readonly struct CharacterCardModel
{
    public readonly Sprite Portrait; // null thì giữ ảnh mặc định của prefab.
    public readonly string Name;
    public readonly int Level;
    public readonly int Stars;
    public readonly CharacterCardState State;

    public CharacterCardModel(Sprite portrait, string name, int level, int stars, CharacterCardState state)
    {
        Portrait = portrait;
        Name = name;
        Level = level;
        Stars = stars;
        State = state;
    }

    /// <summary>Thẻ trống dùng để lấp hàng.</summary>
    public static CharacterCardModel Empty => new CharacterCardModel(null, string.Empty, 0, 0, CharacterCardState.Empty);
}

#endregion

#region Detail Models

/// <summary>Một ô chỉ số đã định dạng sẵn theo Theme.</summary>
public readonly struct StatRowModel
{
    public readonly Sprite Icon;
    public readonly string Value;

    public StatRowModel(Sprite icon, string value)
    {
        Icon = icon;
        Value = value;
    }
}

/// <summary>Một mục giá trên nút UPGRADE: icon tiền và số đã định dạng.</summary>
public readonly struct CostModel
{
    public readonly Sprite Icon;
    public readonly string Amount;

    public CostModel(Sprite icon, string amount)
    {
        Icon = icon;
        Amount = amount;
    }
}

/// <summary>Trạng thái nút UPGRADE của tab đang mở.</summary>
public readonly struct UpgradeButtonModel
{
    public readonly string Title; // "UPGRADE", "MAX", ...: Controller chọn theo lý do bị chặn.
    public readonly bool Interactable;
    public readonly IReadOnlyList<CostModel> Costs; // Rỗng thì ẩn vùng giá.

    public UpgradeButtonModel(string title, bool interactable, IReadOnlyList<CostModel> costs)
    {
        Title = title;
        Interactable = interactable;
        Costs = costs ?? Array.Empty<CostModel>();
    }
}

/// <summary>Toàn bộ dữ liệu màn Detail của một nhân vật.</summary>
public readonly struct CharacterDetailModel
{
    // Header
    public readonly Sprite RarityLabel;
    public readonly string RarityText;
    public readonly Color RarityTextColor;
    public readonly string Name;
    public readonly Sprite Portrait;
    public readonly int Stars;

    // Tab Level
    public readonly CharacterScreenTab Tab;
    public readonly int Level;
    public readonly int MaxLevel;
    public readonly IReadOnlyList<StatRowModel> Stats;
    public readonly IReadOnlyList<Sprite> SkillIcons;

    // Footer
    public readonly bool IsSelected;
    public readonly UpgradeButtonModel Upgrade;

    public CharacterDetailModel(
        Sprite rarityLabel, string rarityText, Color rarityTextColor, string name, Sprite portrait, int stars,
        CharacterScreenTab tab, int level, int maxLevel, IReadOnlyList<StatRowModel> stats, IReadOnlyList<Sprite> skillIcons,
        bool isSelected, UpgradeButtonModel upgrade)
    {
        RarityLabel = rarityLabel;
        RarityText = rarityText;
        RarityTextColor = rarityTextColor;
        Name = name;
        Portrait = portrait;
        Stars = stars;
        Tab = tab;
        Level = level;
        MaxLevel = maxLevel;
        Stats = stats ?? Array.Empty<StatRowModel>();
        SkillIcons = skillIcons ?? Array.Empty<Sprite>();
        IsSelected = isSelected;
        Upgrade = upgrade;
    }
}

#endregion
