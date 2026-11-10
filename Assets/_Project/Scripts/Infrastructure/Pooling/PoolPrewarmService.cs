using System;
using System.Collections.Generic;
using UnityEngine;

// GameManager gọi Prewarm trước khi cho spawn Player; IsReady là điều kiện bước vào gameplay.
// Hiện thực hiện đồng bộ: chi phí Instantiate/Inject được đưa về bước chuẩn bị, chưa chia frame/loading UI.
public sealed class PoolPrewarmService
{
    private readonly PoolPrewarmSettingsSO settings;
    private readonly CharacterView characterPrefab;
    private readonly CharacterDatabaseSO characters;
    private readonly CombatAssetsSO combat;
    private readonly LootDatabaseSO loot;
    private readonly PoolManager objects;
    private readonly CharacterRuntimePool characterRuntimes;
    private readonly EffectRuntimePool effectRuntimes;
    private readonly TimerManager timers;
    private readonly MovementManager movement;
    private readonly ProjectileManager projectiles;
    private readonly ISpatialManager spatial;

    public bool IsReady { get; private set; }
    public int PreparedObjectCount { get; private set; }

    public PoolPrewarmService(PoolPrewarmSettingsSO settings, CharacterView characterPrefab,
        CharacterDatabaseSO characters, CombatAssetsSO combat, LootDatabaseSO loot, PoolManager objects,
        CharacterRuntimePool characterRuntimes, EffectRuntimePool effectRuntimes, TimerManager timers,
        MovementManager movement, ProjectileManager projectiles, ISpatialManager spatial)
    {
        this.settings = settings;
        this.characterPrefab = characterPrefab;
        this.characters = characters;
        this.combat = combat;
        this.loot = loot;
        this.objects = objects;
        this.characterRuntimes = characterRuntimes;
        this.effectRuntimes = effectRuntimes;
        this.timers = timers;
        this.movement = movement;
        this.projectiles = projectiles;
        this.spatial = spatial;
    }

    // GameManager gọi ở bước khởi tạo; không tự prewarm/spawn ngoài luồng game.
    public void Prewarm()
    {
        if (IsReady) return;
        settings.Validate();
        var plan = new Dictionary<GameObject, int>();
        Add(plan, characterPrefab.gameObject, settings.characterShells);
        Add(plan, combat.BulletPrefab.gameObject, settings.bullets);
        Add(plan, loot.PickupPrefab != null ? loot.PickupPrefab.gameObject : null, settings.pickups);
        foreach (CharacterDataSO definition in characters.CreateLookup().Values)
        {
            int count = definition.Team == CharacterTeam.Player ? settings.playerVisuals :
                definition.IsBoss ? settings.bossVisuals : settings.enemyVisuals;
            Add(plan, definition.VisualPrefab, count);
        }
        foreach (CombatAssetsSO.EffectEntry effect in combat.EffectEntries)
        {
            Add(plan, combat.EffectPrefab.gameObject, settings.effectShells);
            Add(plan, effect.visualPrefab, settings.effectShells);
        }
        // Vỏ và visual là hai object có pool riêng; làm nóng cả visual để không Instantiate lúc bắn.
        foreach (CombatAssetsSO.BulletVisual bullet in combat.BulletEntries)
            Add(plan, bullet.visualPrefab, settings.bullets);
        foreach (PoolPrewarmSettingsSO.PrefabEntry entry in settings.additionalPrefabs)
            Add(plan, entry.prefab, entry.count);
        int totalObjects = 0;
        foreach (int count in plan.Values) totalObjects = checked(totalObjects + count);
        objects.ReserveActiveCapacity(totalObjects);

        characterRuntimes.Prewarm(settings.characterRuntimes);
        effectRuntimes.Prewarm<SlowEffectRuntime>(settings.slowRuntimes);
        effectRuntimes.Prewarm<DamageOverTimeEffectRuntime>(settings.damageOverTimeRuntimes);
        // Tạo sẵn cả vùng nhớ job: chỉ prewarm GameObject sẽ vẫn bị resize buffer trong trận đầu.
        timers.PrewarmCapacity(settings.timerCapacity);
        movement.PrewarmCapacity(checked(settings.characterShells + settings.bullets + settings.pickups));
        projectiles.PrewarmCapacity(settings.bullets);
        if (spatial is SpatialManager spatialManager) spatialManager.PrewarmCapacity(settings.characterShells);

        PreparedObjectCount = 0;
        foreach (KeyValuePair<GameObject, int> entry in plan)
        {
            objects.Prewarm(entry.Key, entry.Value);
            PreparedObjectCount += entry.Value;
        }
        IsReady = true; // Chỉ mở trạng thái sẵn sàng sau khi toàn bộ kế hoạch thành công.
        Debug.Log($"[PoolPrewarm] Ready: {PreparedObjectCount} objects, " +
            $"{characterRuntimes.AvailableCount} character runtimes; player can spawn.");
    }

    private static void Add(Dictionary<GameObject, int> plan, GameObject prefab, int count)
    {
        if (count == 0) return;
        if (prefab == null) throw new InvalidOperationException("Missing prefab in prewarm plan.");
        // Hai loại dùng chung prefab visual phải có đủ tổng số clone có thể cùng hoạt động.
        plan.TryGetValue(prefab, out int previous);
        plan[prefab] = checked(previous + count);
    }
}
