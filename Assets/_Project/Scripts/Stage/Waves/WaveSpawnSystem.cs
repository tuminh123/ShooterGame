using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tự quản lý sinh quái và chuyển wave cho từng chuỗi được giao. Do StageManager tạo và tick; không bind vào Zenject.</summary>
internal sealed class WaveSpawnSystem
{
    /// <summary>Phát khi toàn bộ chuỗi Wave đã spawn hoặc Wave cuối đã sạch quái.</summary>
    public event Action AllWavesSpawned;
    public event Action FinalWaveCleared;
    // Chỉ công bố lịch gameplay; UI tự chọn thời điểm cảnh báo, không dời lịch spawn.
    public event Action<StageSpawnWarning, float> SpawnScheduled;
    private bool allWavesSpawnedRaised, finalWaveClearedRaised;
    #region Sequence State

    /// <summary>Giữ wave hiện tại, quota, mốc thời gian Stage và roster riêng.</summary>
    private sealed class ActiveWave
    {
        public WaveConfigSO.Wave[] waves;
        public WaveAdvanceCondition condition;
        public StageSpawnRoster roster;
        public int index, spawned;
        public float nextSpawnAt, nextWaveAt;
        public bool finished;
    }
    private readonly EnemySpawner spawner; // Tạo/thu hồi quái, vùng sinh; do StageManager tạo và dùng chung cho mọi system.
    private readonly List<ActiveWave> activeWaves = new List<ActiveWave>();

    #endregion

    #region Dependencies & Counts

    /// <summary>Nhận spawner chung; các chuỗi sẽ giữ roster riêng khi bắt đầu.</summary>
    public WaveSpawnSystem(EnemySpawner spawner)
    {
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
    }
    /// <summary>True khi mọi chuỗi đã sinh đủ quái của wave cuối.</summary>
    public bool DeploymentComplete
    {
        get { foreach (var active in activeWaves) if (!active.finished) return false; return true; }
    }
    /// <summary>Tổng quái còn sống của mọi chuỗi, gồm cả quái từ wave trước.</summary>
    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (var active in activeWaves) count += active.roster.AliveCount;
            return count;
        }
    }

    #endregion

    #region Validation & Sequence Start

    /// <summary>Kiểm tra WaveConfigSO, vùng spawn và cấu hình cấp của các loại quái.</summary>
    public void Validate(WaveConfigSO config)
    {
        config.Validate();
        spawner.ValidatePerimeter();
        for (int i = 0; i < config.Count; i++)
            foreach (var enemy in config.GetWave(i).Enemies) spawner.ValidateCharacter(enemy);
    }

    /// <summary>Chụp cấu hình toàn bộ chuỗi rồi tạo một lượt chạy có tiến trình độc lập.</summary>
    public void Execute(WaveConfigSO config)
    {
        var snapshots = new WaveConfigSO.Wave[config.Count];
        for (int i = 0; i < snapshots.Length; i++)
            snapshots[i] = config.GetWave(i).CreateSnapshot();
        activeWaves.Add(new ActiveWave { waves = snapshots, condition = config.AdvanceCondition, roster = new StageSpawnRoster(spawner) });
        // Chuỗi theo giờ biết trước mọi mốc. Chuỗi chờ dọn quái chỉ biết chắc wave đầu.
        float startsAt = 0f;
        for (int i = 0; i < snapshots.Length; i++)
        {
            var wave = snapshots[i];
            if (wave.SpawnMode == WaveSpawnMode.Simultaneous)
                SpawnScheduled?.Invoke(StageSpawnWarning.SimultaneousWave, startsAt);
            if (config.AdvanceCondition != WaveAdvanceCondition.AfterDuration) break;
            startsAt += SpawnSpan(wave) + wave.DelayAfterSpawn;
        }
    }

    // Thời gian sinh con cuối; wave đồng thời sinh toàn bộ tại đầu wave.
    private static float SpawnSpan(WaveConfigSO.Wave wave)
        => wave.SpawnMode == WaveSpawnMode.Sequential ? (wave.SpawnCount - 1) * wave.SpawnInterval : 0f;

    /// <summary>Trả mốc sinh xong cả chuỗi nếu lịch không phụ thuộc thời điểm quái chết.</summary>
    public static float? GetScheduledCompletion(WaveConfigSO config)
    {
        if (config.Count == 0 || (config.Count > 1 && config.AdvanceCondition != WaveAdvanceCondition.AfterDuration))
            return null;
        float time = 0f;
        for (int i = 0; i < config.Count; i++)
        {
            var wave = config.GetWave(i);
            time += SpawnSpan(wave);
            if (i < config.Count - 1) time += wave.DelayAfterSpawn;
        }
        return time;
    }

    #endregion

    #region Spawning & Wave Advance

    /// <summary>Sinh bù các mốc đã đến và chuyển wave trên đồng hồ Stage.</summary>
    public void Tick(float stageTime, float deltaTime)
    {
        // Màn không có wave: không phát AllWavesSpawned/FinalWaveCleared (giữ đúng hành vi khi system chưa tồn tại).
        if (deltaTime <= 0f || activeWaves.Count == 0) return;
        bool finalAlive = false;
        foreach (var active in activeWaves)
        {
            while (!active.finished)
            {
                var wave = active.waves[active.index];
                if (active.spawned < wave.SpawnCount)
                {
                    if (stageTime < active.nextSpawnAt) break;
                    var enemy = spawner.Spawn(wave.Enemies[UnityEngine.Random.Range(0, wave.Enemies.Count)],
                        active.index + 1, spawner.ChoosePerimeterPoint());
                    active.roster.Add(enemy);
                    active.spawned++;
                    active.nextWaveAt = active.nextSpawnAt + wave.DelayAfterSpawn;
                    if (wave.SpawnMode == WaveSpawnMode.Sequential) active.nextSpawnAt += wave.SpawnInterval;
                    if (active.spawned == wave.SpawnCount && active.index == active.waves.Length - 1)
                        active.finished = true;
                    continue;
                }

                bool advance = active.condition == WaveAdvanceCondition.AfterDuration
                    ? stageTime >= active.nextWaveAt : active.roster.AliveCount == 0;
                if (!advance) break;
                active.index++;
                active.spawned = 0;
                active.nextSpawnAt = active.condition == WaveAdvanceCondition.AfterDuration
                    ? active.nextWaveAt : stageTime;
                if (active.condition == WaveAdvanceCondition.AfterEnemiesCleared &&
                    active.waves[active.index].SpawnMode == WaveSpawnMode.Simultaneous)
                    SpawnScheduled?.Invoke(StageSpawnWarning.SimultaneousWave, stageTime);
            }
            if (active.index == active.waves.Length - 1 && active.roster.AliveCount > 0) finalAlive = true;
        }
        bool allSpawned = true;
        foreach (var active in activeWaves) if (!active.finished) { allSpawned = false; break; }
        if (allSpawned && !allWavesSpawnedRaised) { allWavesSpawnedRaised = true; AllWavesSpawned?.Invoke(); }
        if (allSpawned && !finalAlive && !finalWaveClearedRaised)
        { finalWaveClearedRaised = true; FinalWaveCleared?.Invoke(); }
    }

    #endregion

    #region Lifecycle & Cleanup

    /// <summary>Dừng mọi chuỗi và dọn roster; gom lỗi cleanup để không bỏ sót chuỗi khác.</summary>
    public void Stop()
    {
        List<Exception> failures = null;
        foreach (var active in activeWaves)
        {
            try { active.roster.Clear(); }
            catch (Exception e)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(e);
            }
        }
        activeWaves.Clear();
        allWavesSpawnedRaised = finalWaveClearedRaised = false;
        if (failures != null) throw new AggregateException("Wave cleanup failed.", failures);
    }
    #endregion
}
