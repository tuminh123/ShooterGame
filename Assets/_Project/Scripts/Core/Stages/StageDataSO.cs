using System;
using System.Collections.Generic;
using UnityEngine;

// Cấu hình cố định của màn; tiến trình mỗi lượt nằm trong StageManager.
[CreateAssetMenu(fileName = "Stage", menuName = "Shooter Game/Data/Stage")]
public sealed class StageDataSO : ScriptableObject
{
    [Serializable]
    public sealed class BossEntry
    {
        [Min(1)] public int afterWave = 1;
        public CharacterDataSO boss;
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public sealed class CurrencyReward
    {
        public CurrencyType type;
        public long amount;
    }

    [SerializeField] private string stageId;
    [SerializeField] private string displayName;
    [SerializeField] private string mapKey = GameConstants.MapKeys.Desert;
    [SerializeField] private WaveConfigSO waves;
    [Tooltip("Bosses spawn after all normal enemies of After Wave have spawned. Bosses do not count toward Enemy Count.")]
    [SerializeField] private BossEntry[] bosses = Array.Empty<BossEntry>();
    [SerializeField] private CurrencyReward[] clearRewards = Array.Empty<CurrencyReward>();
    [SerializeField] private CurrencyReward[] victoryRewards = Array.Empty<CurrencyReward>();
    [SerializeField] private CurrencyReward[] defeatRewards = Array.Empty<CurrencyReward>();

    public string StageId => stageId;
    public string DisplayName => displayName;
    public string MapKey => mapKey;
    public WaveConfigSO Waves => waves;
    public IReadOnlyList<BossEntry> Bosses => bosses;
    public IReadOnlyList<CurrencyReward> ClearRewards => clearRewards;
    public IReadOnlyList<CurrencyReward> VictoryRewards => victoryRewards;
    public IReadOnlyList<CurrencyReward> DefeatRewards => defeatRewards;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(mapKey) || waves == null)
            throw new InvalidOperationException("Stage needs a stable ID, map key and wave config.");
        waves.Validate();
        if (bosses == null) throw new InvalidOperationException("Missing stage boss list.");
        foreach (BossEntry entry in bosses)
            if (entry == null || entry.afterWave < 1 || entry.afterWave > waves.Count || entry.count < 1 ||
                entry.boss == null || entry.boss.Team != CharacterTeam.Enemy || !entry.boss.IsBoss || entry.boss.VisualPrefab == null)
                throw new InvalidOperationException("Each boss entry needs a valid wave number, count and Enemy character marked Is Boss with a visual prefab.");
        ValidateRewards(clearRewards);
        ValidateRewards(victoryRewards);
        ValidateRewards(defeatRewards);
    }

    private static void ValidateRewards(CurrencyReward[] rewards)
    {
        if (rewards == null) throw new InvalidOperationException("Missing stage reward list.");
        foreach (CurrencyReward reward in rewards)
            if (reward == null || !Enum.IsDefined(typeof(CurrencyType), reward.type) || reward.amount < 0)
                throw new InvalidOperationException("Stage rewards need defined currency types and non-negative amounts.");
    }
}
