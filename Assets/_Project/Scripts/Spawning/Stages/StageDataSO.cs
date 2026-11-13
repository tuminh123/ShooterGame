using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Cấu hình màn và danh sách DataSO spawn trực tiếp.</summary>
[CreateAssetMenu(fileName = "Stage", menuName = "Shooter Game/Data/Stage")]
public sealed class StageDataSO : ScriptableObject
{
    #region Cấu hình màn và phần thưởng

    /// <summary>Một khoản thưởng tiền tệ, chưa chứa trạng thái đã cấp.</summary>
    [Serializable]
    public sealed class CurrencyReward
    {
        public CurrencyType type;
        public long amount;
    }

    [SerializeField] private string stageId;
    [SerializeField] private string displayName;
    [SerializeField] private string mapKey = GameConstants.MapKeys.Desert;
    [Tooltip("Danh sách trực tiếp các DataSO triển khai IStageSpawnData: Waves, Pattern, Boss hoặc loại mới.")]
    [SerializeField] private List<ScriptableObject> spawnData = new List<ScriptableObject>();
    [FormerlySerializedAs("clearRewards")]
    [SerializeField] private CurrencyReward[] firstClearStageRewards = Array.Empty<CurrencyReward>();
    [SerializeField] private CurrencyReward[] victoryRewards = Array.Empty<CurrencyReward>();
    [SerializeField] private CurrencyReward[] defeatRewards = Array.Empty<CurrencyReward>();

    #endregion

    #region Dữ liệu chỉ đọc

    public string StageId => stageId;
    public string DisplayName => displayName;
    public string MapKey => mapKey;
    /// <summary>Duyệt các asset dưới interface chung; không tạo bản sao DataSO.</summary>
    public IEnumerable<IStageSpawnData> SpawnData
    {
        get { foreach (var asset in spawnData) yield return asset as IStageSpawnData; }
    }
    public IReadOnlyList<CurrencyReward> FirstClearStageRewards => firstClearStageRewards;
    public IReadOnlyList<CurrencyReward> VictoryRewards => victoryRewards;
    public IReadOnlyList<CurrencyReward> DefeatRewards => defeatRewards;

    #endregion

    #region Kiểm tra cấu hình

    /// <summary>Kiểm tra màn, interface/factory của từng asset và các khoản thưởng.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(mapKey) || spawnData == null || spawnData.Count == 0)
            throw new InvalidOperationException("Stage cần ID, map key và danh sách Spawn Data.");
        foreach (var asset in spawnData)
        {
            if (asset == null || !(asset is IStageSpawnData data))
                throw new InvalidOperationException("Mỗi phần tử Spawn Data phải là DataSO triển khai IStageSpawnData.");
            Type type = data.SystemType;
            if (type == null || type.IsAbstract || type.IsInterface || !typeof(IStageSpawnSystem).IsAssignableFrom(type))
                throw new InvalidOperationException("Spawn DataSO phải khai báo một IStageSpawnSystem cụ thể.");
            if (!typeof(IStageSpawnStatus).IsAssignableFrom(type))
                throw new InvalidOperationException("Hệ thống spawn phải cung cấp IStageSpawnStatus để Stage xác định hoàn tất.");
            data.Validate();
        }
        int bossEntries = 0;
        BossAppearanceCondition? bossCondition = null;
        foreach (var asset in spawnData)
            if (asset is BossSpawnDataSO special)
            {
                if (bossCondition.HasValue && bossCondition.Value != special.bossAppearanceCondition)
                    throw new InvalidOperationException("Các entry đặc biệt trong Stage phải dùng cùng điều kiện xuất hiện Boss.");
                bossCondition = special.bossAppearanceCondition;
                foreach (BossSpawnDataSO.Entry entry in special.entries)
                    if (entry.type == SpecialEnemyType.Boss) bossEntries++;
            }
        if (bossEntries > 1) throw new InvalidOperationException("Một Stage chỉ được cấu hình một Boss.");
        bool hasWaveData = false;
        foreach (var asset in spawnData) if (asset is WaveConfigSO) { hasWaveData = true; break; }
        if (bossEntries > 0 && !hasWaveData)
            throw new InvalidOperationException("Boss chờ event Wave nên Stage phải có WaveConfigSO.");
        ValidateRewards(firstClearStageRewards);
        ValidateRewards(victoryRewards);
        ValidateRewards(defeatRewards);
    }

    /// <summary>Chặn danh sách thưởng rỗng tham chiếu, loại tiền sai hoặc số tiền âm.</summary>
    private static void ValidateRewards(CurrencyReward[] rewards)
    {
        if (rewards == null) throw new InvalidOperationException("Missing stage reward list.");
        foreach (var reward in rewards)
            if (reward == null || !Enum.IsDefined(typeof(CurrencyType), reward.type) || reward.amount < 0)
                throw new InvalidOperationException("Stage rewards need valid currency types and non-negative amounts.");
    }
    #endregion
}
