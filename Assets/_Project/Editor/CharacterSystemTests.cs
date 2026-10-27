#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;

// * Kiểm tra các lỗi dễ xuất hiện khi tách data/hành vi/vỏ: HP dùng chung, sai đồng hồ và rò trạng thái pool.
// * Chạy trong Test Runner > EditMode; bài kiểm tra pool tự vào Play Mode để dùng callback Unity thật.
public sealed class CharacterSystemTests
{
    private const string DataPath = "Assets/_Project/DataSO/Characters/"; // * Dữ liệu mẫu thật của dự án để kiểm tra cả tham chiếu asset.
    private const string DatabasePath = "Assets/_Project/DataSO/Databases/"; // * Database dùng chung giữa scene và bài kiểm tra.

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }

    private static CharacterRuntimeData Runtime(CharacterDataSO data, StatBonus bonus = default)
        => new CharacterRuntimeData(data, 1, 1, default, default, default, bonus);

    [Test]
    public void SpawnBonusAndDamage_AreIsolatedFromOtherInstancesAndTemplate()
    {
        CharacterDataSO data = Load<CharacterDataSO>(DataPath + "ForagingMaggot_Character.asset");
        float baseHealth = data.BaseStats.maxHealth;
        CharacterRuntimeData first = Runtime(data, new StatBonus { maxHealth = 5f });
        CharacterRuntimeData second = Runtime(data, new StatBonus { maxHealth = 10f });
        first.ApplyHealthChange(-7f);

        Assert.That(first.CurrentHealth, Is.EqualTo(baseHealth + 5f - 7f));
        Assert.That(second.CurrentHealth, Is.EqualTo(baseHealth + 10f));
        Assert.That(data.BaseStats.maxHealth, Is.EqualTo(baseHealth));
        Assert.That(first.Team, Is.EqualTo(CharacterTeam.Enemy));
    }

    [Test]
    public void GrowthClock_StartsOnFirstSuccessfulSpawnAndResetsOnlyForNewRun()
    {
        CharacterDataSO first = Load<CharacterDataSO>(DataPath + "ForagingMaggot_Character.asset");
        CharacterDataSO second = Object.Instantiate(first);
        try
        {
            // * Tạo một loại khác trong bản sao thử nghiệm, không chỉnh asset gốc hoặc enum lưu trong save.
            var serialized = new SerializedObject(second);
            serialized.FindProperty("characterType").intValue = (int)CharacterType.BlessedGladiator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var progress = new EnemySpawnProgress();
            Assert.That(progress.GetBonus(first, 500f).maxHealth, Is.Zero);
            progress.RecordSpawn(first.CharacterType, 30f);
            progress.RecordSpawn(first.CharacterType, 70f);
            progress.RecordSpawn(second.CharacterType, 75f);

            Assert.That(progress.GetBonus(first, 79f).maxHealth, Is.Zero);
            Assert.That(progress.GetBonus(first, 80f).maxHealth, Is.EqualTo(5f));
            CharacterRuntimeData existing = Runtime(first, progress.GetBonus(first, 80f));
            Assert.That(progress.GetBonus(first, 130f).maxHealth, Is.EqualTo(10f));
            Assert.That(progress.GetBonus(second, 130f).maxHealth, Is.EqualTo(5f));
            Assert.That(existing.SpawnBonus.maxHealth, Is.EqualTo(5f));
            progress.Reset();
            Assert.That(progress.GetBonus(first, 500f).maxHealth, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void Waves_SwitchAtBoundaryAndKeepLastWave_EnemyNeedsNoPermanentLevelConfig()
    {
        WaveConfigSO config = Object.Instantiate(Load<WaveConfigSO>(DataPath + "Default_Waves.asset"));
        try
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("waves").arraySize = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            config.Validate();
            WaveConfigSO.Wave first = config.GetWave(0f);
            WaveConfigSO.Wave next = config.GetWave(first.duration);
            Assert.That(next, Is.Not.SameAs(first));
            Assert.That(config.GetWave(100000f), Is.SameAs(next));

            using (var saves = new SaveManager(new SaveManager.Settings("CharacterValidation.es3")))
            using (var levels = new CharacterLevelService(saves,
                Load<CharacterDatabaseSO>(DatabasePath + "CharacterDatabase.asset"),
                Load<LevelDatabaseSO>(DatabasePath + "LevelDatabase.asset")))
            {
                Assert.That(levels.GetCharacter(CharacterType.ForagingMaggot).Team, Is.EqualTo(CharacterTeam.Enemy));
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [UnityTest]
    public IEnumerator SharedShell_ReusesAcrossTeamsAndCleansFailedInitialization()
    {
        yield return new EnterPlayMode();
        var container = new DiContainer();
        var movement = new MovementManager();
        var pools = new PoolManager(container);
        container.BindInstance(movement);
        container.BindInstance(pools);
        container.BindInstance(new ProjectileService(pools));
        CharacterView prefab = Load<GameObject>("Assets/_Project/Prefabs/Character.prefab").GetComponent<CharacterView>();
        var factory = new CharacterFactory(new CharacterFactory.Settings(prefab), pools);
        CharacterDataSO enemyData = Load<CharacterDataSO>(DataPath + "ForagingMaggot_Character.asset");
        CharacterDataSO playerData = Load<CharacterDataSO>(DataPath + "BlessedGladiator_Character.asset");
        try
        {
            // * Bài kiểm tra này kiểm tra gắn/tháo và tiếp xúc, không tick AI nên không cần PlayerManager.
            var enemyBehavior = new EnemyController(null, factory);
            CharacterRuntimeData oldRuntime = Runtime(enemyData);
            CharacterView enemy = factory.Spawn(enemyData, oldRuntime, enemyBehavior, Vector3.right * 100f);
            Assert.That(movement.Count, Is.EqualTo(1));
            Assert.That(enemy.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
            int releases = 0;
            enemy.Released += view =>
            {
                releases++;
                // * Regression: visual đã ra khỏi hierarchy nhưng vỏ chưa bị tắt khi báo Released.
                Assert.That(view.gameObject.activeInHierarchy, Is.True);
                Assert.That(view.transform.childCount, Is.Zero);
            };
            oldRuntime.ApplyHealthChange(-5f);
            factory.Despawn(enemy);
            factory.Despawn(enemy); // * Thu hồi lặp phải an toàn và không phát lại Released.
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(movement.Count, Is.Zero);
            Assert.That(enemy.RuntimeData, Is.Null);
            Assert.That(enemy.transform.childCount, Is.Zero);

            CharacterRuntimeData fresh = Runtime(playerData);
            CharacterView player = factory.Spawn(playerData, fresh, new PlayerController(null), Vector3.zero);
            Assert.That(player, Is.SameAs(enemy));
            Assert.That(player.RuntimeData, Is.SameAs(fresh));
            Assert.That(fresh.CurrentHealth, Is.EqualTo(playerData.BaseStats.maxHealth));
            Assert.That(player.GetComponent<BoxCollider2D>().size, Is.EqualTo(playerData.ColliderSize));
            Assert.That(player.GetComponentInChildren<SpriteRenderer>().flipX, Is.False);
            // * Tắt tạm thời chỉ dừng movement, không trả visual về pool từ OnDisable và không làm mất data.
            Transform playerVisual = player.transform.GetChild(0);
            player.gameObject.SetActive(false);
            Assert.That(movement.Count, Is.Zero);
            Assert.That(playerVisual.parent, Is.EqualTo(player.transform));
            Assert.That(player.RuntimeData, Is.SameAs(fresh));
            player.gameObject.SetActive(true);
            Assert.That(movement.Count, Is.EqualTo(1));
            factory.Despawn(player);
            Assert.That(releases, Is.EqualTo(1), "Old owner must not receive the next lifetime's event.");

            // * Lỗi trước khi kích hoạt phải trả được vỏ về pool mà không để lại runtime/hành vi dở dang.
            Assert.Throws<System.ArgumentException>(() => factory.Spawn(enemyData, fresh, enemyBehavior, Vector3.zero));
            Assert.That(movement.Count, Is.Zero);
            CharacterView reused = factory.Spawn(enemyData, Runtime(enemyData), enemyBehavior, Vector3.right * 100f);
            Assert.That(reused, Is.SameAs(enemy));
            Assert.That(reused.RuntimeData.CurrentHealth, Is.EqualTo(enemyData.BaseStats.maxHealth));

            // * Hành vi chạm Player chỉ là test cũ; hiện chỉ damage gây chết mới trả visual/vỏ về pool.
            CharacterView target = factory.Spawn(playerData, Runtime(playerData), new PlayerController(null), Vector3.zero);
            reused.ReceiveDamage(new DamageInfo(100000f, CharacterTeam.Player));
            Assert.That(reused.RuntimeData, Is.Null);
            Assert.That(reused.transform.childCount, Is.Zero);
            Assert.That(movement.Count, Is.EqualTo(1));
            factory.Despawn(target);

            // * Thu hồi khi Enemy hết HP là luật trong EnemyController, không nằm trong vỏ hiển thị.
            reused = factory.Spawn(enemyData, Runtime(enemyData), enemyBehavior, Vector3.right * 100f);
            reused.RuntimeData.ApplyHealthChange(-100000f);
            enemyBehavior.Tick(reused, reused.RuntimeData);
            Assert.That(reused.RuntimeData, Is.Null);
            Assert.That(movement.Count, Is.Zero);

            // * Khi đóng scene, factory thu hồi nhân vật trước PoolManager, cũng không reparent trong OnDisable.
            CharacterView closing = factory.Spawn(enemyData, Runtime(enemyData), enemyBehavior, Vector3.right * 100f);
            factory.Dispose();
            Assert.That(closing.RuntimeData, Is.Null);
            Assert.That(closing.transform.childCount, Is.Zero);
            Assert.That(movement.Count, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            factory.Dispose();
            pools.Dispose();
            movement.Dispose();
        }
        yield return new ExitPlayMode();
    }
}
#endif
