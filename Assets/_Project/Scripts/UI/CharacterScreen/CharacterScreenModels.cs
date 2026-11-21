using System;
using System.Collections.Generic;
using UnityEngine;

// Dữ liệu hiển thị của màn Character (Collection + Detail), gom vào một file vì đều là struct thuần (enum nằm ở Core/GameEnums.cs).
// Controller dựng các model này từ service + Theme rồi đẩy xuống View; View chỉ đọc, không gọi service.

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
    public readonly float Amount; // Giá trị số chưa định dạng, để View biết chỉ số tăng hay giảm.

    public StatRowModel(Sprite icon, string value, float amount)
    {
        Icon = icon;
        Value = value;
        Amount = amount;
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

/// <summary>Một dòng perk trên tab Stars.</summary>
public readonly struct StarPerkModel
{
    public readonly int Star; // Sao mở perk này, bắt đầu từ 1.
    public readonly Sprite Icon; // null thì giữ icon mặc định của dòng.
    public readonly string Description;
    public readonly StarPerkState State;

    public StarPerkModel(int star, Sprite icon, string description, StarPerkState state)
    {
        Star = star;
        Icon = icon;
        Description = description;
        State = state;
    }
}

/// <summary>Toàn bộ dữ liệu màn Detail của một nhân vật.</summary>
public readonly struct CharacterDetailModel
{
    // Header
    public readonly CharacterType Type; // Nhận ra đổi nhân vật (←/→) để không coi chênh lệch số giữa hai nhân vật là "tăng".
    public readonly Sprite RarityLabel;
    public readonly string RarityText;
    public readonly Color RarityTextColor;
    public readonly string Name;
    public readonly Sprite Portrait; // Ảnh dự phòng khi chưa có nhân vật UI.
    public readonly CharacterUIVisual UIVisual; // Prefab nhân vật UI (Idle); null thì hiện Portrait.
    public readonly int Stars;

    // Tab Level
    public readonly CharacterScreenTab Tab;
    public readonly int Level;
    public readonly int MaxLevel;
    public readonly int LevelProgress; // Nguyên liệu đã nạp vào thanh của cấp hiện tại.
    public readonly int LevelProgressRequired; // Cần để đầy thanh; 0 nghĩa là đã ở cấp tối đa.
    public readonly IReadOnlyList<StatRowModel> Stats;
    public readonly IReadOnlyList<Sprite> SkillIcons;

    // Tab Stars
    public readonly IReadOnlyList<StarPerkModel> StarPerks;

    // Footer
    public readonly bool IsSelected;
    public readonly UpgradeButtonModel Upgrade;

    public CharacterDetailModel(
        CharacterType type, Sprite rarityLabel, string rarityText, Color rarityTextColor, string name, Sprite portrait,
        CharacterUIVisual uiVisual, int stars,
        CharacterScreenTab tab, int level, int maxLevel, int levelProgress, int levelProgressRequired,
        IReadOnlyList<StatRowModel> stats, IReadOnlyList<Sprite> skillIcons, IReadOnlyList<StarPerkModel> starPerks,
        bool isSelected, UpgradeButtonModel upgrade)
    {
        Type = type;
        RarityLabel = rarityLabel;
        RarityText = rarityText;
        RarityTextColor = rarityTextColor;
        Name = name;
        Portrait = portrait;
        UIVisual = uiVisual;
        Stars = stars;
        Tab = tab;
        Level = level;
        MaxLevel = maxLevel;
        LevelProgress = levelProgress;
        LevelProgressRequired = levelProgressRequired;
        Stats = stats ?? Array.Empty<StatRowModel>();
        SkillIcons = skillIcons ?? Array.Empty<Sprite>();
        StarPerks = starPerks ?? Array.Empty<StarPerkModel>();
        IsSelected = isSelected;
        Upgrade = upgrade;
    }
}

#endregion
