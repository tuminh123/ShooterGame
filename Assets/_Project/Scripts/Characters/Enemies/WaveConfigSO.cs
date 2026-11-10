using System;
using UnityEngine;

public enum WaveAdvanceCondition { AfterDuration, AfterEnemiesCleared }

[CreateAssetMenu(fileName = "Waves", menuName = "Shooter Game/Data/Waves")]
public sealed class WaveConfigSO : ScriptableObject
{
    [Serializable]
    public sealed class Wave
    {
        [Min(0.01f)] public float duration = 60f; // AfterDuration: chuyển theo thời gian, không chờ giết hết.
        [Min(1)] public int spawnCount = 20; // AfterEnemiesCleared: tổng số quái phải spawn thành công trước khi chờ dọn hết.
        [Min(0.01f)] public float spawnInterval = 0.5f;
        [Min(1)] public int maxAliveEnemies = 20; // Bao gồm quái sống còn lại từ wave trước.
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
                !Positive(wave.spawnInterval) || wave.maxAliveEnemies < 1 || wave.enemies == null || wave.enemies.Length == 0)
                throw new InvalidOperationException("Each wave needs a valid condition, spawn timing, alive limit and enemy data.");
            switch (advanceCondition)
            {
                case WaveAdvanceCondition.AfterDuration:
                    if (!Positive(wave.duration)) throw new InvalidOperationException("Timed wave duration must be positive and finite.");
                    break;
                case WaveAdvanceCondition.AfterEnemiesCleared:
                    if (wave.spawnCount < 1) throw new InvalidOperationException("Clear wave needs at least one enemy to spawn.");
                    break;
            }
            foreach (CharacterDataSO enemy in wave.enemies)
                if (enemy == null || enemy.Team != CharacterTeam.Enemy || enemy.VisualPrefab == null)
                    throw new InvalidOperationException("Waves can only spawn Enemy data with a visual prefab.");
        }
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
}
