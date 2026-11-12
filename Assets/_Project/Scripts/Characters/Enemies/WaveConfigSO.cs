using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum WaveAdvanceCondition
{
    AfterDuration,
    AfterEnemiesCleared
}

[CreateAssetMenu(fileName = "Waves", menuName = "Shooter Game/Data/Waves")]
public sealed class WaveConfigSO : ScriptableObject
{
    #region Wave Data

    [Serializable]
    public sealed class Wave
    {
        [FormerlySerializedAs("duration")]
        [Tooltip("Thời gian chờ sau khi spawn đủ, dùng cho chế độ chuyển wave theo thời gian. Wave cuối vẫn phải dọn hết địch.")]
        [Min(0f)] public float delayAfterSpawn = 20f;

        [Tooltip("Tổng enemy thường của wave. Boss và quân riêng của boss không thuộc tổng này.")]
        [Min(1)] public int spawnCount = 20;

        [Tooltip("Khoảng cách giữa hai lần spawn theo giây gameplay. Không có đội hình: một con mỗi lần. Đến lịch đội hình: một nhóm mỗi lần; trước/sau lịch: một con thường mỗi lần.")]
        [Min(0.01f)] public float spawnInterval = 0.5f;

        [Tooltip("Các loại enemy thường được chọn ngẫu nhiên. Boss được cấu hình riêng trên StageDataSO.")]
        public CharacterDataSO[] enemies = Array.Empty<CharacterDataSO>();

        [Tooltip("Số enemy thường xuất hiện trước lượt đội hình đầu tiên. Tối đa = tổng quái − tổng slot của tất cả lượt đội hình. Không tính số quái đã giết.")]
        [Min(0)] public int formationSpawnAfter;

        [Tooltip("Danh sách theo thứ tự spawn. Mỗi phần tử là một lần spawn, có Formation/Path/thông số riêng. Để rỗng nếu toàn Wave spawn thường.")]
        public EnemyFormationSpawn[] formationSpawns = Array.Empty<EnemyFormationSpawn>();

        public bool UsesFormation => formationSpawns != null && formationSpawns.Length > 0;

        // Inspector, spawner và Workbench dùng chung ngân sách: cộng số slot từng lượt, không nhân một hình mặc định.
        public FormationSpawnPlan GetFormationSpawnPlan()
        {
            if (spawnCount < 1)
            {
                throw new InvalidOperationException("Wave cần tổng quái ít nhất 1.");
            }

            int count = UsesFormation ? formationSpawns.Length : 0;
            var slots = new int[count];
            long budget = 0;

            for (int i = 0; i < count; i++)
            {
                EnemyFormationSpawn entry = formationSpawns[i];

                if (entry == null)
                {
                    throw new InvalidOperationException($"Lượt đội hình {i + 1} đang trống.");
                }

                try
                {
                    entry.Validate();
                    slots[i] = entry.formation.SlotCount;
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException($"Lượt đội hình {i + 1}: {exception.Message}", exception);
                }

                budget += slots[i];
            }

            if (budget > spawnCount)
            {
                throw new InvalidOperationException($"{count} lượt đội hình cần tổng {budget} enemy nhưng Wave chỉ có {spawnCount}. Giảm lượt/cấp hình hoặc tăng tổng quái.");
            }

            int before = UsesFormation ? formationSpawnAfter : 0;
            int maxAfter = spawnCount - (int)budget;

            if (before < 0 || before > maxAfter)
            {
                throw new InvalidOperationException($"Spawn After phải từ 0 đến {maxAfter}. Cần dành {budget} enemy cho {count} lượt đội hình.");
            }

            return new FormationSpawnPlan(slots, before, (int)budget, spawnCount);
        }
    }

    #endregion

    #region Formation Spawn Budget

    // Các lượt có kích thước khác nhau nhưng vẫn tạo một khối lịch liên tục.
    // Lấy lượt từ tổng đã spawn để không cần thêm bộ đếm phải reset riêng.
    public readonly struct FormationSpawnPlan
    {
        private readonly int[] slots;
        public int GroupCount => slots.Length;
        public int Before { get; }
        public int After { get; }
        public int FormationEnemyCount { get; }
        public int MaxSpawnAfter => Before + After;

        internal FormationSpawnPlan(int[] slots, int before, int budget, int total)
        {
            this.slots = slots;
            Before = before;
            FormationEnemyCount = budget;
            After = total - before - budget;
        }

        public bool TryGetFormationIndex(int spawnedEnemies, out int index)
        {
            index = -1;

            if (spawnedEnemies < Before || spawnedEnemies >= Before + FormationEnemyCount)
            {
                return false;
            }

            int cursor = Before;

            for (int i = 0; i < slots.Length; i++)
            {
                if (spawnedEnemies == cursor)
                {
                    index = i;
                    return true;
                }

                cursor += slots[i];
            }

            // Không âm thầm bỏ qua lượt nếu lịch bị chỉnh giữa Wave, làm tiến trình nằm giữa một nhóm.
            throw new InvalidOperationException("Tiến trình spawn không khớp lịch đội hình. Hãy áp dụng cấu hình trước khi bắt đầu Wave.");
        }
    }

    #endregion

    #region Sequence Access

    [Tooltip("AfterDuration: chờ thời gian rồi chuyển. AfterEnemiesCleared: chờ dọn sạch. Wave cuối luôn phải dọn sạch.")]
    [SerializeField] private WaveAdvanceCondition advanceCondition;

    [SerializeField] private Wave[] waves = Array.Empty<Wave>();

    public WaveAdvanceCondition AdvanceCondition => advanceCondition;
    public int Count => waves == null ? 0 : waves.Length;

    public Wave GetWave(int index)
    {
        if (waves == null || index < 0 || index >= waves.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return waves[index];
    }

    #endregion

    #region Configuration Validation

    public void Validate()
    {
        if (Count == 0 || !Enum.IsDefined(typeof(WaveAdvanceCondition), advanceCondition))
        {
            throw new InvalidOperationException("WaveConfig cần ít nhất một wave và cách chuyển wave hợp lệ.");
        }

        foreach (Wave wave in waves)
        {
            if (wave == null || wave.spawnCount < 1 || !Positive(wave.spawnInterval) || wave.enemies == null || wave.enemies.Length == 0)
            {
                throw new InvalidOperationException("Mỗi wave cần tổng quái, nhịp spawn và danh sách enemy hợp lệ.");
            }

            if (advanceCondition == WaveAdvanceCondition.AfterDuration &&
                (float.IsNaN(wave.delayAfterSpawn) || float.IsInfinity(wave.delayAfterSpawn) || wave.delayAfterSpawn < 0f))
            {
                throw new InvalidOperationException("Thời gian nghỉ giữa wave phải hữu hạn và không âm.");
            }

            wave.GetFormationSpawnPlan();

            foreach (CharacterDataSO enemy in wave.enemies)
            {
                if (enemy == null || enemy.Team != CharacterTeam.Enemy || enemy.IsBoss || enemy.VisualPrefab == null)
                {
                    throw new InvalidOperationException("Wave chỉ chứa enemy thường có visual. Boss thuộc StageDataSO.");
                }
            }
        }
    }

    private static bool Positive(float value)
    {
        return value > 0f && !float.IsInfinity(value);
    }

    #endregion
}
