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
        [Tooltip("Loại phần thưởng được nhận khi nhặt; mỗi loại chỉ xuất hiện một lần trong catalog.")]
        [SerializeField] private PickupType type; // Khóa loại vật phẩm, không dùng tên sprite để nhận diện.
        [Tooltip("Sprite gán vào vỏ PickupView khi vật phẩm rơi; không cần tạo prefab riêng cho mỗi loại.")]
        [SerializeField] private Sprite sprite; // Hình ảnh được nạp vào vỏ pickup khi spawn.
        [Tooltip("Lượng EXP hoặc tiền nhận từ một vật phẩm; tối thiểu 1.")]
        [SerializeField, Min(1)] private int amount = 1; // Phần thưởng của một vật phẩm.
        [Tooltip("Xác suất rơi khi Enemy chết, thang 0..1: 0 = không rơi, 0.25 = 25%, 1 = luôn rơi.")]
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f; // 1: luôn rơi; 0: không rơi.

        public PickupType Type => type;
        public Sprite Sprite => sprite;
        public int Amount => amount;
        public float DropChance => dropChance;
    }

    [Tooltip("Danh sách loại loot và xác suất rơi riêng của từng loại; mỗi PickupType chỉ có một entry.")]
    [SerializeField] private List<LootEntry> drops = new List<LootEntry>(); // Mỗi loại xuất hiện tối đa một lần.

    #endregion

    #region Shared Pickup Configuration

    [Tooltip("Vỏ pickup dùng chung cho tất cả loại loot; sprite và lượng thưởng lấy từ entry.")]
    [SerializeField] private PickupView pickupPrefab; // Vỏ dùng chung cho tất cả entry.
    [Tooltip("Khoảng cách nhặt tính từ tâm Player đến vật phẩm, theo world unit; phải dương.")]
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
