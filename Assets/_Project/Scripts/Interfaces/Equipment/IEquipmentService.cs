using System;
using System.Collections.Generic;

#region EquipmentReader

/// <summary>Đọc ID trang bị, tổng bonus và sự kiện đổi loadout.</summary>
public interface IEquipmentReader
{
    event Action EquipmentChanged;
    IReadOnlyList<string> EquippedItemIds { get; }
    StatBonus TotalBonus { get; }
}

#endregion

#region EquipmentCommands

/// <summary>Thay đổi loadout hoặc thử lưu lại trang bị.</summary>
public interface IEquipmentCommands
{
    bool SetEquippedItems(IEnumerable<string> itemIds);
    bool SaveEquipment();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của PlayerEquipmentService; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IEquipmentService
{
    IEquipmentReader GetEquipmentReader();
    IEquipmentCommands GetEquipmentCommands();
}

#endregion
