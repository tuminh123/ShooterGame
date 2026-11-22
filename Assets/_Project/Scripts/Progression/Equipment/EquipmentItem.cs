using System;
using System.Collections.Generic;

/// <summary>
/// Một bản trang bị người chơi đang sở hữu, trong RAM khi game chạy: mẫu đồ, độ hiếm, level, chỉ số chính và các dòng phụ.
/// </summary>
/// <remarks>
/// Đi theo luồng Definition (<see cref="EquipmentDataSO"/>) → Runtime (class này) → View. Mẫu đồ và set được tra một lần lúc load,
/// chỉ số chính được tính sẵn, nên UI, <see cref="Loadout"/> và Combat chỉ đọc. Ra ngoài chỉ có getter;
/// chỉ <see cref="EquipmentInventory"/> (qua <see cref="EquipmentGrowth"/>) được đổi level và dòng phụ.
/// </remarks>
public sealed class EquipmentItem
{
    #region Fields

    private readonly List<StatModifier> substats;

    #endregion

    #region Properties

    /// <summary>Định danh của bản đồ này (phân biệt các bản cùng mẫu).</summary>
    public int Uid { get; }

    /// <summary>Mẫu đồ: tên, icon, ô, set, skill.</summary>
    public EquipmentDataSO Definition { get; }

    /// <summary>Set của món đồ; null nếu mẫu đồ không thuộc set nào.</summary>
    public EquipmentSetData SetData { get; }

    /// <summary>Độ hiếm của bản đồ này.</summary>
    public EquipmentRarity Rarity { get; }

    /// <summary>Level hiện tại (1..MaxLevel của luật).</summary>
    public int Level { get; private set; }

    /// <summary>Chỉ số chính, cố định theo ô.</summary>
    public StatType MainStat { get; }

    /// <summary>Giá trị chỉ số chính ở level hiện tại.</summary>
    public float MainValue { get; private set; }

    /// <summary>Các dòng phụ theo thứ tự mở (modifier Add).</summary>
    public IReadOnlyList<StatModifier> Substats => substats;

    #endregion

    #region Constructor

    internal EquipmentItem(int uid, EquipmentDataSO definition, EquipmentSetData setData, EquipmentRarity rarity,
        int level, StatType mainStat, float mainValue, List<StatModifier> substats)
    {
        Uid = uid;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        SetData = setData;
        Rarity = rarity;
        Level = level;
        MainStat = mainStat;
        MainValue = mainValue;
        this.substats = substats ?? throw new ArgumentNullException(nameof(substats));
    }

    #endregion

    #region Growth (chỉ Progression gọi)

    /// <summary>Đổi level và chỉ số chính tương ứng.</summary>
    internal void SetLevel(int level, float mainValue)
    {
        Level = level;
        MainValue = mainValue;
    }

    /// <summary>Danh sách dòng phụ để <see cref="EquipmentGrowth"/> mở/cộng/sửa tại chỗ.</summary>
    internal List<StatModifier> MutableSubstats => substats;

    #endregion

    #region Bonus And Save

    /// <summary>Tổng chỉ số món đồ cộng cho nhân vật: chỉ số chính + mọi dòng phụ.</summary>
    public StatBonus GetBonus()
    {
        StatBonus total = StatBonus.Of(MainStat, MainValue);
        foreach (StatModifier substat in substats)
            total += StatBonus.Of(substat.stat, substat.value);
        return total;
    }

    /// <summary>Bản ghi độc lập để lưu (không chia sẻ list với runtime).</summary>
    internal EquipmentItemsSaveData.Item ToSaveData()
    {
        var saved = new EquipmentItemsSaveData.Item { uid = Uid, itemId = Definition.Id, rarity = Rarity, level = Level };
        foreach (StatModifier substat in substats)
            saved.substats.Add(new EquipmentItemsSaveData.Substat { stat = substat.stat, value = substat.value });
        return saved;
    }

    #endregion
}
