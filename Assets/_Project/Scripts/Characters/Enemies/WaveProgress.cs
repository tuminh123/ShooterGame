// Chỉ đếm lượt spawn quái thường của wave; StageManager quyết định chuyển wave và kết quả màn.
public sealed class WaveProgress
{
    #region Current Wave Counters

    public int WaveIndex { get; private set; }
    public int EnemyLevel => WaveIndex + 1;
    // Bao gồm enemy của Path, viền và hộ tống; không bao gồm boss hoặc quái sống sót từ Wave cũ.
    public int SpawnedInWave { get; private set; }

    #endregion

    #region Reset And Spawn Budget

    public void Reset() => BeginWave(0);

    public void BeginWave(int index)
    {
        WaveIndex = index;
        SpawnedInWave = 0;
    }

    public bool CanSpawn(WaveConfigSO config)
        => SpawnedInWave < config.GetWave(WaveIndex).spawnCount;

    // Chỉ gọi sau khi factory spawn thành công, tránh dùng hết ngân sách khi chưa có nhân vật.
    public void RecordSpawn() => SpawnedInWave++;

    #endregion
}
