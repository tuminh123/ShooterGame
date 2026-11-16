using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zenject;

/// <summary>Kiểm tra hợp đồng và binding thực trong scene, không cấp tiền hoặc ghi tiến trình.</summary>
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
            if (isFacade) Require(contract.GetInterfaces().Length == 0, contract.Name + " must expose roles through getters, not inheritance.");
            else Require(methods.Length <= 3, contract.Name + " declares more than three methods.");
        }
        Debug.Log($"[ServiceContracts] PASS: {interfaces.Length} public contracts, maximum three declared methods per role.");
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
                IGameStateReader state = context != null ? context.Container.TryResolve<IGameFlow>()?.GetGameStateReader() : null;
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
        var state = container.Resolve<IGameFlow>().GetGameStateReader();
        Require(state.State == GameState.Home, "Scene must be at Home; actual state: " + state.State);
        CheckContracts();
        CheckBindings(container);

        var commands = container.Resolve<IGameFlow>().GetRunCommands();
        var playerReader = container.Resolve<IPlayerSystem>().GetPlayerReader();
        var characterDespawn = container.Resolve<ICharacterFactory>().GetCharacterDespawn();
        var stageLifecycle = container.Resolve<IStageService>().GetStageRunLifecycle();
        var pauses = container.Resolve<IGameplayPauseService>().GetGameplayPause();
        using (pauses.Acquire())
        {
            try
            {
                Require(commands.StartRun(), "StartRun failed.");
                Require(state.State == GameState.Playing && playerReader.RuntimeData.IsAlive, "Player did not enter Playing alive.");
                CharacterView player = playerReader.CurrentPlayer;
                CharacterRuntimeData oldRuntime = player.RuntimeData;
                ulong oldGeneration = oldRuntime.Generation;

                // Dừng lịch Stage để kiểm tra đồng bộ; token pause ngăn nhặt loot và ghi ví.
                stageLifecycle.StopRun();
                CheckMovement(container, player);
                CheckSkills(container, player);
                CheckEffectsAndDamage(container, player, characterDespawn);

                using (pauses.Acquire())
                    Require(Time.timeScale == 0f, "Nested pause did not hold gameplay.");
                Require(Time.timeScale == 0f, "Releasing one lease incorrectly resumed another lease.");

                Require(commands.RestartRun(), "RestartRun failed.");
                Require(playerReader.RuntimeData.IsAlive, "Restart did not create a living player.");
                Require(!ReferenceEquals(oldRuntime, playerReader.RuntimeData) || oldGeneration != oldRuntime.Generation,
                    "Restart reused a runtime without changing its generation.");
                var owned = new List<SkillRuntime>();
                container.Resolve<ISkillSystem>().GetReader().GetOwnedSkills(playerReader.CurrentPlayer, owned);
                Require(owned.Count == 0, "Restart retained gameplay skills.");
                Require(container.Resolve<ILevelService>().GetRunProgress().RunLevel == 1, "Restart did not reset run level.");
                Require(commands.ReturnHome(), "ReturnHome failed.");
                Require(state.State == GameState.Home && playerReader.CurrentPlayer == null, "Home retained the player.");
            }
            finally
            {
                if (state.State == GameState.Playing) commands.ReturnHome();
            }
        }
        Require(Time.timeScale == 0f, "Home lost its flow pause.");
        Debug.Log("[ServiceContracts] PASS: facade getters and singleton instances, lifecycle registration, movement, search, projectiles, skills, effects, damage, stale handles, pause, restart and Home. No result rewards granted.");
    }

    // Mọi vai trò của một service phải resolve về cùng instance, không self-bind implementation.
    private static void CheckBindings(DiContainer container)
    {
        Type[] aggregates = {
            typeof(IMovementSystem), typeof(ISpatialManager), typeof(ISpatialSearchFactory), typeof(ICharacterSearch),
            typeof(ISkillSystem), typeof(ISkillFactory), typeof(ISkillServices), typeof(IEffectSystem),
            typeof(ICharacterFactory), typeof(ICharacterSystem), typeof(IPlayerSystem), typeof(ILevelService),
            typeof(ICharacterLevelService), typeof(IRunLevelService), typeof(IEquipmentService),
            typeof(IPoolSystem), typeof(ICharacterRuntimePool), typeof(IEffectRuntimePool), typeof(IPoolPrewarmService),
            typeof(IProjectileSystem), typeof(ICombatSpawnSystem), typeof(IShootBehaviorFactory), typeof(IProjectileMoveFactory),
            typeof(IGameFlow), typeof(IGameplayPauseService), typeof(IRunSession), typeof(ISaveService),
            typeof(ICurrencyService), typeof(IStageService), typeof(IStageProgress), typeof(IEnemySpawnService),
            typeof(IBossSummonService), typeof(IWorldMapService), typeof(IScreenTransitionService), typeof(IUINavigation)
        };
        foreach (Type aggregate in aggregates)
        {
            object implementation = container.Resolve(aggregate);
            foreach (MethodInfo getter in aggregate.GetMethods())
            {
                Require(getter.GetParameters().Length == 0 && getter.ReturnType.IsInterface, "Invalid facade getter: " + aggregate.Name);
                object role = getter.Invoke(implementation, null);
                Require(role != null && getter.ReturnType.IsInstanceOfType(role), "Missing role: " + getter.Name);
                Require(ReferenceEquals(role, getter.Invoke(implementation, null)), "Unstable role instance: " + getter.Name);
                Require(!container.HasBinding(getter.ReturnType), "Leaf role must be obtained through its facade: " + getter.ReturnType.Name);
            }
            Require(!container.HasBinding(implementation.GetType()), "Unexpected concrete binding: " + implementation.GetType().Name);
            if (implementation is IInitializable)
                Require(container.ResolveAll<IInitializable>().Count(item => ReferenceEquals(item, implementation)) == 1,
                    "Duplicate/absent initialization: " + implementation.GetType().Name);
            if (implementation is IDisposable)
                Require(container.ResolveAll<IDisposable>().Count(item => ReferenceEquals(item, implementation)) == 1,
                    "Duplicate/absent disposal: " + implementation.GetType().Name);
            if (implementation is ITickable)
            {
                int count = container.ResolveAll<ITickable>().Count(item => ReferenceEquals(item, implementation));
                Require(count == (implementation is MonoBehaviour ? 0 : 1), "Duplicate/absent tick registration: " + implementation.GetType().Name);
            }
            if (implementation is ILateTickable)
            {
                int count = container.ResolveAll<ILateTickable>().Count(item => ReferenceEquals(item, implementation));
                Require(count == (implementation is MonoBehaviour ? 0 : 1), "Duplicate/absent late tick registration: " + implementation.GetType().Name);
            }
        }
    }

    #endregion

    #region Character Integration

    private static void CheckMovement(DiContainer container, CharacterView player)
    {
        Vector3 before = player.transform.position;
        player.Move(Vector2.right, 2f);
        // Mô phỏng thủ công khi toàn scene đang pause; scheduler không chạy thêm bước gameplay.
        ((MovementManager)container.Resolve<IMovementSystem>()).Simulate(0.1f);
        Require(player.transform.position.x > before.x, "Movement command did not reach the movement job.");
        player.Move(Vector2.zero, 0f);
    }

    private static void CheckSkills(DiContainer container, CharacterView player)
    {
        var grant = container.Resolve<ISkillSystem>().GetGrant();
        var reader = container.Resolve<ISkillSystem>().GetReader();
        SkillDataSO definition = container.Resolve<SkillDatabaseSO>().CreateRunChoiceCatalog().First();
        Require(grant.GrantOrUpgrade(definition, player), "Could not grant a gameplay skill.");
        Require(reader.TryGetRuntime(definition, player, out SkillRuntime runtime), "Granted skill cannot be read.");
        if (definition.MaxRunLevel > 1)
        {
            Require(grant.GrantOrUpgrade(definition, player), "Could not upgrade the gameplay skill.");
            Require(runtime.RunLevel == 2, "Skill did not advance to level two.");
        }
    }

    private static void CheckEffectsAndDamage(DiContainer container, CharacterView player, ICharacterDespawn release)
    {
        CharacterDataSO definition = container.Resolve<CharacterDatabaseSO>().CreateLookup().Values
            .First(item => item.Team == CharacterTeam.Enemy && !item.IsBoss);
        CharacterView enemy = container.Resolve<IEnemySpawnService>().GetEnemySpawn().Spawn(definition, 1,
            (Vector2)player.transform.position + Vector2.right * 3f);
        var target = new CharacterTarget(enemy);
        SpatialHandle spatialHandle = enemy.SpatialHandle;
        var registration = container.Resolve<ISpatialManager>().GetRegistration();
        var effects = container.Resolve<IEffectSystem>().GetEffectApplication();
        var handles = container.Resolve<IEffectSystem>().GetEffectHandleControl();
        try
        {
            Require(registration.IsRegistered(spatialHandle), "Enemy did not register its hitbox.");
            Require(player.TargetSearch.FindNearest(enemy.HitPosition, 0.1f, CharacterTeam.Enemy).TryGetCharacter(out _),
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
        Require(!container.Resolve<ISpatialManager>().GetResolution().TryResolve(spatialHandle, out _), "Released spatial handle resolved a character.");
    }

    // Gọi đúng pipeline production dưới pause để quét đạn mà không làm đồng hồ scene chạy thêm.
    private static void CheckProjectile(DiContainer container, CharacterView player, CharacterView enemy)
    {
        float before = enemy.RuntimeData.CurrentHealth + enemy.RuntimeData.CurrentShield;
        var data = new ProjectileSpawnData(container.Resolve<CombatAssetsSO>().BulletEntries[0].type,
            new DamageInfo(Mathf.Min(0.1f, before * 0.1f), CharacterTeam.Player), 50f, 1f,
            new StraightProjectileMove(), Array.Empty<IGameplayEffect>(), 0f);
        Vector2 direction = (enemy.HitPosition - player.HitPosition).normalized;
        Require(container.Resolve<ICombatSpawnSystem>().GetProjectileSpawner().SpawnBullet(data, player.HitPosition, direction, default),
            "Projectile spawner failed.");
        ProjectileView projectile = UnityEngine.Object.FindObjectsOfType<ProjectileView>().Single();
        MethodInfo prepare = typeof(ProjectileView).GetMethod("PrepareQuery", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo complete = typeof(ProjectileView).GetMethod("CompleteQuery", BindingFlags.Instance | BindingFlags.NonPublic);
        object[] arguments = { 0.1f, default(SpatialQuery) };
        Require((bool)prepare.Invoke(projectile, arguments), "Projectile did not prepare a sweep query.");
        SpatialHit hit = container.Resolve<ISpatialManager>().GetQueries().Query((SpatialQuery)arguments[1]);
        Require(container.Resolve<ISpatialManager>().GetResolution().TryResolve(hit, out CharacterTarget target), "Projectile sweep missed the enemy.");
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
