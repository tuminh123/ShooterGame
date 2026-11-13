using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tự quản lý sinh quái và chuyển wave cho từng chuỗi được giao.</summary>
public sealed class WaveSpawnSystem : IStageSpawnSystem, IStageSpawnStatus, IUpdateStage
{
    /// <summary>Phát khi toàn bộ chuỗi Wave đã spawn hoặc Wave cuối đã sạch quái.</summary>
    public event Action AllWavesSpawned;
    public event Action FinalWaveCleared;
    private bool allWavesSpawnedRaised, finalWaveClearedRaised;
    #region Trạng thái các chuỗi

    /// <summary>Giữ wave hiện tại, quota, mốc thời gian Stage và roster riêng.</summary>
    private sealed class Run
    {
        public WaveConfigSO.Wave[] waves;
        public WaveAdvanceCondition condition;
        public StageSpawnRoster roster;
        public int index, spawned;
        public float nextSpawnAt, nextWaveAt;
        public bool finished;
    }

    private readonly EnemySpawnManager spawner;
    private readonly List<Run> runs = new List<Run>();

    #endregion

    #region Dependency và thống kê

    /// <summary>Nhận spawner chung; các chuỗi sẽ giữ roster riêng khi bắt đầu.</summary>
    public WaveSpawnSystem(EnemySpawnManager spawner) { this.spawner = spawner; }
    /// <summary>True khi mọi chuỗi đã sinh đủ quái của wave cuối.</summary>
    public bool DeploymentComplete
    {
        get { foreach (var run in runs) if (!run.finished) return false; return true; }
    }
    /// <summary>Tổng quái còn sống của mọi chuỗi, gồm cả quái từ wave trước.</summary>
    public int AliveCount
    {
        get { int count = 0; foreach (var run in runs) count += run.roster.AliveCount; return count; }
    }

    #endregion

    #region Kiểm tra và bắt đầu chuỗi

    /// <summary>Kiểm tra WaveConfigSO, vùng spawn và cấu hình cấp của các loại quái.</summary>
    public void Validate(IStageSpawnData data)
    {
        var config = data as WaveConfigSO ?? throw new ArgumentException("WaveSpawnSystem requires WaveConfigSO.");
        config.Validate();
        spawner.ValidatePerimeter();
        for (int i = 0; i < config.Count; i++)
            foreach (var enemy in config.GetWave(i).enemies) spawner.ValidateCharacter(enemy);
    }

    /// <summary>Chụp cấu hình toàn bộ chuỗi rồi tạo một lượt chạy có tiến trình độc lập.</summary>
    public void Execute(IStageSpawnData data)
    {
        var config = data as WaveConfigSO ?? throw new ArgumentException("WaveSpawnSystem requires WaveConfigSO.");
        var snapshots = new WaveConfigSO.Wave[config.Count];
        for (int i = 0; i < snapshots.Length; i++)
        {
            var wave = config.GetWave(i);
            snapshots[i] = new WaveConfigSO.Wave
            {
                spawnCount = wave.spawnCount, spawnInterval = wave.spawnInterval,
                delayAfterSpawn = wave.delayAfterSpawn, enemies = (CharacterDataSO[])wave.enemies.Clone()
            };
        }
        runs.Add(new Run { waves = snapshots, condition = config.AdvanceCondition, roster = new StageSpawnRoster(spawner) });
    }

    #endregion

    #region Spawn và chuyển wave

    /// <summary>Sinh bù các mốc đã đến và chuyển wave trên đồng hồ Stage.</summary>
    public void Tick(float stageTime, float deltaTime)
    {
        if (deltaTime <= 0f) return;
        bool finalAlive = false;
        foreach (var run in runs)
        {
            while (!run.finished)
            {
                var wave = run.waves[run.index];
                if (run.spawned < wave.spawnCount)
                {
                    if (stageTime < run.nextSpawnAt) break;
                    var enemy = spawner.Spawn(wave.enemies[UnityEngine.Random.Range(0, wave.enemies.Length)],
                        run.index + 1, spawner.ChoosePerimeterPoint());
                    run.roster.Add(enemy);
                    run.spawned++;
                    run.nextWaveAt = run.nextSpawnAt + wave.delayAfterSpawn;
                    run.nextSpawnAt += wave.spawnInterval;
                    if (run.spawned == wave.spawnCount && run.index == run.waves.Length - 1)
                        run.finished = true;
                    continue;
                }

                bool advance = run.condition == WaveAdvanceCondition.AfterDuration
                    ? stageTime >= run.nextWaveAt : run.roster.AliveCount == 0;
                if (!advance) break;
                run.index++;
                run.spawned = 0;
                run.nextSpawnAt = run.condition == WaveAdvanceCondition.AfterDuration
                    ? run.nextWaveAt : stageTime;
            }
            if (run.index == run.waves.Length - 1 && run.roster.AliveCount > 0) finalAlive = true;
        }
        bool allSpawned = true;
        foreach (var run in runs) if (!run.finished) { allSpawned = false; break; }
        if (allSpawned && !allWavesSpawnedRaised) { allWavesSpawnedRaised = true; AllWavesSpawned?.Invoke(); }
        if (allSpawned && !finalAlive && !finalWaveClearedRaised)
        { finalWaveClearedRaised = true; FinalWaveCleared?.Invoke(); }
    }

    #endregion

    #region Vòng đời và cleanup

    /// <summary>Dừng mọi chuỗi và dọn roster; gom lỗi cleanup để không bỏ sót chuỗi khác.</summary>
    public void Stop()
    {
        List<Exception> failures = null;
        foreach (var run in runs)
        {
            try { run.roster.Clear(); }
            catch (Exception e)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(e);
            }
        }
        runs.Clear();
        allWavesSpawnedRaised = finalWaveClearedRaised = false;
        if (failures != null) throw new AggregateException("Wave cleanup failed.", failures);
    }
    #endregion
}
