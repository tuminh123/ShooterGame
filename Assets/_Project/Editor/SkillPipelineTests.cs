#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;
using Object = UnityEngine.Object;

public sealed class SkillPipelineTests
{
    private const string Root = "Assets/_Project/DataSO/";
    private static T Load<T>(string path) where T : Object
    {
        T result = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(result, Is.Not.Null, path);
        return result;
    }
    private static CharacterRuntimeData Runtime(CharacterDataSO data)
        => new CharacterRuntimeData(data, 1, 1, default, default, default);

    [Test]
    public void DefinitionsAndCharacterSkillArrays_AreConnected()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDataSO", new[] { Root }))
        {
            var data = Load<SkillDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.DoesNotThrow(data.Validate, data.name);
            Assert.That(data.Behavior, Is.Not.Null);
            count++;
        }
        Assert.That(count, Is.GreaterThanOrEqualTo(2));
        foreach (string guid in AssetDatabase.FindAssets("t:ProjectileDataSO", new[] { Root }))
            Assert.DoesNotThrow(Load<ProjectileDataSO>(AssetDatabase.GUIDToAssetPath(guid)).Validate);
        foreach (string guid in AssetDatabase.FindAssets("t:ShootingPatternDataSO", new[] { Root }))
            Assert.That(new ShootingPatternFactory().Resolve(
                Load<ShootingPatternDataSO>(AssetDatabase.GUIDToAssetPath(guid))), Is.Not.Null);
        var player = Load<CharacterDataSO>(Root + "Characters/BlessedGladiator_Character.asset");
        Assert.That(player.Shooting.Length, Is.EqualTo(1));
        Assert.That(player.Shooting[0], Is.SameAs(Load<ShootingDataSO>(Root + "Shooting/HolyBolt.asset")));
        var enemy = Load<CharacterDataSO>(Root + "Characters/ForagingMaggot_Character.asset");
        Assert.That(enemy.ActiveSkills[0].Behavior, Is.TypeOf<NormalAttackSkill>());
    }

    [Test]
    public void SkillData_IsCopiedByValue_AndRejectsInvalidLevels()
    {
        var skill = Load<SkillDataSO>(Root + "Skills/EnemyNormalAttack.asset");
        SkillData copy = skill.Data;
        copy.level++;
        Assert.That(skill.Data.level, Is.EqualTo(1));
        copy.level = copy.maxLevel + 1;
        Assert.Throws<InvalidOperationException>(() => copy.Validate());
        Assert.That(typeof(ISkill).GetMethods().Length, Is.EqualTo(1));
    }
    [Test]
    public void CircleHasNoDuplicateSeam_AndFanUsesConfiguredSpread()
    {
        var factory = new ShootingPatternFactory();
        var circle = Load<ShootingPatternDataSO>(Root + "Shooting/Circle.asset");
        IShootingPattern strategy = factory.Resolve(circle);
        var runtime = new ShootingRuntime();
        Vector2 first = strategy.GetDirection(circle, runtime, Vector2.right, 0);
        Vector2 last = strategy.GetDirection(circle, runtime, Vector2.right, circle.ProjectileCount - 1);
        Assert.That(Vector2.Angle(first, last), Is.EqualTo(30f).Within(0.001f));
        for (int i = 0; i < circle.ProjectileCount; i++)
            Assert.That(strategy.GetDirection(circle, runtime, Vector2.right, i).magnitude,
                Is.EqualTo(1f).Within(0.0001f));
        var fan = Load<ShootingPatternDataSO>(Root + "Shooting/Fan.asset");
        strategy = factory.Resolve(fan);
        Assert.That(Vector2.Angle(strategy.GetDirection(fan, runtime, Vector2.right, 0),
            strategy.GetDirection(fan, runtime, Vector2.right, fan.ProjectileCount - 1)),
            Is.EqualTo(45f).Within(0.001f));
    }

    [Test]
    public void ModifiersRestoreStats_UseStrongestSlow_AndKeepRangeFixed()
    {
        var template = Load<CharacterDataSO>(Root + "Characters/ForagingMaggot_Character.asset");
        CharacterRuntimeData runtime = Runtime(template);
        float speed = runtime.FinalStats.moveSpeed, range = runtime.FinalStats.range;
        IDisposable weak = runtime.AddSpeedMultiplier(0.7f);
        IDisposable strong = runtime.AddSpeedMultiplier(0.4f);
        IDisposable bonus = runtime.AddTemporaryBonus(new StatBonus { moveSpeed = 0.5f });
        Assert.That(runtime.FinalStats.moveSpeed, Is.EqualTo((speed + 0.5f) * 0.4f).Within(0.0001f));
        strong.Dispose();
        Assert.That(runtime.FinalStats.moveSpeed, Is.EqualTo((speed + 0.5f) * 0.7f).Within(0.0001f));
        bonus.Dispose(); weak.Dispose(); weak.Dispose();
        Assert.That(runtime.FinalStats.moveSpeed, Is.EqualTo(speed));
        Assert.That(runtime.FinalStats.range, Is.EqualTo(range));
        Assert.That(template.BaseStats.moveSpeed, Is.EqualTo(speed));
    }

    [Test]
    public void EffectChance_UsesAccuracyAgainstResistance()
    {
        Assert.That(CharacterEffectHandler.ApplicationChance(1f, 0f, 0f), Is.EqualTo(1f));
        Assert.That(CharacterEffectHandler.ApplicationChance(1f, 0f, 100f), Is.EqualTo(0.5f));
        Assert.That(CharacterEffectHandler.ApplicationChance(1f, 100f, 100f), Is.GreaterThan(0.5f));
        Assert.That(CharacterEffectHandler.ApplicationChance(0f, 100f, 0f), Is.Zero);
    }

    [UnityTest]
    public IEnumerator SharedPipeline_PreservesStandaloneShootingEffectsAndPoolReuse()
    {
        yield return new EnterPlayMode();
        var container = new DiContainer();
        var movement = new MovementManager();
        var pools = new PoolManager(container);
        var projectiles = new ProjectileService(pools,
            new ProjectileService.Settings(Load<GameObject>("Assets/_Project/Prefabs/HolyBolt.prefab")
                .GetComponent<ProjectileView>()), new ProjectileStrategyFactory());
        var shooting = new ShootingService(projectiles, new ShootingPatternFactory());
        container.BindInstance(movement);
        container.BindInstance(pools);
        container.BindInstance(shooting);
        var skills = new SkillManager();
        container.BindInstance(skills);
        container.BindInstance(new StatusEffectFactory());
        var factory = new CharacterFactory(
            Load<GameObject>("Assets/_Project/Prefabs/Character.prefab").GetComponent<CharacterView>(), pools);
        try
        {
            var playerData = Load<CharacterDataSO>(Root + "Characters/BlessedGladiator_Character.asset");
            var enemyData = Load<CharacterDataSO>(Root + "Characters/ForagingMaggot_Character.asset");
            CharacterView player = factory.Spawn(playerData, Runtime(playerData), new PlayerController(null),
                new Vector3(100f, 100f));
            CharacterView enemy = factory.Spawn(enemyData, Runtime(enemyData), new EnemyController(null, factory),
                new Vector3(100.2f, 100f));
            CharacterRuntimeData source = player.RuntimeData;
            var circle = Load<ShootingDataSO>(Root + "Shooting/BossFireCircle.asset");
            Assert.That(shooting.Fire(circle, new ShootingRuntime(), source, player.HitPosition,
                Vector2.right, default), Is.EqualTo(12));
            var spiral = Load<ShootingDataSO>(Root + "Shooting/BossFireSpiral.asset");
            var first = new ShootingRuntime();
            var second = new ShootingRuntime();
            shooting.Fire(spiral, first, source, player.HitPosition, Vector2.right, default);
            shooting.Fire(spiral, first, source, player.HitPosition, Vector2.right, default);
            shooting.Fire(spiral, second, source, player.HitPosition, Vector2.right, default);
            Assert.That(first.Phase, Is.EqualTo(30f));
            Assert.That(second.Phase, Is.EqualTo(15f));

            // The standalone scheduler still fires the player's configured shot.
            int scheduledShots = pools.GetHolder(PoolGroup.Bullets).childCount;
            var scheduledState = new ShootingRuntime();
            shooting.Tick(player, playerData.Shooting, new[] { scheduledState }, 0.01f);
            Assert.That(pools.GetHolder(PoolGroup.Bullets).childCount, Is.EqualTo(scheduledShots + 1));
            Assert.That(scheduledState.CooldownRemaining, Is.EqualTo(playerData.Shooting[0].Cooldown));
            shooting.Tick(player, playerData.Shooting, new[] { scheduledState }, 0.01f);
            Assert.That(pools.GetHolder(PoolGroup.Bullets).childCount, Is.EqualTo(scheduledShots + 1));

            var iceData = Load<ProjectileDataSO>(Root + "Projectiles/Ice.asset");
            var strategies = new ProjectileStrategyFactory();
            var iceRuntime = new ProjectileRuntimeData(iceData, source, new CharacterTarget(enemy),
                Vector2.right, strategies.Flight(iceData), strategies.Hit(iceData));
            Assert.That(iceRuntime.Hit.Apply(iceRuntime, new CharacterTarget(enemy)), Is.True);
            Assert.That(enemy.RuntimeData.FinalStats.moveSpeed,
                Is.EqualTo(enemyData.BaseStats.moveSpeed * 0.7f).Within(0.0001f));
            enemy.Effects.Tick(2f);
            Assert.That(enemy.RuntimeData.FinalStats.moveSpeed, Is.EqualTo(enemyData.BaseStats.moveSpeed));

            var poison = Load<StatusEffectDataSO>(Root + "Effects/Poison.asset");
            enemy.Effects.TryApply(poison, source.Team, source.FinalStats.damage, 0f);
            enemy.Effects.TryApply(poison, source.Team, source.FinalStats.damage, 0f);
            Assert.That(enemy.Effects.Count, Is.EqualTo(1));
            enemy.RuntimeData.ApplyHealthChange(-(enemy.RuntimeData.CurrentHealth - 1f));
            enemy.Effects.Tick(1f);
            Assert.That(enemy.RuntimeData, Is.Null, "Poison must kill through the normal despawn pipeline.");
            CharacterView reused = factory.Spawn(enemyData, Runtime(enemyData), new EnemyController(null, factory),
                new Vector3(100.2f, 100f));
            Assert.That(reused, Is.SameAs(enemy));
            Assert.That(reused.Effects.Count, Is.Zero);
            Assert.That(reused.RuntimeData.FinalStats.moveSpeed, Is.EqualTo(enemyData.BaseStats.moveSpeed));

            ProjectileView previousBullet = null;
            foreach (string name in new[] { "Fire", "Ice", "Poison", "Holy" })
            {
                var data = Load<ProjectileDataSO>(Root + "Projectiles/" + name + ".asset");
                projectiles.Fire(data, player.HitPosition, Vector2.up, default, source);
                Transform holder = pools.GetHolder(PoolGroup.Bullets);
                ProjectileView bullet = holder.GetChild(holder.childCount - 1).GetComponent<ProjectileView>();
                if (previousBullet != null) Assert.That(bullet, Is.SameAs(previousBullet));
                Assert.That(bullet.RuntimeData.Data, Is.SameAs(data));
                Assert.That(bullet.GetComponent<SpriteRenderer>().color, Is.EqualTo(data.Color));
                previousBullet = bullet;
                pools.Despawn(bullet.gameObject);
                Assert.That(bullet.RuntimeData, Is.Null);
            }
            LogAssert.NoUnexpectedReceived();
        }
        finally { factory.Dispose(); skills.Dispose(); pools.Dispose(); movement.Dispose(); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SkillManager_UsesCharacterDamageAndOwnsCooldownLevelsPassivesAndSpawnLifetimes()
    {
        yield return new EnterPlayMode();
        var container = new DiContainer();
        var movement = new MovementManager();
        var pools = new PoolManager(container);
        var skills = new SkillManager();
        var projectiles = new ProjectileService(pools,
            new ProjectileService.Settings(Load<GameObject>("Assets/_Project/Prefabs/HolyBolt.prefab")
                .GetComponent<ProjectileView>()), new ProjectileStrategyFactory());
        container.BindInstance(movement);
        container.BindInstance(pools);
        container.BindInstance(skills);
        container.BindInstance(new ShootingService(projectiles, new ShootingPatternFactory()));
        container.BindInstance(new StatusEffectFactory());
        var factory = new CharacterFactory(
            Load<GameObject>("Assets/_Project/Prefabs/Character.prefab").GetComponent<CharacterView>(), pools);
        var enemyTemplate = Object.Instantiate(Load<CharacterDataSO>(Root + "Characters/ForagingMaggot_Character.asset"));
        var playerTemplate = Object.Instantiate(Load<CharacterDataSO>(Root + "Characters/BlessedGladiator_Character.asset"));
        try
        {
            // Same asset in two slots and on two owners must have independent runtime data/timers.
            var serialized = new SerializedObject(enemyTemplate);
            serialized.FindProperty("activeSkills").arraySize = 2;
            serialized.FindProperty("activeSkills").GetArrayElementAtIndex(1).objectReferenceValue = enemyTemplate.ActiveSkills[0];
            serialized.FindProperty("passiveSkills").arraySize = 1;
            serialized.FindProperty("passiveSkills").GetArrayElementAtIndex(0).objectReferenceValue =
                Load<SkillDataSO>(Root + "Skills/Toughness.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized = new SerializedObject(playerTemplate);
            serialized.FindProperty("baseStats").FindPropertyRelative("maxHealth").floatValue = 1000f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CharacterRuntimeData Attacker() => new CharacterRuntimeData(enemyTemplate, 1, 1, default, default, default,
                new StatBonus { damage = 7f, critChance = -enemyTemplate.BaseStats.critChance });
            var position = new Vector3(100f, 100f);
            CharacterView victim = factory.Spawn(playerTemplate, Runtime(playerTemplate), new PlayerController(null), position);
            CharacterView first = factory.Spawn(enemyTemplate, Attacker(), new EnemyController(null, factory), position);
            CharacterView second = factory.Spawn(enemyTemplate, Attacker(), new EnemyController(null, factory), position);
            SkillManager.Handle oldHandle = first.SkillHandle;
            CharacterTarget target = new CharacterTarget(victim);
            Assert.That(first.RuntimeData.FinalStats.defense, Is.EqualTo(enemyTemplate.BaseStats.defense + 2f));
            skills.Activate(oldHandle);
            Assert.That(first.RuntimeData.FinalStats.defense, Is.EqualTo(enemyTemplate.BaseStats.defense + 2f));

            float before = victim.RuntimeData.CurrentHealth;
            Assert.That(first.TryUseSkill(0, target), Is.True);
            float expected = first.RuntimeData.FinalStats.damage * 100f / (100f + victim.RuntimeData.FinalStats.defense);
            Assert.That(before - victim.RuntimeData.CurrentHealth, Is.EqualTo(expected).Within(0.0001f),
                "Normal attack must use character BaseStats plus bonuses, with no skill damage multiplier.");
            Assert.That(first.TryUseSkill(0, target), Is.False);
            Assert.That(first.TryUseSkill(1, target), Is.True, "Duplicate asset slots have independent cooldowns.");
            Assert.That(second.TryUseSkill(0, target), Is.True, "Owners never share cooldowns.");

            float cooldown = enemyTemplate.ActiveSkills[0].Data.cooldown;
            first.gameObject.SetActive(false);
            skills.Simulate(cooldown);
            Assert.That(skills.GetCooldown(oldHandle, 0), Is.EqualTo(cooldown));
            first.gameObject.SetActive(true);
            skills.Simulate(cooldown);
            Assert.That(first.TryUseSkill(0, target), Is.True);
            Assert.That(skills.TryLevelUp(oldHandle, SkillCategory.Active, 0), Is.True);
            Assert.That(skills.TryGetData(oldHandle, SkillCategory.Active, 0, out SkillData leveled), Is.True);
            Assert.That(leveled.level, Is.EqualTo(2));
            skills.TryGetData(second.SkillHandle, SkillCategory.Active, 0, out SkillData other);
            Assert.That(other.level, Is.EqualTo(1));
            Assert.That(enemyTemplate.ActiveSkills[0].Data.level, Is.EqualTo(1));
            while (skills.TryLevelUp(oldHandle, SkillCategory.Active, 0)) { }
            skills.TryGetData(oldHandle, SkillCategory.Active, 0, out leveled);
            Assert.That(leveled.level, Is.EqualTo(leveled.maxLevel));
            Assert.That(skills.TryLevelUp(oldHandle, SkillCategory.Passive, 0), Is.True);
            Assert.That(first.RuntimeData.FinalStats.defense, Is.EqualTo(enemyTemplate.BaseStats.defense + 2f),
                "Reapplying a leveled passive must remove its previous modifier.");

            // Failed contact never consumes cooldown.
            skills.Simulate(cooldown);
            first.transform.position += Vector3.right * 5f;
            CharacterSearch.InvalidatePhysics();
            Assert.That(first.TryUseSkill(0, target), Is.False);
            Assert.That(skills.GetCooldown(oldHandle, 0), Is.Zero);
            first.transform.position = position;
            CharacterSearch.InvalidatePhysics();

            CharacterRuntimeData releasedData = first.RuntimeData;
            factory.Despawn(first);
            Assert.That(skills.IsRegistered(oldHandle), Is.False);
            Assert.That(releasedData.FinalStats.defense, Is.EqualTo(enemyTemplate.BaseStats.defense));
            CharacterView reused = factory.Spawn(enemyTemplate, Attacker(), new EnemyController(null, factory), position);
            Assert.That(reused, Is.SameAs(first));
            Assert.That(skills.TryExecute(oldHandle, 0, target), Is.False, "Stale handles cannot control a reused shell.");
            skills.TryGetData(reused.SkillHandle, SkillCategory.Active, 0, out SkillData fresh);
            Assert.That(fresh.level, Is.EqualTo(1));
            Assert.That(reused.TryUseSkill(0, target), Is.True);

            // A damage callback may release the caster before Execute returns.
            var callbackHandle = second.SkillHandle;
            CharacterRuntimeData callbackData = second.RuntimeData;
            victim.DamageReceived += (_, __) => factory.Despawn(second);
            skills.Simulate(cooldown);
            Assert.That(skills.TryExecute(callbackHandle, 0, target), Is.False);
            Assert.That(skills.IsRegistered(callbackHandle), Is.False);
            Assert.That(callbackData.FinalStats.defense, Is.EqualTo(enemyTemplate.BaseStats.defense));
            var deadHandle = reused.SkillHandle;
            reused.ReceiveDamage(new DamageInfo(100000f, CharacterTeam.Player));
            Assert.That(skills.IsRegistered(deadHandle), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            factory.Dispose(); skills.Dispose(); pools.Dispose(); movement.Dispose();
            Object.DestroyImmediate(enemyTemplate); Object.DestroyImmediate(playerTemplate);
        }
        yield return new ExitPlayMode();
    }
}
#endif
