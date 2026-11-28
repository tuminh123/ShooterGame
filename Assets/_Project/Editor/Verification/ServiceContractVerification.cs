using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zenject;

/// <summary>
/// Kiểm tra kiến trúc và binding thực trong scene, không cấp tiền hoặc ghi tiến trình.
/// Edit Mode: quy tắc contract. Play Mode (sau Home): mỗi miền một Manager, lifecycle không trùng/thiếu, và smoke test luồng chính.
/// Đồ thị dependency đầy đủ kiểm bằng Edit > Zenject > Validate Current Scenes. Xem Docs/Tools/Verification.md.
/// </summary>
public static class ServiceContractVerification
{
    #region Contract Structure

    [MenuItem("Shooter Game/Verification/Check Service Contracts")]
    public static void CheckContracts()
    {
        var assembly = typeof(CharacterView).Assembly;
        var interfaces = assembly.GetTypes().Where(t => t.IsInterface && t.IsPublic && t.Namespace == null).ToArray();
        foreach (Type contract in interfaces)
        {
            MethodInfo[] methods = contract.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName).ToArray();
            bool isFacade = methods.Length > 0 && methods.All(method => method.Name.StartsWith("Get", StringComparison.Ordinal) &&
                method.GetParameters().Length == 0 && method.ReturnType.IsInterface);
            // Facade tổng hợp (chỉ có getter trả về interface khác) đã bị bỏ ở Giai đoạn 6: miền khác inject thẳng
            // vai trò trong XxxContracts.cs. Chặn để kiểu cũ không quay lại.
            Require(!isFacade, contract.Name + " is an aggregate facade; expose roles in the domain's XxxContracts.cs instead.");
        }
        CheckNoSelfTickRegistration(assembly);
        Debug.Log($"[ServiceContracts] PASS: {interfaces.Length} public contracts, no self tick registration.");
    }

    // Mọi thứ tự frame nằm ở installer (GameSceneInstaller/UIInstaller). Code game giữ TickableManager nghĩa là
    // đang tự AddLate(this, số) với số tự nghĩ ra; Manager tick hộ View thay vì để View tự đăng ký.
    private static void CheckNoSelfTickRegistration(Assembly assembly)
    {
        const BindingFlags allFields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        foreach (Type type in assembly.GetTypes())
            foreach (FieldInfo field in type.GetFields(allFields))
            {
                Type fieldType = field.FieldType;
                bool holdsTickables = fieldType == typeof(TickableManager) ||
                    (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(LazyInject<>) &&
                     fieldType.GetGenericArguments()[0] == typeof(TickableManager));
                if (holdsTickables) offenders.Add(type.Name + "." + field.Name);
            }
        Require(offenders.Count == 0, "Self tick registration (declare the order in an installer instead): " +
            string.Join(", ", offenders));
    }

    [MenuItem("Shooter Game/Verification/Report Missing Scripts")]
    public static void ReportMissingScripts()
    {
        int count = 0;
        foreach (GameObject item in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item);
            if (missing == 0) continue;
            string path = item.name;
            for (Transform parent = item.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(item);
            string asset = AssetDatabase.GetAssetPath(source != null ? source : item);
            Debug.LogWarning($"[ServiceContracts] Missing {missing} script(s): {path}; asset={asset}", item);
            count += missing;
        }
        Debug.Log($"[ServiceContracts] Missing-script scan: {count} missing component(s) in loaded objects/assets.");
    }

    #endregion

    #region Scene Binding And Lifecycle

    [MenuItem("Shooter Game/Verification/Run Service Smoke Checks")]
    public static void RunSceneChecks()
    {
        Require(Application.isPlaying, "Enter Play Mode before running scene checks.");
        double deadline = EditorApplication.timeSinceStartup + 180d;
        EditorApplication.CallbackFunction waitForHome = null;
        waitForHome = () =>
        {
            try
            {
                var context = UnityEngine.Object.FindObjectOfType<SceneContext>();
                IGameState state = context != null ? context.Container.TryResolve<IGameState>() : null;
                if (Application.isPlaying && state != null && state.State == GameState.Boot && EditorApplication.timeSinceStartup < deadline)
                    return;
                EditorApplication.update -= waitForHome;
                RunReadySceneChecks();
            }
            catch (Exception exception)
            {
                EditorApplication.update -= waitForHome;
                Debug.LogException(exception);
            }
        };
        EditorApplication.update += waitForHome;
        Debug.Log("[ServiceContracts] Scene checks queued; waiting for pool prewarm and Home.");
    }

    private static void RunReadySceneChecks()
    {
        Require(Application.isPlaying, "Enter Play Mode and wait for Home before running scene checks.");
        var context = UnityEngine.Object.FindObjectOfType<SceneContext>();
        Require(context != null, "No SceneContext in the loaded scene.");
        DiContainer container = context.Container;
        var state = container.Resolve<IGameState>();
        Require(state.State == GameState.Home, "Scene must be at Home; actual state: " + state.State);
        CheckContracts();
        CheckBindings(container);

        var commands = container.Resolve<IGameFlow>();
        var playerReader = container.Resolve<IPlayerReader>();
        var characterDespawn = container.Resolve<ICharacterDespawn>();
        var stageLifecycle = container.Resolve<IStageBattleLifecycle>();
        var pauses = container.Resolve<IGamePause>();
        using (pauses.Acquire())
        {
            try
            {
                Require(commands.StartBattle(), "StartBattle failed.");
                Require(state.State == GameState.Playing && playerReader.RuntimeData.IsAlive, "Player did not enter Playing alive.");
                CharacterView player = playerReader.CurrentPlayer;
                CharacterRuntimeData oldRuntime = player.RuntimeData;
                ulong oldGeneration = oldRuntime.Generation;

                // Dừng lịch Stage để kiểm tra đồng bộ; token pause ngăn nhặt loot và ghi ví.
                stageLifecycle.StopBattle();
                CheckMovement(container, player);
                CheckSkills(container, player);
                CheckEffectsAndDamage(container, player, characterDespawn);

                using (pauses.Acquire())
                    Require(Time.timeScale == 0f, "Nested pause did not hold gameplay.");
                Require(Time.timeScale == 0f, "Releasing one lease incorrectly resumed another lease.");

                Require(commands.RestartBattle(), "RestartBattle failed.");
                Require(playerReader.RuntimeData.IsAlive, "Restart did not create a living player.");
                Require(!ReferenceEquals(oldRuntime, playerReader.RuntimeData) || oldGeneration != oldRuntime.Generation,
                    "Restart reused a runtime without changing its generation.");
                var owned = new List<SkillRuntime>();
                container.Resolve<IOwnedSkillReader>().GetOwnedSkills(playerReader.CurrentPlayer, owned);
                Require(owned.Count == 0, "Restart retained gameplay skills.");
                IBattleProgress battle = container.Resolve<IBattleProgress>();
                Require(battle.Level == 1 && battle.Experience == 0, "Restart did not reset battle level.");
                Require(battle.Gold == 0 && battle.EnemiesDefeated == 0, "Restart did not reset battle statistics.");
                Require(commands.ReturnHome(), "ReturnHome failed.");
                Require(state.State == GameState.Home && playerReader.CurrentPlayer == null, "Home retained the player.");
            }
            finally
            {
                if (state.State == GameState.Playing) commands.ReturnHome();
            }
        }
        Require(Time.timeScale == 0f, "Home lost its flow pause.");
        Debug.Log("[ServiceContracts] PASS: domain managers and singleton roles, lifecycle registration, movement, search, projectiles, skills, effects, damage, stale handles, pause, restart and Home. No result rewards granted.");
    }

    // Mỗi miền: mọi vai trò resolve về cùng một Manager, không bind concrete, lifecycle đăng ký đúng một lần.
    private static void CheckBindings(DiContainer container)
    {
        CheckDomainManager(container, typeof(CombatManager), typeof(ISkillGrant), typeof(IOwnedSkillReader),
            typeof(ICombatLifecycle), typeof(ICombatCapacity));
        CheckDomainManager(container, typeof(SimulationManager), typeof(IMovementRegistration), typeof(IMovementCommand),
            typeof(IMovementArena), typeof(ISpatialRegistration), typeof(ISpatialInvalidation), typeof(ISpatialResolution),
            typeof(ISpatialQuery), typeof(ISpatialQueryBuilder), typeof(ISimulationCapacity));
        CheckDomainManager(container, typeof(CharacterManager), typeof(ICharacterSpawn), typeof(ICharacterDespawn),
            typeof(ICharacterRegistry), typeof(ICharacterDeathEvents), typeof(IPlayerReader), typeof(IPlayerLifecycle),
            typeof(IPlayerSelection), typeof(ICharacterCapacity));
        CheckDomainManager(container, typeof(WorldManager), typeof(IWorldMap));
        CheckDomainManager(container, typeof(StageManager), typeof(IStageBattleLifecycle), typeof(IStageSelection),
            typeof(IStageResults), typeof(IStageSchedule));
        CheckDomainManager(container, typeof(BattleManager), typeof(IBattleProgress), typeof(IBattleReset), typeof(ISkillChoice));
        CheckDomainManager(container, typeof(GameManager), typeof(IGameState), typeof(IGameFlow), typeof(IGamePause), typeof(ILoadingProgress));
        // Dịch vụ UI dùng chung duy nhất được bind; là MonoBehaviour trong scene nên không đăng ký lifecycle của Zenject.
        Require(container.Resolve<IScreenTransition>() is ScreenTransitionController, "IScreenTransition must resolve to ScreenTransitionController.");
        Require(!container.HasBinding<ScreenTransitionController>(), "Unexpected concrete binding: ScreenTransitionController");
        CheckDomainManager(container, typeof(SaveManager), typeof(ISaveStore), typeof(ISaveMaintenance));
        CheckDomainManager(container, typeof(ClockManager), typeof(IGameClock), typeof(IClockSync));
        CheckDomainManager(container, typeof(ProgressionManager), typeof(IWallet), typeof(IPermanentBonuses), typeof(ICharacterProgress),
            typeof(IStageProgress), typeof(ICharacterUpgrades), typeof(ICharacterCollection), typeof(IEquipment), typeof(IEquipmentGrant), typeof(IRewardGrant));
        CheckDomainManager(container, typeof(MissionManager), typeof(IMissions));
        CheckDomainManager(container, typeof(LoginCheckInManager), typeof(ILoginCheckIn));
        CheckDomainManager(container, typeof(ShopManager), typeof(IShop));
        CheckDomainManager(container, typeof(GachaManager), typeof(IGacha));
        CheckDomainManager(container, typeof(NotificationManager), typeof(INotifications));
        CheckDomainManager(container, typeof(PoolManager), typeof(IGameObjectPool), typeof(IPoolPreparation), typeof(IPoolCleanup));
    }

    // Kiến trúc mới: một manager theo miền, các vai trò của nó đều resolve về cùng một instance,
    // không bind concrete, và đăng ký Tick/Dispose đúng một lần. System con không có binding nào.
    private static void CheckDomainManager(DiContainer container, Type manager, params Type[] roles)
    {
        object instance = container.Resolve(roles[0]);
        Require(manager.IsInstanceOfType(instance), roles[0].Name + " must resolve to " + manager.Name);
        foreach (Type role in roles)
            Require(ReferenceEquals(container.Resolve(role), instance), role.Name + " resolved to a different instance than " + manager.Name);
        Require(!container.HasBinding(manager), "Unexpected concrete binding: " + manager.Name);
        if (instance is IInitializable)
            Require(container.ResolveAll<IInitializable>().Count(item => ReferenceEquals(item, instance)) == 1, "Duplicate/absent initialization: " + manager.Name);
        if (instance is ITickable)
            Require(container.ResolveAll<ITickable>().Count(item => ReferenceEquals(item, instance)) == 1, "Duplicate/absent tick registration: " + manager.Name);
        if (instance is IDisposable)
            Require(container.ResolveAll<IDisposable>().Count(item => ReferenceEquals(item, instance)) == 1, "Duplicate/absent disposal: " + manager.Name);
    }

    // Smoke test cần gọi thẳng system bên trong một manager (ví dụ áp effect thủ công).
    // Production không lộ các system này, nên tool kiểm tra lấy chúng qua reflection.
    private static T GetManagerSystem<TRole, T>(DiContainer container, string fieldName)
    {
        object manager = container.Resolve(typeof(TRole));
        FieldInfo field = manager.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null && typeof(T).IsAssignableFrom(field.FieldType), manager.GetType().Name + " has no system field " + fieldName);
        return (T)field.GetValue(manager);
    }

    #endregion

    #region Character Integration

    private static void CheckMovement(DiContainer container, CharacterView player)
    {
        Vector3 before = player.transform.position;
        player.Move(Vector2.right, 2f);
        // Mô phỏng thủ công khi toàn scene đang pause; scheduler không chạy thêm bước gameplay.
        GetManagerSystem<IMovementCommand, MovementSystem>(container, "movement").Simulate(0.1f);
        Require(player.transform.position.x > before.x, "Movement command did not reach the movement job.");
        player.Move(Vector2.zero, 0f);
    }

    private static void CheckSkills(DiContainer container, CharacterView player)
    {
        var grant = container.Resolve<ISkillGrant>();
        var reader = container.Resolve<IOwnedSkillReader>();
        SkillDataSO definition = container.Resolve<SkillDatabaseSO>().CreateBattleChoiceCatalog().First();
        Require(grant.GrantOrUpgrade(definition, player), "Could not grant a gameplay skill.");
        Require(reader.TryGetRuntime(definition, player, out SkillRuntime runtime), "Granted skill cannot be read.");
        if (definition.MaxBattleLevel > 1)
        {
            Require(grant.GrantOrUpgrade(definition, player), "Could not upgrade the gameplay skill.");
            Require(runtime.BattleLevel == 2, "Skill did not advance to level two.");
        }
    }

    private static void CheckEffectsAndDamage(DiContainer container, CharacterView player, ICharacterDespawn release)
    {
        CharacterDataSO definition = container.Resolve<CharacterDatabaseSO>().CreateLookup().Values
            .First(item => item.Team == CharacterTeam.Enemy && !item.IsBoss);
        CharacterView enemy = GetManagerSystem<IStageBattleLifecycle, EnemySpawner>(container, "spawner").Spawn(definition, 1,
            (Vector2)player.transform.position + Vector2.right * 3f);
        var target = new CharacterHandle(enemy);
        SpatialHandle spatialHandle = enemy.SpatialHandle;
        var registration = container.Resolve<ISpatialRegistration>();
        EffectSystem effects = GetManagerSystem<ICombatLifecycle, EffectSystem>(container, "effects");
        EffectSystem handles = effects; // Cùng một system áp effect và quản lý handle.
        try
        {
            Require(registration.IsRegistered(spatialHandle), "Enemy did not register its hitbox.");
            Require(player.Search.FindNearest(enemy.HitPosition, 0.1f, CharacterTeam.Enemy).TryGetCharacter(out _),
                "Character search did not resolve the enemy.");
            CheckProjectile(container, player, enemy);
            float speed = enemy.RuntimeData.FinalStats.moveSpeed;
            EffectHandle slow = effects.ApplyMaintained(new SlowEffect(0.2f), target, CharacterTeam.Player);
            Require(handles.IsActive(slow), "Maintained slow did not activate.");
            Require(enemy.RuntimeData.FinalStats.moveSpeed <= speed, "Slow increased movement speed.");
            handles.Remove(slow);
            Require(!handles.IsActive(slow), "Removed effect handle remained active.");
            EffectHandle replacement = effects.ApplyMaintained(new SlowEffect(0.1f), target, CharacterTeam.Player);
            handles.Remove(slow);
            Require(handles.IsActive(replacement), "A stale effect handle removed a reused runtime.");

            DamageResult hit = enemy.ReceiveDamage(new DamageInfo(enemy.RuntimeData.CurrentHealth + enemy.RuntimeData.CurrentShield + 1f,
                CharacterTeam.Player));
            Require(hit.Killed && !target.TryGetCharacter(out _), "Lethal damage did not end the target lifetime.");
            Require(!registration.IsRegistered(spatialHandle), "Dead enemy retained its hitbox.");
            Require(!handles.IsActive(replacement), "Death retained the owner's effect.");
        }
        finally { release.Despawn(enemy); }
        Require(!container.Resolve<ISpatialResolution>().TryResolve(spatialHandle, out _), "Released spatial handle resolved a character.");
    }

    // Gọi đúng pipeline production dưới pause để quét đạn mà không làm đồng hồ scene chạy thêm.
    private static void CheckProjectile(DiContainer container, CharacterView player, CharacterView enemy)
    {
        float before = enemy.RuntimeData.CurrentHealth + enemy.RuntimeData.CurrentShield;
        // Chỉ số tự tạo, không chí mạng: sát thương cố định nhỏ để mục tiêu chắc chắn còn sống.
        BaseStats stats = default;
        stats.damage = Mathf.Min(0.1f, before * 0.1f);
        var payload = new HitPayload(default, stats, default, CharacterTeam.Player, 1f, Array.Empty<IGameplayEffect>());
        var data = new ProjectileSpawnData(container.Resolve<CombatAssetsSO>().BulletEntries[0].Type,
            in payload, 50f, 1f, new StraightProjectileMove());
        Vector2 direction = (enemy.HitPosition - player.HitPosition).normalized;
        ProjectileSystem projectiles = GetManagerSystem<ICombatLifecycle, ProjectileSystem>(container, "projectiles");
        Require(projectiles.SpawnBullet(in data, player.HitPosition, direction, default),
            "Projectile spawner failed.");
        ProjectileView projectile = UnityEngine.Object.FindObjectsOfType<ProjectileView>().Single();
        MethodInfo prepare = typeof(ProjectileView).GetMethod("PrepareQuery", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo complete = typeof(ProjectileView).GetMethod("CompleteQuery", BindingFlags.Instance | BindingFlags.NonPublic);
        object[] arguments = { 0.1f, default(SpatialQuery) };
        Require((bool)prepare.Invoke(projectile, arguments), "Projectile did not prepare a sweep query.");
        SpatialHit hit = container.Resolve<ISpatialQuery>().Query((SpatialQuery)arguments[1]);
        Require(container.Resolve<ISpatialResolution>().TryResolve(hit, out CharacterHandle target), "Projectile sweep missed the enemy.");
        complete.Invoke(projectile, new object[] { target, new Vector2(hit.Position.x, hit.Position.y) });
        Require(enemy.RuntimeData.CurrentHealth + enemy.RuntimeData.CurrentShield < before, "Projectile hit did not apply damage.");
        Require(!projectile.isActiveAndEnabled, "Projectile hit did not return its shell to the pool.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[ServiceContracts] " + message);
    }

    #endregion
}
