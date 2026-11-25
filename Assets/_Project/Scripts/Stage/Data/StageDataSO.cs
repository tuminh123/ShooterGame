using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cấu hình màn và danh sách DataSO spawn trực tiếp.</summary>
[CreateAssetMenu(fileName = "Stage", menuName = "Shooter Game/Data/Stage")]
public sealed class StageDataSO : ScriptableObject
{
    #region Stage & Reward Configuration

    [Tooltip("ID ổn định dùng lưu tiến trình vượt màn và thưởng lần đầu; không đổi sau khi đã phát hành save.")]
    [SerializeField] private string stageId;
    [Tooltip("Tên màn hiển thị cho người chơi; độc lập Stage ID.")]
    [SerializeField] private string displayName;
    [Tooltip("Khóa map trong MapDatabaseSO, phân biệt hoa/thường; phải có sprite nền tương ứng.")]
    [SerializeField] private string mapKey = GameConstants.MapKeys.Desert;
    [Tooltip("Danh sách trực tiếp các DataSO triển khai IStageSpawnData: Waves, Pattern, Boss hoặc loại mới.")]
    [SerializeField] private List<ScriptableObject> spawnData = new List<ScriptableObject>();
    [Tooltip("Thưởng bổ sung khi thắng màn lần đầu. Được cộng với Victory Rewards; các lần thắng sau không nhận lại.")]
    [SerializeReference] private List<IReward> firstClearStageRewards = new List<IReward>();
    [Tooltip("Thưởng mỗi lần thắng màn; cộng thêm thưởng lần đầu nếu màn chưa từng được vượt.")]
    [SerializeReference] private List<IReward> victoryRewards = new List<IReward>();
    [Tooltip("Thưởng khi thua màn; không nhận thưởng lần đầu hoặc thưởng thắng.")]
    [SerializeReference] private List<IReward> defeatRewards = new List<IReward>();

    #endregion

    #region Read-only Data

    public string StageId => stageId;
    public string DisplayName => displayName;
    public string MapKey => mapKey;
    /// <summary>Duyệt các asset dưới interface chung; không tạo bản sao DataSO.</summary>
    public IEnumerable<IStageSpawnData> SpawnData
    {
        get { foreach (var asset in spawnData) yield return asset as IStageSpawnData; }
    }
    public IReadOnlyList<IReward> FirstClearStageRewards => firstClearStageRewards;
    public IReadOnlyList<IReward> VictoryRewards => victoryRewards;
    public IReadOnlyList<IReward> DefeatRewards => defeatRewards;

    #endregion

    #region Validation

    /// <summary>Kiểm tra màn, từng asset spawn và các khoản thưởng.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(mapKey) || spawnData == null || spawnData.Count == 0)
            throw new InvalidOperationException("Stage cần ID, map key và danh sách Spawn Data.");
        foreach (var asset in spawnData)
        {
            if (asset == null || !(asset is IStageSpawnData data))
                throw new InvalidOperationException("Mỗi phần tử Spawn Data phải là DataSO triển khai IStageSpawnData.");
            data.Validate();
        }
        int bossEntries = 0;
        BossAppearanceCondition? bossCondition = null;
        foreach (var asset in spawnData)
            if (asset is BossSpawnDataSO special)
            {
                if (bossCondition.HasValue && bossCondition.Value != special.BossAppearanceCondition)
                    throw new InvalidOperationException("Các entry đặc biệt trong Stage phải dùng cùng điều kiện xuất hiện Boss.");
                bossCondition = special.BossAppearanceCondition;
                foreach (BossSpawnDataSO.Entry entry in special.Entries)
                    if (entry.Type == SpecialEnemyType.Boss) bossEntries++;
            }
        if (bossEntries > 1) throw new InvalidOperationException("Một Stage chỉ được cấu hình một Boss.");
        bool hasWaveData = false;
        foreach (var asset in spawnData) if (asset is WaveConfigSO) { hasWaveData = true; break; }
        if (bossEntries > 0 && !hasWaveData)
            throw new InvalidOperationException("Boss chờ event Wave nên Stage phải có WaveConfigSO.");
        RewardBatch.From(firstClearStageRewards);
        RewardBatch.From(victoryRewards);
        RewardBatch.From(defeatRewards);
    }
    #endregion
}
