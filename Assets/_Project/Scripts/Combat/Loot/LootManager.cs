using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Điều phối rơi/nhặt vật phẩm; phần thưởng được gửi tới manager gameplay tương ứng.
public sealed class LootManager : IInitializable, ILateTickable, IDisposable
{
    #region Dependencies & Run State

    private readonly LootDatabaseSO database; // Cấu hình prefab và bán kính nhặt từ asset chung.
    private readonly Dictionary<PickupType, LootDatabaseSO.LootEntry> drops; // Tra cấu hình từng loại, không giữ trạng thái trên asset.
    private readonly CharacterManager characters;
    private readonly PlayerManager players;
    private readonly LevelManager levels;
    private readonly PoolManager pools;
    private readonly RunSessionManager session; // Nhận thưởng vàng, không giao dữ liệu trực tiếp cho UI.
    private readonly CurrencyService currencies; // Ví vĩnh viễn: vàng được lưu ngay khi nhặt.
    private readonly List<PickupView> pickups = new List<PickupView>(128); // Vật phẩm còn trên mặt đất, tái sử dụng qua pool.
    private int runVersion; // Dừng lượt nhặt cũ nếu callback nhận EXP vừa reset trận.
    private bool disposed;

    public LootManager(LootDatabaseSO database, CharacterManager characters, PlayerManager players,
        LevelManager levels, PoolManager pools, RunSessionManager session, CurrencyService currencies)
    {
        this.database = database != null ? database : throw new ArgumentNullException(nameof(database));
        drops = database.CreateLookup();
        this.characters = characters;
        this.players = players;
        this.levels = levels;
        this.pools = pools;
        this.session = session;
        this.currencies = currencies;
    }

    #endregion

    #region Initialization & Enemy Drops

    public void Initialize()
    {
        characters.CharacterDied += DropEnemyLoot;
        levels.RunReset += ResetRun;
    }

    // CharacterDied chỉ phát khi chết thật; despawn hoặc tắt enemy không tạo phần thưởng.
    private void DropEnemyLoot(CharacterView enemy)
    {
        if (disposed || enemy == null || enemy.RuntimeData == null || enemy.RuntimeData.Team != CharacterTeam.Enemy) return;
        Vector3 position = enemy.transform.position; // Chốt vị trí trước khi factory trả enemy về pool.
        drops.TryGetValue(PickupType.Experience, out LootDatabaseSO.LootEntry experience);
        drops.TryGetValue(PickupType.Gold, out LootDatabaseSO.LootEntry gold);
        bool dropsExperience = ShouldDrop(experience);
        bool dropsGold = ShouldDrop(gold);
        if (dropsExperience) Spawn(experience, position + (dropsGold ? Vector3.left * 0.06f : Vector3.zero));
        if (dropsGold) Spawn(gold, position + (dropsExperience ? Vector3.right * 0.06f : Vector3.zero));
    }

    // Không có entry hoặc tỉ lệ bằng 0 thì bỏ qua; tỉ lệ 1 đảm bảo EXP rơi từ mọi enemy.
    private static bool ShouldDrop(LootDatabaseSO.LootEntry entry)
        => entry != null && entry.DropChance > 0f && (entry.DropChance >= 1f || UnityEngine.Random.value < entry.DropChance);

    private void Spawn(LootDatabaseSO.LootEntry entry, Vector3 position)
    {
        GameObject clone = pools.Spawn(database.PickupPrefab.gameObject, position, Quaternion.identity, PoolGroup.Other,
            beforeActivate: item => item.GetComponent<PickupView>().Initialize(entry.Type, entry.Amount, entry.Sprite));
        if (clone != null) pickups.Add(clone.GetComponent<PickupView>());
    }

    #endregion

    #region Pickup Collection

    // Chạy sau MovementManager để đo khoảng cách bằng vị trí mới; một vòng chung cho tất cả vật phẩm.
    public void LateTick()
    {
        CharacterView player = players.CurrentPlayer;
        if (disposed || Time.deltaTime <= 0f || player == null || !player.isActiveAndEnabled ||
            player.RuntimeData == null || !player.RuntimeData.IsAlive) return;
        Vector2 position = player.transform.position;
        float radiusSquared = database.PickupRadius * database.PickupRadius;
        int version = runVersion;
        for (int index = pickups.Count - 1; index >= 0; index--)
        {
            PickupView pickup = pickups[index];
            if (pickup != null && ((Vector2)pickup.transform.position - position).sqrMagnitude > radiusSquared) continue;
            PickupType type = pickup != null ? pickup.Type : default;
            int amount = pickup != null ? pickup.Amount : 0;
            // Gỡ và trả pool trước callback nhận thưởng, tránh nhặt hai lần trong callback lồng nhau.
            int last = pickups.Count - 1;
            pickups[index] = pickups[last];
            pickups.RemoveAt(last);
            if (pickup != null) pools.Despawn(pickup.gameObject);
            if (amount <= 0) continue;
            if (type == PickupType.Experience) levels.AddRunExperience(amount);
            else if (type == PickupType.Gold)
            {
                // Add đã cập nhật ví và thử lưu. IO lỗi giữ snapshot để retry, không cộng thưởng lần nữa.
                currencies.Add(CurrencyType.Gold, amount);
                if (disposed || runVersion != version) return;
                session.AddGold(amount); // Chỉ thống kê vàng kiếm trong trận, không quyết toán lại cuối trận.
            }
            if (disposed || runVersion != version) return;
        }
    }

    #endregion

    #region Run Reset & Cleanup

    private void ResetRun()
    {
        runVersion++;
        ClearPickups();
    }

    private void ClearPickups()
    {
        foreach (PickupView pickup in pickups)
            if (pickup != null) pools.Despawn(pickup.gameObject);
        pickups.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        characters.CharacterDied -= DropEnemyLoot;
        levels.RunReset -= ResetRun;
        ClearPickups();
    }

    #endregion
}
