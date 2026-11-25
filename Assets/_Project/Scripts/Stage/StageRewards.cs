using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thưởng kết quả của một lượt chơi: tính (thắng/thua, thêm thưởng thắng lần đầu nếu màn chưa từng thắng),
/// trao đúng một lần, và giữ lại kết quả thực tế để popup hiển thị.
/// </summary>
/// <remarks>
/// Do <see cref="StageManager"/> tạo; tách riêng để StageManager chỉ còn lo điều phối spawn và điều kiện thắng.
/// Trao qua <see cref="IStageProgress"/> (thưởng và mốc vượt màn trong một lần lưu).
/// </remarks>
public sealed class StageRewards
{
    #region State

    private readonly IRewardGrant grants;
    private readonly IStageProgress stageProgress;
    private List<IReward> grantedRewards; // Danh sách đã trao khi thắng, để x2 trao lại đúng danh sách đó (kể cả thưởng lần đầu).
    private string stageId;
    private bool doubled;

    /// <summary>Kết quả thực tế của lượt; null nếu chưa trao.</summary>
    public RewardResult Result { get; private set; }

    /// <summary>Lượt hiện tại đã nhận thưởng kết quả chưa (chặn cấp hai lần).</summary>
    public bool Granted => Result != null;

    #endregion

    #region Lifecycle

    public StageRewards(IRewardGrant grants, IStageProgress stageProgress)
    {
        this.grants = grants;
        this.stageProgress = stageProgress;
    }

    /// <summary>Bắt đầu lượt mới hoặc đổi màn: quên thưởng của lượt trước.</summary>
    public void Reset()
    {
        Result = null;
        grantedRewards = null;
        stageId = null;
        doubled = false;
    }

    /// <summary>Kiểm tra cả ba danh sách trao được, để phát hiện cấu hình lỗi trước khi chơi.</summary>
    public void Validate(StageDataSO stage)
    {
        grants.ValidateRewards(stage.FirstClearStageRewards);
        grants.ValidateRewards(stage.VictoryRewards);
        grants.ValidateRewards(stage.DefeatRewards);
    }

    #endregion

    #region Grant

    /// <summary>Trao thưởng thắng/thua đúng một lần cho lượt hiện tại; gọi lại thì không làm gì.</summary>
    public void Grant(StageDataSO stage, bool won)
    {
        if (Granted)
            return;
        List<IReward> rewards = Build(stage, won);
        Result = stageProgress.GrantRewards(stage.StageId, rewards, won, out bool saved);
        WarnIfNotSaved(saved);
        if (!won) return;
        grantedRewards = rewards;
        stageId = stage.StageId;
    }

    /// <summary>Trao lại thưởng thắng một lần nữa kèm vàng nhặt; đồ trang bị roll dòng phụ mới, nhân vật trùng thành mảnh.</summary>
    public bool TryDouble(int pickedGold, out RewardResult bonus)
    {
        bonus = null;
        if (grantedRewards == null || doubled) return false;
        var rewards = new List<IReward>(grantedRewards);
        if (pickedGold > 0) rewards.Add(new CurrencyReward(CurrencyType.Gold, pickedGold));
        bonus = stageProgress.GrantRewards(stageId, rewards, false, out bool saved);
        doubled = true;
        WarnIfNotSaved(saved);
        return true;
    }

    // Lỗi IO: hệ thống save giữ snapshot để ghi lại; không bao giờ trao lần thứ hai.
    private static void WarnIfNotSaved(bool saved)
    {
        if (!saved) Debug.LogWarning("Stage rewards were granted but not saved yet; they will be retried.");
    }

    // Chỉ thêm thưởng lần đầu nếu màn chưa từng được thắng.
    private List<IReward> Build(StageDataSO stage, bool won)
    {
        var rewards = new List<IReward>();
        if (won && !stageProgress.HasFirstCleared(stage.StageId))
            rewards.AddRange(stage.FirstClearStageRewards);
        rewards.AddRange(won ? stage.VictoryRewards : stage.DefeatRewards);
        return rewards;
    }

    #endregion
}
