using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Lên lịch nhiều Elite và tối đa một Boss từ dữ liệu Stage.</summary>
public sealed class BossSpawnSystem : IStageSpawnSystem, IStageSpawnStatus, IUpdateStage
{
    private sealed class EliteRun
    {
        public BossSpawnDataSO.Entry entry;
        public bool spawned;
    }

    private readonly EnemySpawnManager spawner;
    private readonly StageSpawnRoster roster;
    private readonly BossHandleSummon summon;
    private readonly List<EliteRun> elites = new List<EliteRun>();
    private BossSpawnDataSO.Entry boss;
    private BossAppearanceCondition condition;
    private Vector2 arenaSize;
    private bool allWavesSpawned, finalWaveCleared, bossSpawned;

    public bool DeploymentComplete
    {
        get
        {
            foreach (EliteRun elite in elites) if (!elite.spawned) return false;
            return boss == null || bossSpawned;
        }
    }
    public int AliveCount => roster.AliveCount;

    /// <summary>Nhận spawner và dịch vụ summon đấu trường.</summary>
    public BossSpawnSystem(EnemySpawnManager spawner, BossHandleSummon summon)
    {
        this.spawner = spawner;
        this.summon = summon;
        roster = new StageSpawnRoster(spawner);
    }

    /// <summary>Kiểm tra entry Elite/Boss và nhân vật tương ứng.</summary>
    public void Validate(IStageSpawnData data)
    {
        var config = data as BossSpawnDataSO ?? throw new ArgumentException("BossSpawnSystem requires BossSpawnDataSO.");
        config.Validate();
        foreach (BossSpawnDataSO.Entry entry in config.entries) spawner.ValidateCharacter(entry.character);
    }

    /// <summary>Đăng ký các Elite theo StageTime và ghi nhận cấu hình điều kiện Boss.</summary>
    public void Execute(IStageSpawnData data)
    {
        var config = data as BossSpawnDataSO ?? throw new ArgumentException("BossSpawnSystem requires BossSpawnDataSO.");
        condition = config.bossAppearanceCondition;
        foreach (BossSpawnDataSO.Entry entry in config.entries)
        {
            if (entry.type == SpecialEnemyType.Elite) elites.Add(new EliteRun { entry = entry });
            else { boss = entry; arenaSize = config.arenaSize; }
        }
    }

    /// <summary>Nhận event Wave để kích hoạt điều kiện xuất hiện Boss.</summary>
    public void NotifyAllWavesSpawned() => allWavesSpawned = true;
    public void NotifyFinalWaveCleared() => finalWaveCleared = true;

    /// <summary>Spawn Elite tại mốc StageTime rồi summon Boss khi event Wave phù hợp đã tới.</summary>
    public void Tick(float stageTime, float deltaTime)
    {
        if (deltaTime <= 0f) return;
        foreach (EliteRun elite in elites)
        {
            if (elite.spawned || stageTime < elite.entry.eliteStageTime) continue;
            var view = spawner.SpawnElite(elite.entry.character, elite.entry.enemyLevel,
                spawner.ChoosePerimeterPoint(), elite.entry.eliteScale, elite.entry.eliteHealthMultiplier);
            roster.Add(view);
            elite.spawned = true;
        }
        if (boss == null || bossSpawned) return;
        bool ready = condition == BossAppearanceCondition.AfterAllWavesSpawned ? allWavesSpawned : finalWaveCleared;
        if (!ready) return;
        roster.Add(summon.Summon(boss.character, boss.enemyLevel, arenaSize));
        bossSpawned = true;
    }

    /// <summary>Dọn lượt đặc biệt và gỡ ranh giới đấu trường.</summary>
    public void Stop()
    {
        try { roster.Clear(); }
        finally
        {
            summon.Clear();
            elites.Clear();
            boss = null;
            allWavesSpawned = finalWaveCleared = bossSpawned = false;
        }
    }
}
