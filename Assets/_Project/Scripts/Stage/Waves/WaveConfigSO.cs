using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Cấu hình toàn bộ chuỗi wave quái thường, độc lập với Stage.</summary>
[CreateAssetMenu(fileName = "Waves", menuName = "Shooter Game/Data/Waves")]
public sealed class WaveConfigSO : ScriptableObject, IStageSpawnData
{
    #region Wave Sequence Data

    /// <summary>Thông số một wave: loại quái, quota, nhịp sinh và thời gian nghỉ.</summary>
    [Serializable]
    public sealed class Wave
    {
        [FormerlySerializedAs("duration")]
        [Tooltip("AfterDuration: chờ sau khi wave sinh đủ quái rồi chuyển wave tiếp theo. Wave cuối phải dọn sạch.")]
        [SerializeField][Min(0f)] private float delayAfterSpawn = 20f;
        [Tooltip("Tổng quái thường sinh trong wave; không bao gồm thành viên của Pattern hoặc Boss. Cấp quái lấy số thứ tự wave, bắt đầu từ 1.")]
        [SerializeField][Min(1)] private int spawnCount = 20;
        [Tooltip("Lần lượt: sinh theo Spawn Interval. Đồng thời: sinh toàn bộ quái trong cùng một lượt cập nhật.")]
        [SerializeField] private WaveSpawnMode spawnMode;
        [Tooltip("Số giây giữa hai quái khi chọn Sequential. Wave Simultaneous bỏ qua trường này.")]
        [SerializeField][Min(0.01f)] private float spawnInterval = 0.5f;
        [Tooltip("Các template Enemy thường có visual, không dùng Boss. Mỗi lần spawn chọn ngẫu nhiên một phần tử.")]
        [SerializeField] private CharacterDataSO[] enemies = Array.Empty<CharacterDataSO>();

        /// <summary>AfterDuration: chờ sau khi wave sinh đủ quái rồi chuyển wave tiếp theo. Wave cuối phải dọn sạch.</summary>
        public float DelayAfterSpawn => delayAfterSpawn;
        /// <summary>Tổng quái thường sinh trong wave; không bao gồm thành viên của Pattern hoặc Boss. Cấp quái lấy số thứ tự wave, bắt đầu từ 1.</summary>
        public int SpawnCount => spawnCount;
        /// <summary>Lần lượt: sinh theo Spawn Interval. Đồng thời: sinh toàn bộ quái trong cùng một lượt cập nhật.</summary>
        public WaveSpawnMode SpawnMode => spawnMode;
        /// <summary>Số giây giữa hai quái khi chọn Sequential. Wave Simultaneous bỏ qua trường này.</summary>
        public float SpawnInterval => spawnInterval;
        /// <summary>Các template Enemy thường có visual, không dùng Boss. Mỗi lần spawn chọn ngẫu nhiên một phần tử.</summary>
        public IReadOnlyList<CharacterDataSO> Enemies => enemies;

        /// <summary>
        /// Bản sao độc lập (kể cả mảng quái) để lượt chạy không bị ảnh hưởng nếu asset bị sửa trong Inspector lúc đang chơi.
        /// </summary>
        public Wave CreateSnapshot()
        {
            var copy = (Wave)MemberwiseClone();
            copy.enemies = (CharacterDataSO[])enemies.Clone();
            return copy;
        }
    }

    [Tooltip("AfterDuration: chuyển wave sau Delay After Spawn tính từ quái cuối. AfterEnemiesCleared: chờ hết quái của cả chuỗi; wave cuối luôn phải được dọn sạch.")]
    [SerializeField] private WaveAdvanceCondition advanceCondition;
    [Tooltip("Chuỗi chạy theo thứ tự từ Wave 1. Mỗi wave giữ quota, cách spawn và danh sách Enemy riêng.")]
    [SerializeField] private Wave[] waves = Array.Empty<Wave>();

    #endregion

    #region Wave Access

    public WaveAdvanceCondition AdvanceCondition => advanceCondition;
    public int Count => waves == null ? 0 : waves.Length;

    /// <summary>Lấy wave theo chỉ số bắt đầu từ 0; báo lỗi nếu vượt phạm vi.</summary>
    public Wave GetWave(int index)
    {
        if (waves == null || index < 0 || index >= waves.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return waves[index];
    }

    #endregion

    #region Validation

    /// <summary>Kiểm tra chuỗi, thời gian và danh sách quái thường của từng wave.</summary>
    public void Validate()
    {
        if (Count == 0 || !Enum.IsDefined(typeof(WaveAdvanceCondition), advanceCondition))
            throw new InvalidOperationException("WaveConfig cần danh sách wave và điều kiện chuyển hợp lệ.");
        foreach (var wave in waves)
        {
            if (wave == null || wave.SpawnCount < 1 || !Enum.IsDefined(typeof(WaveSpawnMode), wave.SpawnMode) ||
                (wave.SpawnMode == WaveSpawnMode.Sequential && !Positive(wave.SpawnInterval)) ||
                float.IsNaN(wave.DelayAfterSpawn) || float.IsInfinity(wave.DelayAfterSpawn) || wave.DelayAfterSpawn < 0f ||
                wave.Enemies == null || wave.Enemies.Count == 0)
                throw new InvalidOperationException("Mỗi wave cần số quái, nhịp spawn, delay và danh sách quái hợp lệ.");
            foreach (var enemy in wave.Enemies)
                if (enemy == null || enemy.Team != CharacterTeam.Enemy || enemy.IsBoss || enemy.VisualPrefab == null)
                    throw new InvalidOperationException("Wave chỉ chứa enemy thường có visual. Boss dùng dữ liệu riêng.");
        }
    }

    /// <summary>Kiểm tra số thực dương và hữu hạn.</summary>
    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    #endregion
}
