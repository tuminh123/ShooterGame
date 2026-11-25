using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm tra miền Mission trong Edit Mode bằng Save/Progression thật trên một FILE SAVE TẠM riêng (xóa khi xong),
/// nên không chạm hồ sơ người chơi. Gameplay (quái chết, kết quả trận) được giả bằng event.
/// </summary>
public static class MissionVerification
{
    #region Entry

    private const string SaveFile = "MissionVerification.es3";
    private static readonly List<UnityEngine.Object> Temporary = new List<UnityEngine.Object>();

    [MenuItem("Shooter Game/Verification/Check Missions")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run Check Missions in Edit Mode.");
        Require(!ES3.FileExists(SaveFile), "Temporary save already exists: " + SaveFile);
        try
        {
            CheckConfiguredDatabases();
            CheckCycles();
            CheckProgressAndClaim();
            CheckReset();
            CheckAppSuspend();
            CheckFinalMission();
            CheckInvalidRewards();
            CheckStageRewards();
            CheckKillFilters();
            Debug.Log("[Missions] PASS: databases, cycles, progress, claim (currency/character/shards/equipment), one-shot claim, reload, reset, app suspend, final mission, invalid rewards, stage rewards, kill filters.");
        }
        finally
        {
            ES3.DeleteFile(SaveFile);
            foreach (UnityEngine.Object item in Temporary) UnityEngine.Object.DestroyImmediate(item);
            Temporary.Clear();
        }
    }

    #endregion

    #region Checks

    // Mọi database trong project hợp lệ và mọi gói thưởng trao được với database nhân vật/trang bị thật.
    private static void CheckConfiguredDatabases()
    {
        using (var world = new World(Array.Empty<MissionDatabaseSO>()))
            foreach (MissionDatabaseSO database in Assets<MissionDatabaseSO>())
            {
                database.Validate();
                foreach (MissionDataSO mission in database.Missions)
                {
                    world.Progression.ValidateRewards(mission.Rewards);
                    foreach (MissionTask task in mission.Tasks) CheckKillTarget(mission, task);
                }
                if (database.FinalMission != null) world.Progression.ValidateRewards(database.FinalMission.Rewards);
            }
    }

    private static void CheckCycles()
    {
        var sunday = new DateTime(2026, 11, 22, 23, 59, 59, DateTimeKind.Utc);
        DateTime monday = sunday.AddSeconds(1);
        MissionDatabaseSO daily = Database("d", MissionCategory.Daily);
        MissionDatabaseSO weekly = Database("w", MissionCategory.Weekly);
        MissionDatabaseSO ev = Database("e", MissionCategory.Event, "2026-11-20", "2026-11-23");
        Require(Cycle(daily, sunday) != Cycle(daily, monday), "Daily did not roll at midnight UTC.");
        Require(Cycle(daily, monday) == Cycle(daily, monday.AddHours(23)), "Daily rolled inside one day.");
        Require(Cycle(weekly, sunday) != Cycle(weekly, monday), "Weekly did not roll on Monday.");
        Require(Cycle(weekly, monday) == Cycle(weekly, monday.AddDays(6)), "Weekly rolled inside one week.");
        Require(ev.TryGetCycle(sunday, out _, out _) && !ev.TryGetCycle(monday, out _, out _), "Event window is wrong.");
        daily.TryGetCycle(sunday, out _, out DateTime ends);
        Require(ends == monday, "Daily end time is wrong.");
    }

    private static void CheckProgressAndClaim()
    {
        CharacterDataSO player = Assets<CharacterDataSO>().First(item => item.Team == CharacterTeam.Player);
        EquipmentDatabaseSO items = Assets<EquipmentDatabaseSO>().Single();
        EquipmentDataSO item = items.Items[0];
        const int level = 10;
        List<FixedSubstats.Line> lines = FixedLines(items.Rules, item.Slot, EquipmentRarity.Epic, level);
        MissionDataSO mission = Mission("m",
            new[] { (MissionObjectiveType.KillEnemy, 3), (MissionObjectiveType.WinRun, 1), (MissionObjectiveType.CollectRunGold, 50) },
            new CurrencyReward(CurrencyType.Gold, 500), new CurrencyReward(CurrencyType.Gold, 100), new CurrencyReward(CurrencyType.Diamond, 5),
            new CharacterReward(player), new CharacterShardReward(player, 7),
            new EquipmentReward(item, EquipmentRarity.Epic, level, 1, new FixedSubstats(lines)),
            new EquipmentReward(item, EquipmentRarity.Rare, level, 2, new RolledSubstats()));
        MissionDatabaseSO daily = Database("daily", MissionCategory.Daily, null, null, mission);
        MissionDatabaseSO closed = Database("closed", MissionCategory.Event, "2000-01-01", "2000-01-02", mission);

        using (var world = new World(new[] { daily, closed }))
        {
            MissionRuntime runtime = world.Missions.Boards[0].Missions[0];
            MissionRuntime inactive = world.Missions.Boards[1].Missions[0];
            int changes = 0;
            world.Missions.Changed += () => changes++;

            world.Kill(CharacterTeam.Player);
            Require(runtime.GetProgress(0) == 0 && changes == 0, "Player death counted as an enemy.");
            for (int i = 0; i < 5; i++) world.Kill(CharacterTeam.Enemy);
            Require(runtime.GetProgress(0) == 3 && changes == 3, "Kills were not capped at the target.");
            Require(inactive.GetProgress(0) == 0, "A closed event received progress.");
            Require(!world.Missions.TryClaim(runtime, out _, out _), "Claimed an unfinished mission.");

            world.Game.Set(GameState.Playing);
            world.RunData.Gold = 30;
            world.Game.Set(GameState.EndingLose);
            Require(runtime.GetProgress(1) == 0 && runtime.GetProgress(2) == 30, "Losing counted as a win or lost run gold.");
            world.Game.Set(GameState.Playing);
            world.RunData.Gold = 40;
            world.Game.Set(GameState.EndingWin);
            Require(runtime.State == MissionState.Claimable && world.Missions.HasClaimable, "Finished mission is not claimable.");

            long gold = world.Progression.GetBalance(CurrencyType.Gold);
            long diamond = world.Progression.GetBalance(CurrencyType.Diamond);
            var collection = (ICharacterCollection)world.Progression;
            var equipment = (IEquipment)world.Progression;
            bool ownedBefore = collection.GetStatus(player.CharacterType).Owned;
            long shardsBefore = collection.GetStatus(player.CharacterType).Shards;
            int itemsBefore = equipment.Items.Count;
            int balanceEvents = 0;
            bool reentrant = false;
            world.Progression.BalanceChanged += (type, balance) =>
            {
                balanceEvents++;
                reentrant |= world.Missions.TryClaim(runtime, out _, out _); // Không được nhận lồng từ callback.
            };

            Require(world.Missions.TryClaim(runtime, out RewardResult result, out bool saved) && saved, "Claim failed or was not saved.");
            Require(world.Progression.GetBalance(CurrencyType.Gold) == gold + 600 && world.Progression.GetBalance(CurrencyType.Diamond) == diamond + 5,
                "Currency reward is wrong.");
            Require(balanceEvents == 2 && !reentrant, "Balance events are wrong or a nested claim succeeded.");
            CharacterCollectionStatus status = collection.GetStatus(player.CharacterType);
            long duplicate = ownedBefore ? status.ShardsPerDuplicate : 0;
            Require(status.Owned && status.Shards == shardsBefore + duplicate + 7, "Character or shard reward is wrong.");
            Require(ownedBefore ? result.Shards.Count == 2 && result.NewCharacters.Count == 0 : result.NewCharacters.Count == 1, "Reward result is wrong.");
            Require(equipment.Items.Count == itemsBefore + 3 && result.Equipment.Count == 3, "Equipment reward count is wrong.");
            EquipmentItem fixedItem = result.Equipment[0];
            Require(fixedItem.Level == level && fixedItem.Rarity == EquipmentRarity.Epic && fixedItem.Substats.Count == lines.Count &&
                fixedItem.Substats.Select(stat => stat.value).SequenceEqual(lines.Select(line => line.value)), "Fixed substats were not kept.");
            Require(Mathf.Approximately(fixedItem.MainValue, items.Rules.GetMainValue(item.Slot, EquipmentRarity.Epic, level)), "Main stat is wrong.");
            Require(result.Equipment[1].Substats.Count == items.Rules.GetExpectedSubstats(EquipmentRarity.Rare, level), "Rolled substats ignore milestones.");
            Require(runtime.State == MissionState.Claimed && !world.Missions.TryClaim(runtime, out _, out _), "Reward can be claimed twice.");
            Require(!world.Missions.TryClaim(inactive, out _, out _), "Claimed a closed event.");
        }

        // Mở lại từ file: mọi phần của gói đã nằm trong cùng lần ghi và Repair không sửa món đồ cố định.
        using (var world = new World(new[] { daily }))
        {
            Require(world.Missions.Boards[0].Missions[0].State == MissionState.Claimed, "Claimed flag was not saved.");
            Require(world.Progression.GetBalance(CurrencyType.Diamond) == 5, "Wallet was not saved with the claim.");
            EquipmentItem loaded = ((IEquipment)world.Progression).Items.First(candidate => candidate.Rarity == EquipmentRarity.Epic);
            Require(loaded.Substats.Select(stat => stat.value).SequenceEqual(lines.Select(line => line.value)), "Reload changed the fixed item.");

            // Tín hiệu nâng cấp: trang bị mỗi level một lần, nhân vật theo level thật.
            var equipment = (IEquipment)world.Progression;
            world.Progression.Add(CurrencyType.Gold, 10_000_000);
            int itemUps = 0, levelUps = 0;
            equipment.EquipmentLeveledUp += _ => itemUps++;
            world.Progression.CharacterRaised += (type, kind, amount) => levelUps += amount;
            Require(equipment.TryUpgrade(loaded, out _) && itemUps == 1, "Equipment level up signal is wrong.");
            int before = ((ICharacterProgress)world.Progression).GetLevel(player.CharacterType);
            for (int i = 0; i < 20; i++) world.Progression.TryUpgrade(player.CharacterType, CharacterUpgradeKind.Level, out _);
            int raised = ((ICharacterProgress)world.Progression).GetLevel(player.CharacterType) - before;
            Require(raised > 0 && levelUps == raised, "Character level signal does not match real levels.");
        }
        ES3.DeleteFile(SaveFile);
    }

    // Kỳ đã lưu khác kỳ hiện tại thì reset; giữ nguyên khi cùng kỳ.
    private static void CheckReset()
    {
        MissionDataSO mission = Mission("m", new[] { (MissionObjectiveType.KillEnemy, 5) }, new CurrencyReward(CurrencyType.Gold, 1));
        MissionDatabaseSO daily = Database("daily", MissionCategory.Daily, null, null, mission);
        using (var world = new World(new[] { daily }))
        {
            for (int i = 0; i < 4; i++) world.Kill(CharacterTeam.Enemy);
            world.Game.Set(GameState.Playing);
            world.Game.Set(GameState.Home); // Bỏ trận giữa chừng vẫn ghi.
        }
        using (var world = new World(new[] { daily }))
            Require(world.Missions.Boards[0].Missions[0].GetProgress(0) == 4, "Progress was not saved when returning Home.");

        long today = Cycle(daily, DateTime.UtcNow);
        using (var save = OpenSave())
        {
            MissionsSaveData data = ((ISaveStore)save).Load(GameConstants.SaveKeys.Missions, new MissionsSaveData(), MissionsSaveData.Validate);
            data.boards.Single().cycle = today - 1;
            data.boards.Single().missions.Single().claimed = true;
            ((ISaveStore)save).Save(GameConstants.SaveKeys.Missions, data);
        }
        using (var world = new World(new[] { daily }))
        {
            MissionRuntime runtime = world.Missions.Boards[0].Missions[0];
            Require(runtime.GetProgress(0) == 0 && runtime.State == MissionState.InProgress, "A new day did not reset the board.");
        }
        ES3.DeleteFile(SaveFile);
    }

    // App ẩn/thoát giữa trận: Saving phải ghi tiến độ; đóng scene mà không có Saving (Reset Data) thì không được ghi.
    private static void CheckAppSuspend()
    {
        MissionDataSO mission = Mission("m", new[] { (MissionObjectiveType.KillEnemy, 50) }, new CurrencyReward(CurrencyType.Gold, 1));
        MissionDatabaseSO daily = Database("daily", MissionCategory.Daily, null, null, mission);
        using (var world = new World(new[] { daily }))
        {
            world.Game.Set(GameState.Playing);
            for (int i = 0; i < 25; i++) world.Kill(CharacterTeam.Enemy);
            world.Suspend();
            for (int i = 0; i < 5; i++) world.Kill(CharacterTeam.Enemy); // Sau lần ẩn, không ghi nữa.
        }
        using (var world = new World(new[] { daily }))
            Require(world.Missions.Boards[0].Missions[0].GetProgress(0) == 25, "App suspend did not save run progress, or disposing saved it.");
        ES3.DeleteFile(SaveFile);
    }

    // Final Mission đếm mission đã xong (không cần nhận), không tự đếm chính nó, lưu và nhận như mission thường.
    private static void CheckFinalMission()
    {
        MissionDataSO first = Mission("a", new[] { (MissionObjectiveType.KillEnemy, 1) }, new CurrencyReward(CurrencyType.Gold, 1));
        MissionDataSO second = Mission("b", new[] { (MissionObjectiveType.KillEnemy, 2) }, new CurrencyReward(CurrencyType.Gold, 1));
        MissionDataSO last = Mission("final", new[] { (MissionObjectiveType.CompleteMission, 2) }, new CurrencyReward(CurrencyType.Diamond, 5));
        MissionDatabaseSO daily = Database("daily", MissionCategory.Daily, null, null, first, second);
        using (var world = new World(new[] { daily })) // Save cũ: chưa có Final Mission.
        {
            world.Kill(CharacterTeam.Enemy);
            world.Suspend();
        }
        SetFinal(daily, last);
        daily.Validate();
        using (var world = new World(new[] { daily }))
        {
            MissionBoard board = world.Missions.Boards[0];
            Require(board.CompletedCount == 1 && board.NormalCount == 2 && board.FinalMission.GetProgress(0) == 1, "Old save did not count for the final mission.");
            world.Kill(CharacterTeam.Enemy);
            Require(board.CompletedCount == 2 && board.FinalMission.State == MissionState.Claimable, "Final mission did not finish.");
            Require(world.Missions.TryClaim(board.FinalMission, out _, out bool saved) && saved, "Final mission claim failed.");
            Require(board.CompletedCount == 2 && world.Missions.HasClaimable, "Claiming the final mission changed other missions.");
        }
        using (var world = new World(new[] { daily }))
            Require(world.Missions.Boards[0].FinalMission.State == MissionState.Claimed, "Final mission claim was not saved.");
        ES3.DeleteFile(SaveFile);

        SetFinal(daily, Mission("final", new[] { (MissionObjectiveType.CompleteMission, 3) }, new CurrencyReward(CurrencyType.Gold, 1)));
        bool rejected = false;
        try { daily.Validate(); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "An unreachable final mission was accepted.");
    }

    // Gói sai cấu hình phải bị chặn trước khi đổi bất kỳ thứ gì.
    private static void CheckInvalidRewards()
    {
        EquipmentDataSO item = Assets<EquipmentDatabaseSO>().Single().Items[0];
        MissionDataSO mission = Mission("m", new[] { (MissionObjectiveType.KillEnemy, 1) },
            new CurrencyReward(CurrencyType.Gold, 10),
            new EquipmentReward(item, EquipmentRarity.Epic, 10, 1, new FixedSubstats()));
        MissionDatabaseSO daily = Database("daily", MissionCategory.Daily, null, null, mission);
        using (var world = new World(new[] { daily }, true))
        {
            world.Kill(CharacterTeam.Enemy);
            MissionRuntime runtime = world.Missions.Boards[0].Missions[0];
            bool rejected = false;
            try { world.Missions.TryClaim(runtime, out _, out _); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Invalid reward was accepted.");
            Require(world.Progression.GetBalance(CurrencyType.Gold) == 0 && runtime.State == MissionState.Claimable,
                "Rejected reward changed the wallet or consumed the claim.");
        }
    }

    // Thưởng màn: thắng lần đầu, x2 đúng một lần (mảnh, trang bị, vàng nhặt), thắng lại và thua.
    private static void CheckStageRewards()
    {
        CharacterDataSO player = Assets<CharacterDataSO>().First(item => item.Team == CharacterTeam.Player);
        EquipmentDataSO item = Assets<EquipmentDatabaseSO>().Single().Items[0];
        StageDataSO stage = ScriptableObject.CreateInstance<StageDataSO>();
        Temporary.Add(stage);
        var data = new SerializedObject(stage);
        data.FindProperty("stageId").stringValue = "verification";
        SetRewards(data, "firstClearStageRewards", new CurrencyReward(CurrencyType.Diamond, 3));
        SetRewards(data, "victoryRewards", new CurrencyReward(CurrencyType.Gold, 50), new CharacterShardReward(player, 7),
            new EquipmentReward(item, EquipmentRarity.Rare, 1, 1, new RolledSubstats()));
        SetRewards(data, "defeatRewards", new CurrencyReward(CurrencyType.Gold, 10));
        data.ApplyModifiedPropertiesWithoutUndo();

        using (var world = new World(Array.Empty<MissionDatabaseSO>()))
        {
            ProgressionManager progression = world.Progression;
            foreach (StageDataSO configured in Assets<StageDataSO>()) new StageRewards(progression, progression).Validate(configured);
            long gold = progression.GetBalance(CurrencyType.Gold), diamond = progression.GetBalance(CurrencyType.Diamond);
            int items = progression.Items.Count;
            var rewards = new StageRewards(progression, progression);
            Require(!rewards.TryDouble(1, out _), "Double was granted before the result.");
            rewards.Grant(stage, true);
            rewards.Grant(stage, true);
            Require(progression.GetBalance(CurrencyType.Gold) == gold + 50 && progression.GetBalance(CurrencyType.Diamond) == diamond + 3 &&
                progression.Items.Count == items + 1 && progression.HasFirstCleared("verification"), "Victory rewards are wrong.");
            Require(rewards.TryDouble(5, out RewardResult bonus) && !rewards.TryDouble(5, out _), "Double must be granted exactly once.");
            Require(progression.GetBalance(CurrencyType.Gold) == gold + 105 && progression.GetBalance(CurrencyType.Diamond) == diamond + 6 &&
                progression.Items.Count == items + 2, "Double rewards are wrong.");
            var total = new RewardResult();
            total.Include(rewards.Result);
            total.Include(bonus);
            Require(total.Shards.Count == 1 && total.Shards[0].Amount == 14 && total.Equipment.Count == 2 && total.Currencies.Count == 2,
                "Combined result is wrong.");

            rewards.Reset();
            rewards.Grant(stage, true);
            Require(progression.GetBalance(CurrencyType.Diamond) == diamond + 6, "First clear reward was granted twice.");
            rewards.Reset();
            rewards.Grant(stage, false);
            Require(progression.GetBalance(CurrencyType.Gold) == gold + 165 && !rewards.TryDouble(1, out _), "Defeat rewards are wrong.");
        }
    }

    // Quái được chọn đích danh phải có thật, thuộc phe Enemy và không mâu thuẫn với nhóm (Boss/không phải Boss).
    private static void CheckKillTarget(MissionDataSO mission, MissionTask task)
    {
        if (task.Character == CharacterType.None) return;
        CharacterDataSO enemy = Assets<CharacterDataSO>().FirstOrDefault(item => item.CharacterType == task.Character);
        Require(enemy != null && enemy.Team == CharacterTeam.Enemy &&
            (task.EnemyKind == EnemyKind.Any || enemy.IsBoss == (task.EnemyKind == EnemyKind.Boss)),
            mission.name + "/" + task.Id + ": kill target does not exist or cannot match.");
    }

    // Một cái chết là một tín hiệu: mỗi mission con tự lọc theo nhóm và ID; UI chỉ được báo một lần mỗi việc.
    private static void CheckKillFilters()
    {
        CharacterDataSO[] enemies = Assets<CharacterDataSO>().Where(item => item.Team == CharacterTeam.Enemy).ToArray();
        CharacterDataSO boss = enemies.First(item => item.IsBoss);
        CharacterDataSO normal = enemies.First(item => !item.IsBoss);
        var objective = MissionObjectiveType.KillEnemy;
        MissionDataSO mission = Mission("m", new[] { (objective, 99), (objective, 99), (objective, 99), (objective, 99), (objective, 99) },
            new CurrencyReward(CurrencyType.Gold, 1));
        var data = new SerializedObject(mission);
        SetFilter(data, 1, EnemyKind.Normal, CharacterType.None);
        SetFilter(data, 2, EnemyKind.Elite, CharacterType.None);
        SetFilter(data, 3, EnemyKind.Boss, CharacterType.None);
        SetFilter(data, 4, EnemyKind.Any, normal.CharacterType);
        data.ApplyModifiedPropertiesWithoutUndo();
        MissionDataSO runs = Mission("r", new[] { (MissionObjectiveType.FinishRun, 9), (MissionObjectiveType.WinRun, 9), (MissionObjectiveType.CollectRunGold, 999) },
            new CurrencyReward(CurrencyType.Gold, 1));
        MissionDatabaseSO daily = Database("daily", MissionCategory.Daily, null, null, mission, runs);
        using (var world = new World(new[] { daily }))
        {
            int changes = 0;
            world.Missions.Changed += () => changes++;
            MissionRuntime runtime = world.Missions.Boards[0].Missions[0];
            world.Kill(normal, false);
            RequireProgress(runtime, 1, 1, 0, 0, 1);
            world.Kill(normal, true);
            RequireProgress(runtime, 2, 1, 1, 0, 2);
            world.Kill(boss, false);
            RequireProgress(runtime, 3, 1, 1, 1, 2);
            Require(changes == 3, "One death must notify exactly once.");
            world.RunData.Gold = 10;
            world.Game.Set(GameState.EndingWin);
            Require(changes == 4, "One finished run must notify exactly once.");
            world.Game.Set(GameState.Home);
            Require(changes == 4, "Returning Home without changes must not notify.");
        }
        ES3.DeleteFile(SaveFile);

        // Bộ lọc quái trên việc khác và KillBoss cũ phải bị từ chối.
        RequireInvalid(runs, data2 => SetFilter(data2, 0, EnemyKind.Boss, CharacterType.None));
        RequireInvalid(mission, data2 => data2.FindProperty("tasks").GetArrayElementAtIndex(0).FindPropertyRelative("objective").intValue =
            (int)MissionObjectiveType.KillBoss);
    }

    private static void SetFilter(SerializedObject data, int index, EnemyKind kind, CharacterType character)
    {
        SerializedProperty task = data.FindProperty("tasks").GetArrayElementAtIndex(index);
        task.FindPropertyRelative("enemyKind").intValue = (int)kind;
        task.FindPropertyRelative("character").intValue = (int)character;
    }

    private static void RequireProgress(MissionRuntime runtime, params int[] expected)
    {
        for (int i = 0; i < expected.Length; i++)
            Require(runtime.GetProgress(i) == expected[i], "Kill filter " + i + " expected " + expected[i] + " but was " + runtime.GetProgress(i) + ".");
    }

    private static void RequireInvalid(MissionDataSO mission, Action<SerializedObject> edit)
    {
        var data = new SerializedObject(mission);
        edit(data);
        data.ApplyModifiedPropertiesWithoutUndo();
        bool rejected = false;
        try { mission.Validate(); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "An invalid kill filter was accepted.");
    }

    private static void SetRewards(SerializedObject data, string field, params IReward[] rewards)
    {
        SerializedProperty list = data.FindProperty(field);
        list.arraySize = rewards.Length;
        for (int i = 0; i < rewards.Length; i++) list.GetArrayElementAtIndex(i).managedReferenceValue = rewards[i];
    }

    #endregion

    #region Test World

    // Save + Progression thật trên file tạm, gameplay giả.
    private sealed class World : IDisposable
    {
        private readonly SaveManager save;
        private readonly FakeCharacters characters = new FakeCharacters();
        public readonly ProgressionManager Progression;
        public readonly MissionManager Missions;
        public readonly FakeGame Game = new FakeGame();
        public readonly FakeRun RunData = new FakeRun();

        public World(IEnumerable<MissionDatabaseSO> databases, bool expectRewardErrors = false)
        {
            save = OpenSave();
            Progression = new ProgressionManager(save, Assets<CharacterDatabaseSO>().Single(), Assets<LevelDatabaseSO>().Single(),
                Assets<EquipmentDatabaseSO>().Single());
            Progression.Initialize();
            Missions = new MissionManager(new MissionManager.Settings(databases.ToList()), save, Progression, characters, characters,
                Game, RunData, Progression, Progression);
            // Thưởng sai cố ý: Initialize log lỗi là đúng, tắt log để không lẫn với lỗi thật.
            bool logs = Debug.unityLogger.logEnabled;
            if (expectRewardErrors) Debug.unityLogger.logEnabled = false;
            try { Missions.Initialize(); }
            finally { Debug.unityLogger.logEnabled = logs; }
        }

        public void Kill(CharacterTeam team) => characters.Kill(Assets<CharacterDataSO>().First(item => item.Team == team), false);
        public void Kill(CharacterDataSO template, bool elite) => characters.Kill(template, elite);

        // Đúng lời gọi của GameSceneInstaller khi app mất focus, xuống nền hoặc thoát.
        public void Suspend() => ((ISaveMaintenance)save).SaveIfDirty();

        public void Dispose()
        {
            Missions.Dispose();
            Progression.Dispose();
            save.Dispose();
        }
    }

    private static SaveManager OpenSave()
    {
        var save = new SaveManager(new SaveManager.Settings(SaveFile));
        save.Initialize();
        return save;
    }

    private sealed class FakeCharacters : ICharacterRegistry, ICharacterDeathEvents
    {
        private readonly Dictionary<CharacterView, CharacterDataSO> all = new Dictionary<CharacterView, CharacterDataSO>();
        public IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters => all;
        public event Action<CharacterView, CharacterDataSO> CharacterAdded { add { } remove { } }
        public event Action<CharacterView> CharacterRemoved { add { } remove { } }
        public event Action<CharacterView> CharacterDied;
        public event Action EnemyCelebrationsCompleted { add { } remove { } }
        public void PlayEnemyCelebrations() { }

        public void Kill(CharacterDataSO template, bool elite)
        {
            var shell = new GameObject("MissionVerification") { hideFlags = HideFlags.HideAndDontSave };
            shell.SetActive(false);
            Temporary.Add(shell);
            CharacterView view = shell.AddComponent<CharacterView>();
            // Setter là internal của game assembly, chỉ EnemySpawner dùng.
            if (elite) typeof(CharacterView).GetProperty(nameof(CharacterView.IsElite)).SetValue(view, true);
            all.Add(view, template);
            CharacterDied?.Invoke(view);
        }
    }

    private sealed class FakeGame : IGameState
    {
        public GameState State { get; private set; } = GameState.Boot;
        public Exception LastError => null;
        public event Action<GameState, GameState> StateChanged;

        public void Set(GameState next)
        {
            GameState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }

    private sealed class FakeRun : IRunReader
    {
        public int Level => 1;
        public int Experience => 0;
        public int ExperienceToNextLevel => 1;
        public int Gold { get; set; }
        public int ElapsedSeconds => 0;
        public int EnemiesDefeated => 0;
        public event Action<int, int> LevelUp { add { } remove { } }
        public event Action<int, int> ExperienceChanged { add { } remove { } }
        public event Action<int> GoldChanged { add { } remove { } }
        public event Action<int> TimeChanged { add { } remove { } }
        public event Action<int> EnemiesDefeatedChanged { add { } remove { } }
        public event Action RunReset { add { } remove { } }
    }

    #endregion

    #region Data Building

    private static MissionDataSO Mission(string id, (MissionObjectiveType objective, int target)[] tasks, params IReward[] rewards)
    {
        var mission = ScriptableObject.CreateInstance<MissionDataSO>();
        Temporary.Add(mission);
        var data = new SerializedObject(mission);
        data.FindProperty("id").stringValue = id;
        SerializedProperty taskList = data.FindProperty("tasks");
        taskList.arraySize = tasks.Length;
        for (int i = 0; i < tasks.Length; i++)
        {
            SerializedProperty task = taskList.GetArrayElementAtIndex(i);
            task.FindPropertyRelative("id").stringValue = "t" + i;
            task.FindPropertyRelative("objective").intValue = (int)tasks[i].objective;
            task.FindPropertyRelative("target").intValue = tasks[i].target;
        }
        SerializedProperty rewardList = data.FindProperty("rewards");
        rewardList.arraySize = rewards.Length;
        for (int i = 0; i < rewards.Length; i++) rewardList.GetArrayElementAtIndex(i).managedReferenceValue = rewards[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        return mission;
    }

    private static MissionDatabaseSO Database(string id, MissionCategory category, string start = null, string end = null,
        params MissionDataSO[] missions)
    {
        var database = ScriptableObject.CreateInstance<MissionDatabaseSO>();
        Temporary.Add(database);
        var data = new SerializedObject(database);
        data.FindProperty("id").stringValue = id;
        data.FindProperty("category").intValue = (int)category;
        data.FindProperty("eventStart").stringValue = start ?? string.Empty;
        data.FindProperty("eventEnd").stringValue = end ?? string.Empty;
        SerializedProperty list = data.FindProperty("missions");
        list.arraySize = missions.Length;
        for (int i = 0; i < missions.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = missions[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        database.Validate();
        return database;
    }

    private static void SetFinal(MissionDatabaseSO database, MissionDataSO mission)
    {
        var data = new SerializedObject(database);
        data.FindProperty("finalMission").objectReferenceValue = mission;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // Giá trị 777 vượt mọi khoảng roll để chắc chắn lúc load không bị sửa về mức roll.
    private static List<FixedSubstats.Line> FixedLines(EquipmentRulesSO rules, EquipmentSlot slot, EquipmentRarity rarity, int level)
        => rules.SubstatRolls.Where(roll => roll.Stat != rules.GetMainStat(slot)).Take(rules.GetExpectedSubstats(rarity, level))
            .Select((roll, index) => new FixedSubstats.Line { stat = roll.Stat, value = 777 + index }).ToList();

    private static long Cycle(MissionDatabaseSO database, DateTime now)
    {
        Require(database.TryGetCycle(now, out long cycle, out _), "Database should be open.");
        return cycle;
    }

    private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object
        => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Missions] " + message);
    }

    #endregion
}
