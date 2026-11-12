using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum BossSpawnTiming
{
    [InspectorName("Đầu wave")]
    Start,

    [InspectorName("Giữa wave (đã spawn 50% quái)")]
    Middle,

    [InspectorName("Cuối wave (đã spawn đủ quái)")]
    End
}

// Cấu hình cố định của màn; tiến trình mỗi lượt nằm trong StageManager.
[CreateAssetMenu(fileName = "Stage", menuName = "Shooter Game/Data/Stage")]
public sealed class StageDataSO : ScriptableObject
{
    #region Serializable Entries

    // Boss thuộc Stage; wave là số thứ tự bắt đầu từ 1, count không cộng vào ngân sách enemy thường.
    [Serializable]
    public sealed class BossEntry
    {
        [FormerlySerializedAs("afterWave")]
        [Min(1)] public int wave = 1;
        public CharacterDataSO boss;
        [Min(1)] public int count = 1;

        [Tooltip("Mốc dựa trên số enemy thường đã spawn, không phải số đã giết. Boss và cơ chế riêng không lấy ngân sách của Wave.")]
        public BossSpawnTiming timing;

        public bool CanSpawnAt(int spawnedEnemies, int totalEnemies)
        {
            switch (timing)
            {
                case BossSpawnTiming.Start:
                    return true;

                case BossSpawnTiming.Middle:
                    return spawnedEnemies >= totalEnemies / 2 + totalEnemies % 2;

                case BossSpawnTiming.End:
                    return spawnedEnemies >= totalEnemies;

                default:
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class CurrencyReward
    {
        public CurrencyType type;
        public long amount;
    }

    #endregion

    #region Serialized Stage Configuration

    // ID ổn định dùng lưu cờ first clear; không thay ID chỉ vì đổi tên hiển thị hoặc tên asset.
    [SerializeField] private string stageId;
    [SerializeField] private string displayName;
    [SerializeField] private string mapKey = GameConstants.MapKeys.Desert;
    [SerializeField] private WaveConfigSO waves;
    [Tooltip("Boss xuất hiện theo số wave và mốc đầu/giữa/cuối. Cơ chế hộ tống/đấu trường sẽ thuộc boss riêng, không dùng Formation/Path của Wave.")]
    [SerializeField] private BossEntry[] bosses = Array.Empty<BossEntry>();
    // Giữ dữ liệu thưởng của asset cũ khi đổi tên từ Clear Rewards sang First Clear Stage Rewards.
    [FormerlySerializedAs("clearRewards")]
    [SerializeField] private CurrencyReward[] firstClearStageRewards = Array.Empty<CurrencyReward>();
    [SerializeField] private CurrencyReward[] victoryRewards = Array.Empty<CurrencyReward>();
    [SerializeField] private CurrencyReward[] defeatRewards = Array.Empty<CurrencyReward>();

    #endregion

    #region Read Only Configuration Access

    public string StageId => stageId;
    public string DisplayName => displayName;
    public string MapKey => mapKey;
    public WaveConfigSO Waves => waves;
    public IReadOnlyList<BossEntry> Bosses => bosses;
    public IReadOnlyList<CurrencyReward> FirstClearStageRewards => firstClearStageRewards;
    public IReadOnlyList<CurrencyReward> VictoryRewards => victoryRewards;
    public IReadOnlyList<CurrencyReward> DefeatRewards => defeatRewards;

    #endregion

    #region Configuration Validation

    // Chạy trước lượt chơi: Wave kiểm tra enemy/path; Stage kiểm tra boss và thời điểm spawn độc lập với đội hình enemy.
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(mapKey) || waves == null)
        {
            throw new InvalidOperationException("Stage needs a stable ID, map key and wave config.");
        }
        waves.Validate();
        if (bosses == null)
        {
            throw new InvalidOperationException("Missing stage boss list.");
        }
        foreach (BossEntry entry in bosses)
        {
            if (entry == null || entry.wave < 1 || entry.wave > waves.Count || entry.count < 1 ||
                !Enum.IsDefined(typeof(BossSpawnTiming), entry.timing) || entry.boss == null || entry.boss.Team != CharacterTeam.Enemy || !entry.boss.IsBoss || entry.boss.VisualPrefab == null)
            {
                throw new InvalidOperationException("Each boss entry needs a valid wave number, count and Enemy character marked Is Boss with a visual prefab.");
            }
        }
        ValidateRewards(firstClearStageRewards);
        ValidateRewards(victoryRewards);
        ValidateRewards(defeatRewards);
    }

    private static void ValidateRewards(CurrencyReward[] rewards)
    {
        if (rewards == null)
        {
            throw new InvalidOperationException("Missing stage reward list.");
        }
        foreach (CurrencyReward reward in rewards)
        {
            if (reward == null || !Enum.IsDefined(typeof(CurrencyType), reward.type) || reward.amount < 0)
            {
                throw new InvalidOperationException("Stage rewards need defined currency types and non-negative amounts.");
            }
        }
    }

    #endregion
}
