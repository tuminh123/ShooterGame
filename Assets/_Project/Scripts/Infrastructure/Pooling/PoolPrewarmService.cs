using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Loading chuẩn bị pool theo batch; chỉ mở Home khi mọi GameObject và buffer đã sẵn sàng.
public sealed class PoolPrewarmService
{
    #region Dependencies And Progress

    private readonly PoolPrewarmSettingsSO settings;
    private readonly CharacterView characterPrefab;
    private readonly CharacterDatabaseSO characters;
    private readonly CombatAssetsSO combat;
    private readonly LootDatabaseSO loot;
    private readonly PoolManager objects;
    private readonly CharacterRuntimePool characterRuntimes;
    private readonly EffectRuntimePool effectRuntimes;
    private readonly MovementManager movement;
    private readonly ProjectileManager projectiles;
    private readonly ISpatialManager spatial;

    public bool IsReady { get; private set; }
    public int PreparedObjectCount { get; private set; }
    public float Progress { get; private set; }
    public event Action<float> ProgressChanged;

    #endregion

    #region Injection

    public PoolPrewarmService(PoolPrewarmSettingsSO settings, CharacterView characterPrefab,
        CharacterDatabaseSO characters, CombatAssetsSO combat, LootDatabaseSO loot, PoolManager objects,
        CharacterRuntimePool characterRuntimes, EffectRuntimePool effectRuntimes,
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
        this.movement = movement;
        this.projectiles = projectiles;
        this.spatial = spatial;
    }

    #endregion

    #region Loading Preparation

    // GameManager gọi ở bước khởi tạo; không tự prewarm/spawn ngoài luồng game.
    public async UniTask PrewarmAsync(CancellationToken cancellationToken)
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
        movement.PrewarmCapacity(checked(settings.characterShells + settings.bullets + settings.pickups));
        projectiles.PrewarmCapacity(settings.bullets);
        if (spatial is SpatialManager spatialManager) spatialManager.PrewarmCapacity(settings.characterShells);

        PreparedObjectCount = 0;
        foreach (KeyValuePair<GameObject, int> entry in plan)
        {
            await objects.PrewarmAsync(entry.Key, entry.Value, settings.objectsPerFrame, count =>
            {
                PreparedObjectCount += count;
                Progress = totalObjects == 0 ? 1f : (float)PreparedObjectCount / totalObjects;
                ProgressChanged?.Invoke(Progress);
            }, cancellationToken);
            await UniTask.NextFrame(cancellationToken: cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        IsReady = true; // Chỉ mở trạng thái sẵn sàng sau khi toàn bộ kế hoạch thành công.
        Progress = 1f;
        ProgressChanged?.Invoke(Progress);
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

    #endregion
}
