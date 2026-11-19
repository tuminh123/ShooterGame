using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Bước Loading: lập kế hoạch số lượng cho mọi miền rồi tạo sẵn pool GameObject và buffer theo batch;
/// chỉ mở Home khi mọi thứ đã sẵn sàng.
/// </summary>
/// <remarks>
/// Do <see cref="GameManager"/> tạo; thay cho PoolPrewarmService cũ. Thuộc miền Game vì kế hoạch cần dung lượng của
/// Character, Combat và Simulation — đặt trong Pool (nằm dưới các miền đó) sẽ tạo vòng phụ thuộc.
/// </remarks>
public sealed class GamePrewarm
{
    #region Dependencies And Progress

    private readonly PoolPrewarmSettingsSO settings;
    private readonly CharacterView characterPrefab;
    private readonly CharacterDatabaseSO characters;
    private readonly CombatAssetsSO combat;
    private readonly LootDatabaseSO loot;
    private readonly IPoolPreparation objects;
    private readonly ICharacterCapacity characterRuntimes; // Runtime nhân vật của miền Character.
    private readonly ICombatCapacity combatCapacity; // Runtime effect và buffer đạn của miền Combat.
    private readonly ISimulationCapacity simulationCapacity; // Buffer job di chuyển và lưới hitbox.

    public bool IsReady { get; private set; }
    public int PreparedObjectCount { get; private set; }
    public float Progress { get; private set; }
    public event Action<float> ProgressChanged;

    #endregion

    #region Injection

    public GamePrewarm(
        PoolPrewarmSettingsSO settings,
        CharacterView characterPrefab,
        CharacterDatabaseSO characters,
        CombatAssetsSO combat,
        LootDatabaseSO loot,
        IPoolPreparation poolPreparation,
        ICharacterCapacity characterCapacity,
        ICombatCapacity combatCapacity,
        ISimulationCapacity simulationCapacity)
    {
        this.settings = settings;
        this.characterPrefab = characterPrefab;
        this.characters = characters;
        this.combat = combat;
        this.loot = loot;
        this.objects = poolPreparation;
        this.characterRuntimes = characterCapacity;
        this.combatCapacity = combatCapacity;
        this.simulationCapacity = simulationCapacity;
    }

    #endregion

    #region Loading Preparation

    // GameManager gọi ở bước khởi tạo; không tự prewarm/spawn ngoài luồng game.
    public async UniTask PrewarmAsync(CancellationToken cancellationToken)
    {
        if (IsReady) return;
        settings.Validate();
        var plan = new Dictionary<GameObject, int>();
        Add(plan, characterPrefab.gameObject, settings.CharacterShells);
        Add(plan, combat.BulletPrefab.gameObject, settings.Bullets);
        Add(plan, loot.PickupPrefab != null ? loot.PickupPrefab.gameObject : null, settings.Pickups);
        foreach (CharacterDataSO definition in characters.CreateLookup().Values)
        {
            int count = definition.Team == CharacterTeam.Player ? settings.PlayerVisuals :
                definition.IsBoss ? settings.BossVisuals : settings.EnemyVisuals;
            Add(plan, definition.VisualPrefab, count);
        }
        foreach (CombatAssetsSO.EffectEntry effect in combat.EffectEntries)
        {
            Add(plan, combat.EffectPrefab.gameObject, settings.EffectShells);
            Add(plan, effect.VisualPrefab, settings.EffectShells);
        }
        // Vỏ và visual là hai object có pool riêng; làm nóng cả visual để không Instantiate lúc bắn.
        foreach (CombatAssetsSO.BulletVisual bullet in combat.BulletEntries)
            Add(plan, bullet.VisualPrefab, settings.Bullets);
        foreach (PoolPrewarmSettingsSO.PrefabEntry entry in settings.AdditionalPrefabs)
            Add(plan, entry.Prefab, entry.Count);
        int totalObjects = 0;
        foreach (int count in plan.Values) totalObjects = checked(totalObjects + count);
        objects.ReserveActiveCapacity(totalObjects);

        characterRuntimes.PrewarmRuntimes(settings.CharacterRuntimes);
        combatCapacity.PrewarmCapacity(settings.SlowRuntimes, settings.DamageOverTimeRuntimes, settings.Bullets);
        // Tạo sẵn cả vùng nhớ job: chỉ prewarm GameObject sẽ vẫn bị resize buffer trong trận đầu.
        simulationCapacity.PrewarmCapacity(
            movers: checked(settings.CharacterShells + settings.Bullets + settings.Pickups),
            hitboxes: settings.CharacterShells);

        PreparedObjectCount = 0;
        foreach (KeyValuePair<GameObject, int> entry in plan)
        {
            await objects.PrewarmAsync(entry.Key, entry.Value, settings.ObjectsPerFrame, count =>
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
            $"{characterRuntimes.AvailableRuntimes} character runtimes; player can spawn.");
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
