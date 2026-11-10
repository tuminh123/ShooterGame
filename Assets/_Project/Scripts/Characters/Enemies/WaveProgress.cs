using System;

// Đi lần lượt đến wave cuối rồi lặp wave cuối; cấp enemy tăng sau mỗi đợt hoàn thành.
public sealed class WaveProgress
{
    public int WaveIndex { get; private set; }
    public int CompletedWaves { get; private set; }
    public int EnemyLevel => checked(CompletedWaves + 1);
    public int SpawnedInWave { get; private set; }
    private double waveStartedAt;

    public void Reset(float gameTime)
    {
        WaveIndex = CompletedWaves = SpawnedInWave = 0;
        waveStartedAt = gameTime;
    }

    public bool Advance(WaveConfigSO config, float gameTime, int aliveEnemies)
    {
        int previous = CompletedWaves;
        switch (config.AdvanceCondition)
        {
            case WaveAdvanceCondition.AfterDuration:
                while (WaveIndex < config.Count - 1 && gameTime - waveStartedAt >= config.GetWave(WaveIndex).duration)
                    MoveNext(config.Count, waveStartedAt + config.GetWave(WaveIndex).duration);
                // Wave cuối vẫn spawn liên tục; mỗi chu kỳ Duration tăng thêm một cấp.
                if (WaveIndex == config.Count - 1)
                {
                    double duration = config.GetWave(WaveIndex).duration;
                    int repeats = checked((int)Math.Floor(Math.Max(0d, gameTime - waveStartedAt) / duration));
                    if (repeats > 0)
                    {
                        CompletedWaves = checked(CompletedWaves + repeats);
                        waveStartedAt += repeats * duration;
                        SpawnedInWave = 0;
                    }
                }
                break;
            case WaveAdvanceCondition.AfterEnemiesCleared:
                if (SpawnedInWave >= config.GetWave(WaveIndex).spawnCount && aliveEnemies == 0)
                    MoveNext(config.Count, gameTime);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(config.AdvanceCondition));
        }
        // Lặp wave cuối vẫn là một đợt mới dù WaveIndex không đổi.
        return previous != CompletedWaves;
    }

    private void MoveNext(int count, double startedAt)
    {
        CompletedWaves = checked(CompletedWaves + 1);
        WaveIndex = Math.Min(WaveIndex + 1, count - 1);
        SpawnedInWave = 0;
        waveStartedAt = startedAt;
    }

    public bool CanSpawn(WaveConfigSO config)
    {
        switch (config.AdvanceCondition)
        {
            case WaveAdvanceCondition.AfterDuration: return true;
            case WaveAdvanceCondition.AfterEnemiesCleared: return SpawnedInWave < config.GetWave(WaveIndex).spawnCount;
            default: throw new ArgumentOutOfRangeException(nameof(config.AdvanceCondition));
        }
    }

    public void RecordSpawn() => SpawnedInWave++;
}
