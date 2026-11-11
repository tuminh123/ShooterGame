using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Điều phối map, wave, boss và thưởng; chỉ giữ tiến trình trong RAM, không sửa asset Stage.
public sealed class StageManager : ITickable, IDisposable
{
    private readonly StageDataSO data;
    private readonly EnemySpawnManager spawner;
    private readonly GroundChunkManager ground;
    private readonly CharacterFactory factory;
    private readonly PlayerManager players;
    private readonly CurrencyService currencies;
    private readonly List<CharacterView> bosses = new List<CharacterView>();
    private readonly Dictionary<CurrencyType, long> resultRewards = new Dictionary<CurrencyType, long>();
    private bool[] bossEntriesSpawned;
    private bool running, waveSpawned, waveDeploymentFinished, clearRequested, resultGranted, disposed;
    private float deploymentFinishedAt;
    private int currentWave;

    public StageDataSO CurrentStage => data;
    public int CurrentWaveNumber => currentWave + 1;
    public bool ResultGranted => resultGranted;
    public event Action Cleared;
    public event Action<Exception> Failed;

    public StageManager(StageDataSO data, EnemySpawnManager spawner, GroundChunkManager ground,
        CharacterFactory factory, PlayerManager players, CurrencyService currencies)
    {
        this.data = data; this.spawner = spawner; this.ground = ground;
        this.factory = factory; this.players = players; this.currencies = currencies;
        spawner.WaveSpawnCompleted += OnWaveSpawnCompleted;
    }

    public void Validate()
    {
        data.Validate();
        spawner.ValidateConfig(data.Waves);
        foreach (StageDataSO.BossEntry entry in data.Bosses) spawner.ValidateCharacter(entry.boss);
        // Kiểm tra tổng thưởng trước trận để không tràn số khi cấp kết quả.
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
        if (!ground.SetMap(data.MapKey)) throw new InvalidOperationException("Cannot apply stage map: " + data.MapKey);
        resultRewards.Clear();
        resultGranted = clearRequested = false;
        bossEntriesSpawned = new bool[data.Bosses.Count];
        running = true;
        BeginWave(0);
    }

    private void BeginWave(int index)
    {
        currentWave = index;
        waveSpawned = waveDeploymentFinished = false;
        spawner.BeginWave(data.Waves, index);
    }

    private void OnWaveSpawnCompleted(int waveNumber)
    {
        if (running && waveNumber == CurrentWaveNumber) waveSpawned = true;
    }

    public void Tick()
    {
        try { TickStage(); }
        catch (Exception exception)
        {
            StopRun();
            if (Failed != null) Failed(exception);
            else Debug.LogException(exception);
        }
    }

    private void TickStage()
    {
        if (disposed || !running || Time.deltaTime <= 0f || players.RuntimeData == null || !players.RuntimeData.IsAlive) return;
        spawner.TickSpawning();
        if (!running || !waveSpawned || players.RuntimeData == null || !players.RuntimeData.IsAlive) return;

        if (!waveDeploymentFinished)
        {
            // Mỗi entry kích hoạt đúng một lần; chỉ đếm nghỉ khi đã sinh đủ cả boss.
            for (int i = 0; i < data.Bosses.Count; i++)
            {
                StageDataSO.BossEntry entry = data.Bosses[i];
                if (entry.afterWave != CurrentWaveNumber || bossEntriesSpawned[i]) continue;
                for (int spawned = 0; spawned < entry.count; spawned++)
                {
                    CharacterView boss = spawner.SpawnBoss(entry.boss, CurrentWaveNumber);
                    if (boss == null) throw new InvalidOperationException("Could not spawn stage boss: " + entry.boss.name);
                    bosses.Add(boss);
                    boss.Released += OnBossReleased;
                }
                bossEntriesSpawned[i] = true;
            }
            waveDeploymentFinished = true;
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
        if (advance) BeginWave(currentWave + 1);
    }

    // GameManager gọi khi chốt thắng/thua, trước animation và panel; không phụ thuộc UI mở bao nhiêu lần.
    public void FinishRun(bool won)
    {
        EnsureNotDisposed();
        StopRun();
        if (resultGranted) return;
        Dictionary<CurrencyType, long> rewards = BuildRewards(won);
        currencies.AddBatch(rewards); // IO lỗi giữ pending snapshot; tuyệt đối không cấp lại giao dịch.
        resultGranted = true;
        foreach (KeyValuePair<CurrencyType, long> reward in rewards) resultRewards.Add(reward.Key, reward.Value);
    }

    public long GetResultReward(CurrencyType type)
        => resultRewards.TryGetValue(type, out long amount) ? amount : 0L;

    private Dictionary<CurrencyType, long> BuildRewards(bool won)
    {
        var rewards = new Dictionary<CurrencyType, long>();
        if (won) AddRewards(rewards, data.ClearRewards);
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

    public void StopRun() => running = false;

    private int CountLivingBosses()
    {
        int count = 0;
        foreach (CharacterView boss in bosses)
            if (boss != null && boss.RuntimeData != null && boss.RuntimeData.IsAlive) count++;
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
            if (boss == null) continue;
            boss.Released -= OnBossReleased;
            factory.Despawn(boss);
        }
    }

    private void EnsureNotDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(StageManager));
    }

    public void Dispose()
    {
        if (disposed) return;
        StopRun();
        spawner.WaveSpawnCompleted -= OnWaveSpawnCompleted;
        ClearBosses();
        spawner.ResetRun();
        Cleared = null;
        Failed = null;
        disposed = true;
    }
}
