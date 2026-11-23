using System;
using System.Collections.Generic;
using UnityEngine;

// Dữ liệu hiển thị của màn Character (Collection + Detail), gom vào một file vì đều là struct thuần (enum nằm ở Core/GameEnums.cs).
// Controller dựng các model này từ service + Theme rồi đẩy xuống View; View chỉ đọc, không gọi service.

#region Collection Models

/// <summary>Thanh mảnh trên thẻ nhân vật chưa sở hữu: icon mảnh của nhân vật, số mảnh riêng đang có / số cần để ghép.</summary>
public readonly struct ShardBarModel
{
    public readonly Sprite Icon;
    public readonly long Current;
    public readonly int Required;

    /// <summary>Đủ mảnh để ghép: thanh và viền thẻ sáng lên, chạm vào là ghép.</summary>
    public bool Ready => Current >= Required;

    public ShardBarModel(Sprite icon, long current, int required)
    {
        Icon = icon;
        Current = current;
        Required = required;
    }
}

/// <summary>Dữ liệu một thẻ nhân vật. Thẻ Locked = chưa sở hữu (xám, thanh mảnh thay cho level/sao).</summary>
public readonly struct CharacterCardModel
{
    public readonly Sprite Portrait; // null thì giữ ảnh mặc định của prefab.
    public readonly string Name;
    public readonly int Level;
    public readonly int Stars;
    public readonly CharacterCardState State;
    public readonly RarityStyleDatabaseSO.CharacterStyle Rarity; // Nhãn + màu nền theo độ hiếm; null khi thẻ trống.
    public readonly ShardBarModel Shards; // Chỉ dùng khi State = Locked.

    public CharacterCardModel(Sprite portrait, string name, int level, int stars, CharacterCardState state,
        RarityStyleDatabaseSO.CharacterStyle rarity, ShardBarModel shards = default)
    {
        Portrait = portrait;
        Name = name;
        Level = level;
        Stars = stars;
        State = state;
        Rarity = rarity;
        Shards = shards;
    }

    /// <summary>Thẻ trống (không có nhân vật). Collection hiện đúng số nhân vật nên không còn dùng để lấp hàng.</summary>
    public static CharacterCardModel Empty => new CharacterCardModel(null, string.Empty, 0, 0, CharacterCardState.Empty, null);
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
    public readonly string Description; // Dòng ngắn trên tab (tên skill + cấp), chi tiết nằm trong Info.
    public readonly StarPerkState State;
    public readonly InfoPopupModel Info; // Nội dung popup khi chạm vào dòng (mô tả đầy đủ của từng skill sao này nâng).

    public StarPerkModel(int star, Sprite icon, string description, StarPerkState state, InfoPopupModel info)
    {
        Star = star;
        Icon = icon;
        Description = description;
        State = state;
        Info = info;
    }
}

/// <summary>Nội dung popup thông tin ở giữa màn Detail (dùng chung cho ô skill và dòng sao).</summary>
public readonly struct InfoPopupModel
{
    public readonly Sprite Icon; // Icon trong ô; null thì ẩn.
    public readonly string Title; // Tên skill hoặc "STAR n".
    public readonly string Status; // "Lv.3", "Unlocks at Lv 15", "Unlocked", "Locked"...
    public readonly bool Unlocked; // Tô màu dòng trạng thái: sáng khi đã mở, xám khi chưa.
    public readonly string Description; // Mô tả đầy đủ, có thể nhiều dòng.

    public InfoPopupModel(Sprite icon, string title, string status, bool unlocked, string description)
    {
        Icon = icon;
        Title = title;
        Status = status;
        Unlocked = unlocked;
        Description = description;
    }
}

/// <summary>Một ô skill trên tab Level: skill đã mở (hiện cấp) hoặc còn khóa (hiện điều kiện mở).</summary>
public readonly struct SkillSlotModel
{
    public readonly Sprite Icon; // null thì ẩn icon.
    public readonly bool Unlocked;
    public readonly string Label; // Nhãn dưới ô. Đã mở: cấp hiện tại ("Lv.3"); còn khóa: điều kiện ("Lv 15", "★2").
    public readonly InfoPopupModel Info; // Nội dung popup khi chạm vào ô.

    public SkillSlotModel(Sprite icon, bool unlocked, string label, InfoPopupModel info)
    {
        Icon = icon;
        Unlocked = unlocked;
        Label = label;
        Info = info;
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
    public readonly ImageStyle ScreenBackground; // Nền cả màn Detail theo độ hiếm (màu hoặc sprite).
    public readonly ImageStyle ScreenGlow; // Lớp phát sáng (PanelEffect/PanelGlow), cùng tông với nền.
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
    public readonly IReadOnlyList<SkillSlotModel> SkillSlots; // Đúng số skill của nhân vật, kể cả skill còn khóa.

    // Tab Stars
    public readonly IReadOnlyList<StarPerkModel> StarPerks;

    // Footer
    public readonly bool IsSelected;
    public readonly bool Owned; // Chưa sở hữu thì chỉ xem: ẩn nút chọn và nút nâng cấp.
    public readonly UpgradeButtonModel Upgrade;

    public CharacterDetailModel(
        CharacterType type, Sprite rarityLabel, string rarityText, Color rarityTextColor, ImageStyle screenBackground, ImageStyle screenGlow, string name, Sprite portrait,
        CharacterUIVisual uiVisual, int stars,
        CharacterScreenTab tab, int level, int maxLevel, int levelProgress, int levelProgressRequired,
        IReadOnlyList<StatRowModel> stats, IReadOnlyList<SkillSlotModel> skillSlots, IReadOnlyList<StarPerkModel> starPerks,
        bool isSelected, UpgradeButtonModel upgrade, bool owned)
    {
        Type = type;
        RarityLabel = rarityLabel;
        RarityText = rarityText;
        RarityTextColor = rarityTextColor;
        ScreenBackground = screenBackground;
        ScreenGlow = screenGlow;
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
        SkillSlots = skillSlots ?? Array.Empty<SkillSlotModel>();
        StarPerks = starPerks ?? Array.Empty<StarPerkModel>();
        IsSelected = isSelected;
        Owned = owned;
        Upgrade = upgrade;
    }
}

#endregion
