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
            Assert.That(new SkillFactory().Create(data, new SkillContext(null, null)), Is.Not.Null);
            count++;
        }
        Assert.That(count, Is.GreaterThanOrEqualTo(7));
        foreach (string guid in AssetDatabase.FindAssets("t:ProjectileDataSO", new[] { Root }))
            Assert.DoesNotThrow(Load<ProjectileDataSO>(AssetDatabase.GUIDToAssetPath(guid)).Validate);
        foreach (string guid in AssetDatabase.FindAssets("t:ShootingPatternDataSO", new[] { Root }))
            Assert.That(new ShootingPatternFactory().Resolve(
                Load<ShootingPatternDataSO>(AssetDatabase.GUIDToAssetPath(guid))), Is.Not.Null);
        var player = Load<CharacterDataSO>(Root + "Characters/BlessedGladiator_Character.asset");
        Assert.That(player.Skills.Length, Is.EqualTo(1));
        Assert.That(player.Skills[0], Is.SameAs(Load<SkillDataSO>(Root + "Skills/HolyBolt.asset")));
    }

    [Test]
    public void Factory_SharesBehaviorButSeparatesRuntimePerSkillAndOwner()
    {
        var factory = new SkillFactory();
        var context = new SkillContext(null, null);
        SkillRuntime fire = factory.Create(Load<SkillDataSO>(Root + "Skills/FireBolt.asset"), context);
        SkillRuntime ice = factory.Create(Load<SkillDataSO>(Root + "Skills/IceBolt.asset"), context);
        SkillRuntime secondFire = factory.Create(fire.Data, context);
        Assert.That(fire.Strategy, Is.SameAs(ice.Strategy));
        Assert.That(fire, Is.Not.SameAs(secondFire));
        Assert.That(fire.Shooting, Is.Not.SameAs(secondFire.Shooting));
        fire.Dispose();
        Assert.That(secondFire.IsAttached, Is.True);
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
    public IEnumerator SharedPipeline_ConnectsPatternsCooldownPassivesEffectsAndPoolReuse()
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
        container.BindInstance(new SkillFactory());
        container.BindInstance(new StatusEffectFactory());
        var factory = new CharacterFactory(new CharacterFactory.Settings(
            Load<GameObject>("Assets/_Project/Prefabs/Character.prefab").GetComponent<CharacterView>()), pools);
        try
        {
            var playerData = Load<CharacterDataSO>(Root + "Characters/BlessedGladiator_Character.asset");
            var enemyData = Load<CharacterDataSO>(Root + "Characters/ForagingMaggot_Character.asset");
            CharacterView player = factory.Spawn(playerData, Runtime(playerData), new PlayerController(null),
                new Vector3(100f, 100f));
            CharacterView enemy = factory.Spawn(enemyData, Runtime(enemyData), new EnemyController(null, factory),
                new Vector3(100.2f, 100f));
            CharacterRuntimeData source = player.RuntimeData;
            var circle = Load<ProjectileSkillDataSO>(Root + "Skills/BossFireCircle.asset");
            Assert.That(shooting.Fire(circle, new ShootingRuntime(), source, player.HitPosition,
                Vector2.right, default), Is.EqualTo(12));
            var spiral = Load<ProjectileSkillDataSO>(Root + "Skills/BossFireSpiral.asset");
            var first = new ShootingRuntime();
            var second = new ShootingRuntime();
            shooting.Fire(spiral, first, source, player.HitPosition, Vector2.right, default);
            shooting.Fire(spiral, first, source, player.HitPosition, Vector2.right, default);
            shooting.Fire(spiral, second, source, player.HitPosition, Vector2.right, default);
            Assert.That(first.Phase, Is.EqualTo(30f));
            Assert.That(second.Phase, Is.EqualTo(15f));

            SkillRuntime fire = player.Skills.AddSkill(Load<SkillDataSO>(Root + "Skills/FireBolt.asset"));
            SkillRuntime ice = player.Skills.AddSkill(Load<SkillDataSO>(Root + "Skills/IceBolt.asset"));
            Assert.That(player.Skills.TryUse(fire), Is.True);
            Assert.That(player.Skills.TryUse(fire), Is.False);
            Assert.That(player.Skills.TryUse(ice), Is.True);
            Assert.That(fire.CooldownRemaining, Is.EqualTo(0.8f));
            Assert.That(ice.CooldownRemaining, Is.EqualTo(1f));
            SkillRuntime passive = player.Skills.AddSkill(Load<SkillDataSO>(Root + "Skills/Toughness.asset"));
            player.Skills.Tick(0.01f);
            player.Skills.Tick(0.01f);
            Assert.That(source.FinalStats.defense, Is.EqualTo(playerData.BaseStats.defense + 2f));
            int triggers = 0;
            Assert.That(player.Skills.TryTriggerPassive(passive, () => { triggers++; return true; }), Is.True);
            Assert.That(player.Skills.TryTriggerPassive(passive, () => { triggers++; return true; }), Is.False);
            Assert.That(triggers, Is.EqualTo(1), "Passive internal cooldown belongs to the character handler.");
            player.Skills.RemoveSkill(passive);
            Assert.That(source.FinalStats.defense, Is.EqualTo(playerData.BaseStats.defense));

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
        finally { factory.Dispose(); pools.Dispose(); movement.Dispose(); }
        yield return new ExitPlayMode();
    }
}
#endif
