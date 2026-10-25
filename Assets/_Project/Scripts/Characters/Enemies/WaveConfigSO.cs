using System;
using UnityEngine;

// * Các wave chạy nối tiếp theo thời gian game; wave cuối tiếp tục chạy đến khi lượt chơi kết thúc.
[CreateAssetMenu(fileName = "Waves", menuName = "Shooter Game/Data/Waves")]
public sealed class WaveConfigSO : ScriptableObject
{
    [Serializable]
    public sealed class Wave
    {
        [Min(0.01f)] public float duration = 60f; // * Thời lượng wave tính bằng giây game, pause sẽ dừng tiến trình.
        [Min(0.01f)] public float spawnInterval = 0.5f; // * Khoảng cách giữa hai lượt spawn, tối đa một con mỗi lượt.
        [Min(1)] public int maxAliveEnemies = 20; // * Trần tổng số quái đang sống, bao gồm quái còn lại từ wave trước.
        public CharacterDataSO[] enemies = Array.Empty<CharacterDataSO>(); // * Các loại được phép xuất hiện; chọn ngẫu nhiên đều giữa các phần tử.
    }

    [SerializeField] private Wave[] waves = Array.Empty<Wave>(); // * Thứ tự trong Inspector cũng là thứ tự diễn ra trong trận.

    public Wave GetWave(float elapsed)
    {
        foreach (Wave wave in waves)
        {
            if (elapsed < wave.duration)
                return wave;
            elapsed -= wave.duration;
        }
        return waves.Length > 0 ? waves[waves.Length - 1] : null;
    }

    // * Kiểm tra khi vào game thay vì lặp kiểm tra toàn bộ cấu hình trong mỗi frame.
    public void Validate()
    {
        if (waves.Length == 0)
            throw new InvalidOperationException("Wave config needs at least one wave.");
        foreach (Wave wave in waves)
        {
            if (wave == null || !(wave.duration > 0f) || !(wave.spawnInterval > 0f) ||
                wave.maxAliveEnemies < 1 || wave.enemies == null || wave.enemies.Length == 0)
                throw new InvalidOperationException("Each wave needs positive timing, an alive limit and enemy data.");
            foreach (CharacterDataSO enemy in wave.enemies)
                if (enemy == null || enemy.Team != CharacterTeam.Enemy || enemy.VisualPrefab == null)
                    throw new InvalidOperationException("Waves can only spawn Enemy data with a visual prefab.");
        }
    }
}
