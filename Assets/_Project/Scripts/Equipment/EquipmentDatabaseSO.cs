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
        public string id;
        public string displayName;
        public StatBonus bonus;
    }

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
