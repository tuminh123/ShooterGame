using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Stage chỉ điều phối wave, boss và kết quả. Không tạo hộ tống/đấu trường từ đội hình enemy thường.
public sealed class StageManager : ITickable, IDisposable
{
    #region Dependencies And Run State

    private readonly StageDataSO data;
    private readonly EnemySpawnManager spawner;
    private readonly GroundChunkManager ground;
    private readonly CharacterFactory factory;
    private readonly PlayerManager players;
    private readonly CurrencyService currencies;
    private readonly StageProgressService progress;
    private readonly List<CharacterView> bosses = new List<CharacterView>();
    private readonly Dictionary<CurrencyType, long> resultRewards = new Dictionary<CurrencyType, long>();
    private bool[] bossEntriesSpawned;
    private bool running;
    private bool waveSpawned;
    private bool deploymentFinished;
    private bool clearRequested;
    private bool resultGranted;
    private bool disposed;
    private int currentWave;
    private float deploymentFinishedAt;

    public StageDataSO CurrentStage => data;
    public int CurrentWaveNumber => currentWave + 1;
    public bool ResultGranted => resultGranted;
    public event Action Cleared;
    public event Action<Exception> Failed;

    #endregion

    #region Initialization And Configuration

    public StageManager(StageDataSO data, EnemySpawnManager spawner, GroundChunkManager ground,
        CharacterFactory factory, PlayerManager players, CurrencyService currencies, StageProgressService progress)
    {
        this.data = data;
        this.spawner = spawner;
        this.ground = ground;
        this.factory = factory;
        this.players = players;
        this.currencies = currencies;
        this.progress = progress;
        spawner.WaveSpawnCompleted += OnWaveSpawnCompleted;
    }

    public void Validate()
    {
        data.Validate();
        spawner.ValidateConfig(data.Waves);

        foreach (StageDataSO.BossEntry entry in data.Bosses)
        {
            spawner.ValidateCharacter(entry.boss);
        }

        BuildRewards(true);
        BuildRewards(false);
    }

    public void BeginRun()
    {
        EnsureNotDisposed();
        StopRun();
        ClearBosses();
        spawner.ResetRun();
        Validate();

        if (!ground.SetMap(data.MapKey))
        {
            throw new InvalidOperationException("Không thể áp dụng map của Stage: " + data.MapKey);
        }

        resultRewards.Clear();
        resultGranted = false;
        clearRequested = false;
        bossEntriesSpawned = new bool[data.Bosses.Count];
        running = true;
        BeginWave(0);
    }

    private void BeginWave(int index)
    {
        currentWave = index;
        waveSpawned = false;
        deploymentFinished = false;
        spawner.BeginWave(data.Waves, index);
    }

    private void OnWaveSpawnCompleted(int waveNumber)
    {
        if (running && waveNumber == CurrentWaveNumber)
        {
            waveSpawned = true;
        }
    }

    #endregion

    #region Stage Tick And Boss Timing

    public void Tick()
    {
        try
        {
            TickStage();
        }
        catch (Exception exception)
        {
            StopRun();

            if (Failed != null)
            {
                Failed(exception);
            }
            else
            {
                Debug.LogException(exception);
            }
        }
    }

    private void TickStage()
    {
        if (disposed || !running || Time.deltaTime <= 0f || players.RuntimeData == null || !players.RuntimeData.IsAlive)
        {
            return;
        }

        // Đầu wave được xét trước nhịp enemy đầu tiên; giữa/cuối được xét lại sau nhịp spawn.
        SpawnDueBosses();
        spawner.TickSpawning();

        if (!running || players.RuntimeData == null || !players.RuntimeData.IsAlive)
        {
            return;
        }

        SpawnDueBosses();

        if (!waveSpawned || !AllWaveBossesSpawned())
        {
            return;
        }

        if (!deploymentFinished)
        {
            deploymentFinished = true;
            deploymentFinishedAt = Time.time;
        }

        bool allCleared = spawner.AliveEnemyCount == 0 && CountLivingBosses() == 0;

        if (currentWave == data.Waves.Count - 1)
        {
            if (allCleared && !clearRequested)
            {
                clearRequested = true;
                running = false;
                Cleared?.Invoke();
            }

            return;
        }

        bool advance = data.Waves.AdvanceCondition == WaveAdvanceCondition.AfterDuration
            ? Time.time - deploymentFinishedAt >= data.Waves.GetWave(currentWave).delayAfterSpawn
            : allCleared;

        if (advance)
        {
            BeginWave(currentWave + 1);
        }
    }

    private void SpawnDueBosses()
    {
        int total = data.Waves.GetWave(currentWave).spawnCount;

        for (int i = 0; i < data.Bosses.Count; i++)
        {
            StageDataSO.BossEntry entry = data.Bosses[i];

            if (entry.wave != CurrentWaveNumber || bossEntriesSpawned[i] || !entry.CanSpawnAt(spawner.SpawnedInWave, total))
            {
                continue;
            }

            for (int count = 0; count < entry.count; count++)
            {
                CharacterView boss = spawner.SpawnBoss(entry.boss, CurrentWaveNumber);

                if (boss == null)
                {
                    throw new InvalidOperationException("Không thể spawn boss: " + entry.boss.name);
                }

                bosses.Add(boss);
                boss.Released += OnBossReleased;
            }

            bossEntriesSpawned[i] = true;
        }
    }

    private bool AllWaveBossesSpawned()
    {
        for (int i = 0; i < data.Bosses.Count; i++)
        {
            if (data.Bosses[i].wave == CurrentWaveNumber && !bossEntriesSpawned[i])
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Result And First Clear Rewards

    public void FinishRun(bool won)
    {
        EnsureNotDisposed();
        StopRun();
        if (resultGranted)
        {
            return;
        }
        Dictionary<CurrencyType, long> rewards = BuildRewards(won);
        // IO failures retain the combined snapshot; never apply the reward twice.
        if (won)
        {
            progress.GrantVictoryRewards(data.StageId, rewards);
        }
        else
        {
            currencies.AddBatch(rewards);
        }
        resultGranted = true;
        foreach (KeyValuePair<CurrencyType, long> reward in rewards)
        {
            resultRewards.Add(reward.Key, reward.Value);
        }
    }

    public long GetResultReward(CurrencyType type)
        => resultRewards.TryGetValue(type, out long amount) ? amount : 0L;

    // Thưởng lần đầu tra theo StageId đã lưu; resultGranted chỉ chống cấp trùng trong một lượt.
    private Dictionary<CurrencyType, long> BuildRewards(bool won)
    {
        var rewards = new Dictionary<CurrencyType, long>();
        if (won && !progress.HasFirstClearedStage(data.StageId))
        {
            AddRewards(rewards, data.FirstClearStageRewards);
        }
        AddRewards(rewards, won ? data.VictoryRewards : data.DefeatRewards);
        return rewards;
    }

    private static void AddRewards(Dictionary<CurrencyType, long> totals, IReadOnlyList<StageDataSO.CurrencyReward> rewards)
    {
        foreach (StageDataSO.CurrencyReward reward in rewards)
        {
            totals.TryGetValue(reward.type, out long previous);
            totals[reward.type] = checked(previous + reward.amount);
        }
    }


    #endregion

    #region Cleanup

    public void StopRun()
    {
        running = false;
        spawner.StopFormations();
    }

    private int CountLivingBosses()
    {
        int count = 0;

        foreach (CharacterView boss in bosses)
        {
            if (boss != null && boss.RuntimeData != null && boss.RuntimeData.IsAlive)
            {
                count++;
            }
        }

        return count;
    }

    private void OnBossReleased(CharacterView boss)
    {
        boss.Released -= OnBossReleased;
        bosses.Remove(boss);
    }

    private void ClearBosses()
    {
        while (bosses.Count > 0)
        {
            int last = bosses.Count - 1;
            CharacterView boss = bosses[last];
            bosses.RemoveAt(last);

            if (boss != null)
            {
                boss.Released -= OnBossReleased;
                factory.Despawn(boss);
            }
        }
    }

    private void EnsureNotDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(StageManager));
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        StopRun();
        spawner.WaveSpawnCompleted -= OnWaveSpawnCompleted;
        ClearBosses();
        spawner.ResetRun();
        Cleared = null;
        Failed = null;
        disposed = true;
    }

    #endregion
}
