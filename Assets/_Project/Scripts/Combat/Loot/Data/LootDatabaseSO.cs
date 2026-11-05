using System;
using System.Collections.Generic;
using UnityEngine;

// Catalog loot dùng chung: asset chỉ giữ cấu hình, không lưu vật phẩm đang rơi hoặc vàng của trận.
[CreateAssetMenu(fileName = "LootDatabase", menuName = "Shooter Game/Data/Loot Database")]
public sealed class LootDatabaseSO : ScriptableObject
{
    #region Drop Entries

    [Serializable]
    public sealed class LootEntry
    {
        [SerializeField] private PickupType type; // Khóa loại vật phẩm, không dùng tên sprite để nhận diện.
        [SerializeField] private Sprite sprite; // Hình ảnh được nạp vào vỏ pickup khi spawn.
        [SerializeField, Min(1)] private int amount = 1; // Phần thưởng của một vật phẩm.
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f; // 1: luôn rơi; 0: không rơi.

        public PickupType Type => type;
        public Sprite Sprite => sprite;
        public int Amount => amount;
        public float DropChance => dropChance;
    }

    [SerializeField] private List<LootEntry> drops = new List<LootEntry>(); // Mỗi loại xuất hiện tối đa một lần.

    #endregion

    #region Shared Pickup Configuration

    [SerializeField] private PickupView pickupPrefab; // Vỏ dùng chung cho tất cả entry.
    [SerializeField, Min(0.01f)] private float pickupRadius = 0.25f; // Bán kính nhặt tính từ tâm Player, theo world unit.

    public PickupView PickupPrefab => pickupPrefab;
    public float PickupRadius => pickupRadius;

    #endregion

    #region Runtime Lookup Creation

    // Tạo dictionary cho manager, giống database Character/Equipment; không cache trạng thái lên asset.
    public Dictionary<PickupType, LootEntry> CreateLookup()
    {
        if (pickupPrefab == null || !(pickupRadius > 0f) || float.IsInfinity(pickupRadius))
            throw new InvalidOperationException("Loot database requires a pickup prefab and a positive finite pickup radius.");
        var lookup = new Dictionary<PickupType, LootEntry>(drops.Count);
        foreach (LootEntry entry in drops)
        {
            if (entry == null || !Enum.IsDefined(typeof(PickupType), entry.Type) || entry.Sprite == null ||
                entry.Amount < 1 || float.IsNaN(entry.DropChance) || entry.DropChance < 0f || entry.DropChance > 1f)
                throw new InvalidOperationException("Loot database contains an invalid drop entry.");
            if (lookup.ContainsKey(entry.Type))
                throw new InvalidOperationException($"Duplicate pickup type: {entry.Type}.");
            lookup.Add(entry.Type, entry);
        }
        return lookup;
    }

    #endregion
}
