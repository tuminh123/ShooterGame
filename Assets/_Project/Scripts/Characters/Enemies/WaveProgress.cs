// Chỉ đếm lượt spawn quái thường của wave; StageManager quyết định chuyển wave và kết quả màn.
public sealed class WaveProgress
{
    public int WaveIndex { get; private set; }
    public int EnemyLevel => WaveIndex + 1;
    public int SpawnedInWave { get; private set; }

    public void Reset() => BeginWave(0);

    public void BeginWave(int index)
    {
        WaveIndex = index;
        SpawnedInWave = 0;
    }

    public bool CanSpawn(WaveConfigSO config)
        => SpawnedInWave < config.GetWave(WaveIndex).spawnCount;

    public void RecordSpawn() => SpawnedInWave++;
}
