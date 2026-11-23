using UnityEngine;

// Dữ liệu hiển thị của màn Equipment, gom vào một file vì đều là struct thuần.
// EquipmentScreenController dựng các model từ IEquipment + Theme rồi đẩy xuống View; View chỉ đọc, không gọi service.

#region Card

/// <summary>Một thẻ đồ (ô mặc, lưới túi đồ, popup). Thẻ trống chỉ hiện hình mờ của ô.</summary>
public readonly struct EquipmentCardModel
{
    public readonly bool IsEmpty;
    public readonly Sprite Icon;
    public readonly string LevelText;
    public readonly Sprite SlotIcon;
    public readonly Sprite EmptyIcon;
    public readonly RarityStyleDatabaseSO.EquipmentStyle Rarity; // null khi thẻ trống.

    public EquipmentCardModel(bool isEmpty, Sprite icon, string levelText, Sprite slotIcon, Sprite emptyIcon,
        RarityStyleDatabaseSO.EquipmentStyle rarity)
    {
        IsEmpty = isEmpty;
        Icon = icon;
        LevelText = levelText;
        SlotIcon = slotIcon;
        EmptyIcon = emptyIcon;
        Rarity = rarity;
    }
}

#endregion

#region Loadout

/// <summary>Phần trên của màn: nhân vật đang chọn (đứng Idle), 6 ô mặc và danh sách set đang mặc.</summary>
public readonly struct EquipmentLoadoutModel
{
    public readonly CharacterUIVisual UIVisual; // Nhân vật UI đứng Idle; null thì hiện ảnh tĩnh.
    public readonly Sprite Portrait; // Ảnh dự phòng khi nhân vật chưa có prefab UI.
    public readonly string CharacterName;
    public readonly EquipmentCardModel[] Slots; // Theo thứ tự EquipmentSlot.
    public readonly string Sets; // Mỗi set một dòng; trống nếu không mặc món nào thuộc set.
    public readonly bool CanUnequipAll;

    public EquipmentLoadoutModel(CharacterUIVisual uiVisual, Sprite portrait, string characterName, EquipmentCardModel[] slots,
        string sets, bool canUnequipAll)
    {
        UIVisual = uiVisual;
        Portrait = portrait;
        CharacterName = characterName;
        Slots = slots;
        Sets = sets;
        CanUnequipAll = canUnequipAll;
    }
}

#endregion

#region Popup

/// <summary>Một dòng chỉ số trong popup: icon, tên, giá trị và chữ phụ (xem trước "+X" hoặc điều kiện mở).</summary>
public readonly struct EquipmentStatLineModel
{
    public readonly Sprite Icon;
    public readonly string Name;
    public readonly string Value;
    public readonly string Extra;
    public readonly bool Locked;

    public EquipmentStatLineModel(Sprite icon, string name, string value, string extra, bool locked)
    {
        Icon = icon;
        Name = name;
        Value = value;
        Extra = extra;
        Locked = locked;
    }
}

/// <summary>Khối Skill hoặc khối Set trong popup. Visible = false thì ẩn cả khối.</summary>
public readonly struct EquipmentBonusBlockModel
{
    public readonly bool Visible;
    public readonly Sprite Icon;
    public readonly string Text;
    public readonly bool Active;

    public EquipmentBonusBlockModel(bool visible, Sprite icon, string text, bool active)
    {
        Visible = visible;
        Icon = icon;
        Text = text;
        Active = active;
    }

    /// <summary>Khối bị ẩn.</summary>
    public static EquipmentBonusBlockModel Hidden => new EquipmentBonusBlockModel(false, null, string.Empty, false);
}

/// <summary>Toàn bộ nội dung popup chi tiết của một món đồ.</summary>
public readonly struct EquipmentInfoModel
{
    public readonly Sprite SlotIcon;
    public readonly string SlotText;
    public readonly string Title;
    public readonly string RarityText;
    public readonly Color RarityColor;
    public readonly EquipmentCardModel Card;
    public readonly string Description;
    public readonly EquipmentStatLineModel MainStat;
    public readonly EquipmentStatLineModel[] Substats; // Luôn đủ số dòng tối đa; dòng chưa mở có Locked = true.
    public readonly EquipmentBonusBlockModel Skill;
    public readonly EquipmentBonusBlockModel Set;
    public readonly string Hint; // Gợi ý mốc của cấp tới; trống nếu không phải mốc.
    public readonly string EquipLabel;
    public readonly string UpgradeLabel;
    public readonly bool UpgradeInteractable; // Còn bấm được khi thiếu tiền (bấm thì rung + báo); tắt khi MAX.

    public EquipmentInfoModel(Sprite slotIcon, string slotText, string title, string rarityText, Color rarityColor,
        EquipmentCardModel card, string description, EquipmentStatLineModel mainStat, EquipmentStatLineModel[] substats,
        EquipmentBonusBlockModel skill, EquipmentBonusBlockModel set, string hint, string equipLabel, string upgradeLabel, bool upgradeInteractable)
    {
        SlotIcon = slotIcon;
        SlotText = slotText;
        Title = title;
        RarityText = rarityText;
        RarityColor = rarityColor;
        Card = card;
        Description = description;
        MainStat = mainStat;
        Substats = substats;
        Skill = skill;
        Set = set;
        Hint = hint;
        EquipLabel = equipLabel;
        UpgradeLabel = upgradeLabel;
        UpgradeInteractable = upgradeInteractable;
    }
}

#endregion
