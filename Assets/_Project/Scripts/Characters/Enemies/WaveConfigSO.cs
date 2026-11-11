using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum WaveAdvanceCondition { AfterDuration, AfterEnemiesCleared }

[CreateAssetMenu(fileName = "Waves", menuName = "Shooter Game/Data/Waves")]
public sealed class WaveConfigSO : ScriptableObject
{
    [Serializable]
    public sealed class Wave
    {
        [FormerlySerializedAs("duration")]
        [Min(0f)] public float delayAfterSpawn = 20f; // Sau khi spawn đủ quái và boss, chờ bấy nhiêu giây trước wave tiếp.
        [Min(1)] public int spawnCount = 20; // Tổng số quái phải spawn thành công trong wave ở cả hai chế độ.
        [Min(0.01f)] public float spawnInterval = 0.5f;
        public CharacterDataSO[] enemies = Array.Empty<CharacterDataSO>();
    }

    [SerializeField] private WaveAdvanceCondition advanceCondition;
    [SerializeField] private Wave[] waves = Array.Empty<Wave>();
    public WaveAdvanceCondition AdvanceCondition => advanceCondition;
    public int Count => waves.Length;
    public Wave GetWave(int index)
    {
        if (index < 0 || index >= waves.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return waves[index];
    }

    public void Validate()
    {
        if (waves == null || waves.Length == 0) throw new InvalidOperationException("Wave config needs at least one wave.");
        if (!Enum.IsDefined(typeof(WaveAdvanceCondition), advanceCondition))
            throw new InvalidOperationException("Unknown wave advance condition.");
        foreach (Wave wave in waves)
        {
            if (wave == null ||
                !Positive(wave.spawnInterval) || wave.spawnCount < 1 || wave.enemies == null || wave.enemies.Length == 0)
                throw new InvalidOperationException("Each wave needs a positive enemy count, valid spawn timing and enemy data.");
            switch (advanceCondition)
            {
                case WaveAdvanceCondition.AfterDuration:
                    if (float.IsNaN(wave.delayAfterSpawn) || float.IsInfinity(wave.delayAfterSpawn) || wave.delayAfterSpawn < 0f)
                        throw new InvalidOperationException("Delay after spawning must be non-negative and finite.");
                    break;
                case WaveAdvanceCondition.AfterEnemiesCleared:
                    break;
            }
            foreach (CharacterDataSO enemy in wave.enemies)
                if (enemy == null || enemy.Team != CharacterTeam.Enemy || enemy.IsBoss || enemy.VisualPrefab == null)
                    throw new InvalidOperationException("Waves only contain normal Enemy data with visual prefabs. Configure bosses on StageDataSO.");
        }
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
}
