using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Cách chuyển wave: chờ thời gian hoặc dọn sạch quái của chuỗi.</summary>
public enum WaveAdvanceCondition { AfterDuration, AfterEnemiesCleared }

/// <summary>Cách sinh quái trong một wave.</summary>
public enum WaveSpawnMode
{
    [InspectorName("Lần lượt")] Sequential = 0,
    [InspectorName("Đồng thời")] Simultaneous = 1
}

/// <summary>Cấu hình toàn bộ chuỗi wave quái thường, độc lập với Stage.</summary>
[CreateAssetMenu(fileName = "Waves", menuName = "Shooter Game/Data/Waves")]
public sealed class WaveConfigSO : ScriptableObject, IStageSpawnData
{
    #region Dữ liệu chuỗi wave

    /// <summary>Thông số một wave: loại quái, quota, nhịp sinh và thời gian nghỉ.</summary>
    [Serializable]
    public sealed class Wave
    {
        [FormerlySerializedAs("duration")]
        [Tooltip("AfterDuration: chờ sau khi wave sinh đủ quái rồi chuyển wave tiếp theo. Wave cuối phải dọn sạch.")]
        [Min(0f)] public float delayAfterSpawn = 20f;
        [Tooltip("Tổng quái thường sinh trong wave; không bao gồm thành viên của Pattern hoặc Boss. Cấp quái lấy số thứ tự wave, bắt đầu từ 1.")]
        [Min(1)] public int spawnCount = 20;
        [Tooltip("Lần lượt: sinh theo Spawn Interval. Đồng thời: sinh toàn bộ quái trong cùng một lượt cập nhật.")]
        public WaveSpawnMode spawnMode;
        [Tooltip("Số giây giữa hai quái khi chọn Sequential. Wave Simultaneous bỏ qua trường này.")]
        [Min(0.01f)] public float spawnInterval = 0.5f;
        [Tooltip("Các template Enemy thường có visual, không dùng Boss. Mỗi lần spawn chọn ngẫu nhiên một phần tử.")]
        public CharacterDataSO[] enemies = Array.Empty<CharacterDataSO>();
    }

    [Tooltip("AfterDuration: chuyển wave sau Delay After Spawn tính từ quái cuối. AfterEnemiesCleared: chờ hết quái của cả chuỗi; wave cuối luôn phải được dọn sạch.")]
    [SerializeField] private WaveAdvanceCondition advanceCondition;
    [Tooltip("Chuỗi chạy theo thứ tự từ Wave 1. Mỗi wave giữ quota, cách spawn và danh sách Enemy riêng.")]
    [SerializeField] private Wave[] waves = Array.Empty<Wave>();

    #endregion

    #region Định tuyến và truy cập wave

    public Type SystemType => typeof(WaveSpawnSystem);
    /// <summary>Tạo WaveSpawnSystem bằng constructor injection qua container.</summary>
    public IStageSpawnSystem CreateSystem(Zenject.DiContainer container) => container.Instantiate<WaveSpawnSystem>();
    public WaveAdvanceCondition AdvanceCondition => advanceCondition;
    public int Count => waves == null ? 0 : waves.Length;

    /// <summary>Lấy wave theo chỉ số bắt đầu từ 0; báo lỗi nếu vượt phạm vi.</summary>
    public Wave GetWave(int index)
    {
        if (waves == null || index < 0 || index >= waves.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return waves[index];
    }

    #endregion

    #region Kiểm tra cấu hình

    /// <summary>Kiểm tra chuỗi, thời gian và danh sách quái thường của từng wave.</summary>
    public void Validate()
    {
        if (Count == 0 || !Enum.IsDefined(typeof(WaveAdvanceCondition), advanceCondition))
            throw new InvalidOperationException("WaveConfig cần danh sách wave và điều kiện chuyển hợp lệ.");
        foreach (var wave in waves)
        {
            if (wave == null || wave.spawnCount < 1 || !Enum.IsDefined(typeof(WaveSpawnMode), wave.spawnMode) ||
                (wave.spawnMode == WaveSpawnMode.Sequential && !Positive(wave.spawnInterval)) ||
                float.IsNaN(wave.delayAfterSpawn) || float.IsInfinity(wave.delayAfterSpawn) || wave.delayAfterSpawn < 0f ||
                wave.enemies == null || wave.enemies.Length == 0)
                throw new InvalidOperationException("Mỗi wave cần số quái, nhịp spawn, delay và danh sách quái hợp lệ.");
            foreach (var enemy in wave.enemies)
                if (enemy == null || enemy.Team != CharacterTeam.Enemy || enemy.IsBoss || enemy.VisualPrefab == null)
                    throw new InvalidOperationException("Wave chỉ chứa enemy thường có visual. Boss dùng dữ liệu riêng.");
        }
    }

    /// <summary>Kiểm tra số thực dương và hữu hạn.</summary>
    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    #endregion
}
