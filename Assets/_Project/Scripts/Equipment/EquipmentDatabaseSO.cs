using System;
using System.Collections.Generic;
using UnityEngine;

// * Cấu hình trang bị theo ID ổn định; save chỉ giữ ID, bonus được đọc lại khi vào game.
[CreateAssetMenu(fileName = "EquipmentDatabase", menuName = "Shooter Game/Data/Equipment Database")]
public sealed class EquipmentDatabaseSO : ScriptableObject
{
    [Serializable]
    private sealed class EquipmentEntry
    {
        [Tooltip("ID ổn định được lưu trong save; phân biệt hoa/thường, không để trống hoặc trùng. Đổi ID có thể làm mất liên kết trang bị đã lưu.")]
        public string id;
        [Tooltip("Tên hiển thị của trang bị; độc lập ID nên có thể đổi mà không thay khóa save.")]
        public string displayName;
        [Tooltip("Bonus cộng vào chỉ số nhân vật khi trang bị được chọn. Các tỷ lệ dùng điểm phần trăm; dữ liệu này không giữ trạng thái đã trang bị.")]
        public StatBonus bonus;
    }

    [Tooltip("Danh mục trang bị theo ID. Mỗi entry khai báo tên và bonus riêng; không để phần tử trống.")]
    [SerializeField] private List<EquipmentEntry> items = new List<EquipmentEntry>();

    public Dictionary<string, StatBonus> CreateBonusLookup()
    {
        var lookup = new Dictionary<string, StatBonus>(StringComparer.Ordinal);
        foreach (EquipmentEntry item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                throw new InvalidOperationException("Equipment database contains a missing item or empty ID.");
            if (lookup.ContainsKey(item.id))
                throw new InvalidOperationException($"Duplicate equipment ID: {item.id}.");
            lookup.Add(item.id, item.bonus);
        }
        return lookup;
    }
}
