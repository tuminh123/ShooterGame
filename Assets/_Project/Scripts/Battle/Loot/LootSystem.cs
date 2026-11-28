using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rơi và nhặt vật phẩm (EXP, vàng) trong lượt chơi. Chỉ quản lý vật phẩm trên mặt đất;
/// phần thưởng được báo qua <see cref="Collected"/> để <see cref="BattleManager"/> chuyển tới đúng nơi.
/// </summary>
/// <remarks>
/// Do <see cref="BattleManager"/> tạo; thay cho LootManager cũ.
/// Không biết InBattleLevel hay ví: các system anh em không gọi nhau, manager nối dây.
/// </remarks>
public sealed class LootSystem : IDisposable
{
    #region Fields

    private readonly LootDatabaseSO database; // Prefab, sprite, lượng thưởng và bán kính nhặt; không giữ trạng thái trên asset.
    private readonly Dictionary<PickupType, LootDatabaseSO.LootEntry> drops; // Tra cấu hình theo loại.
    private readonly IGameObjectPool pools;
    private readonly List<PickupView> pickups = new List<PickupView>(128); // Vật phẩm còn trên mặt đất.
    // Tăng mỗi lần dọn: nếu callback nhận thưởng làm reset lượt chơi thì dừng vòng nhặt cũ ngay.
    private int version;
    private bool disposed;

    #endregion

    #region Events

    /// <summary>(loại, lượng) của một vật phẩm vừa được nhặt. Vật phẩm đã được trả pool trước khi phát.</summary>
    public event Action<PickupType, int> Collected;

    #endregion

    #region Constructor / Lifecycle

    /// <summary>Tạo bảng tra loot từ database; kiểm tra cấu hình ngay để lỗi lộ ra lúc khởi động.</summary>
    public LootSystem(LootDatabaseSO database, IGameObjectPool pools)
    {
        this.database = database != null ? database : throw new ArgumentNullException(nameof(database));
        this.pools = pools ?? throw new ArgumentNullException(nameof(pools));
        drops = database.CreateLookup();
    }

    /// <summary>Thu hồi mọi vật phẩm còn lại và gỡ listener.</summary>
    public void Dispose()
    {
        if (disposed) return;
        Clear();
        disposed = true;
        Collected = null;
    }

    #endregion

    #region Drops

    /// <summary>Thả EXP/vàng tại vị trí quái vừa chết, mỗi loại theo tỉ lệ rơi riêng.</summary>
    public void DropAt(Vector3 position)
    {
        if (disposed) return;
        drops.TryGetValue(PickupType.Experience, out LootDatabaseSO.LootEntry experience);
        drops.TryGetValue(PickupType.Gold, out LootDatabaseSO.LootEntry gold);
        bool dropsExperience = ShouldDrop(experience);
        bool dropsGold = ShouldDrop(gold);
        // Rơi cả hai thì tách nhẹ sang hai bên để không chồng lên nhau.
        if (dropsExperience) Spawn(experience, position + (dropsGold ? Vector3.left * 0.06f : Vector3.zero));
        if (dropsGold) Spawn(gold, position + (dropsExperience ? Vector3.right * 0.06f : Vector3.zero));
    }

    // Không có entry hoặc tỉ lệ 0 thì bỏ qua; tỉ lệ 1 bảo đảm luôn rơi (không gọi Random).
    private static bool ShouldDrop(LootDatabaseSO.LootEntry entry)
        => entry != null && entry.DropChance > 0f && (entry.DropChance >= 1f || UnityEngine.Random.value < entry.DropChance);

    private void Spawn(LootDatabaseSO.LootEntry entry, Vector3 position)
    {
        GameObject clone = pools.Spawn(database.PickupPrefab.gameObject, position, Quaternion.identity, PoolGroup.Other,
            beforeActivate: item => item.GetComponent<PickupView>().Initialize(entry.Type, entry.Amount, entry.Sprite));
        if (clone != null) pickups.Add(clone.GetComponent<PickupView>());
    }

    #endregion

    #region Collection

    /// <summary>
    /// Nhặt mọi vật phẩm trong bán kính quanh <paramref name="playerPosition"/>.
    /// Phải chạy sau khi movement đã áp vị trí mới trong frame (LateTick).
    /// </summary>
    /// <param name="radiusMultiplier">Hệ số nhân bán kính nhặt của database (1 = như cấu hình; bonus PickupRange của Player).</param>
    public void Collect(Vector2 playerPosition, float radiusMultiplier = 1f)
    {
        if (disposed) return;
        float radius = database.PickupRadius * Mathf.Max(0f, radiusMultiplier);
        float radiusSquared = radius * radius;
        int startVersion = version;
        for (int index = pickups.Count - 1; index >= 0; index--)
        {
            PickupView pickup = pickups[index];
            if (pickup != null && ((Vector2)pickup.transform.position - playerPosition).sqrMagnitude > radiusSquared) continue;
            PickupType type = pickup != null ? pickup.Type : default;
            int amount = pickup != null ? pickup.Amount : 0;
            // Gỡ và trả pool TRƯỚC khi báo thưởng, để callback lồng nhau không nhặt cùng vật phẩm hai lần.
            int last = pickups.Count - 1;
            pickups[index] = pickups[last];
            pickups.RemoveAt(last);
            if (pickup != null) pools.Despawn(pickup.gameObject);
            if (amount <= 0) continue;
            Collected?.Invoke(type, amount);
            if (disposed || version != startVersion) return;
        }
    }

    #endregion

    #region Cleanup

    /// <summary>Thu hồi mọi vật phẩm trên mặt đất (lượt mới hoặc rời gameplay).</summary>
    public void Clear()
    {
        if (disposed) return;
        version++;
        foreach (PickupView pickup in pickups)
            if (pickup != null) pools.Despawn(pickup.gameObject);
        pickups.Clear();
    }

    #endregion
}
