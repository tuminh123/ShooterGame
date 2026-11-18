using System.Collections.Generic;

/// <summary>
/// Thưởng kết quả của một lượt chơi: tính (thắng/thua, thêm thưởng thắng lần đầu nếu màn chưa từng thắng),
/// cấp đúng một lần, và giữ lại số đã cấp để popup kết quả hiển thị.
/// </summary>
/// <remarks>
/// Do <see cref="StageManager"/> tạo; tách riêng để StageManager chỉ còn lo điều phối spawn và điều kiện thắng.
/// Ghi vào miền Progression qua <see cref="ICurrencyWallet"/> (thua) và <see cref="IStageVictoryRewards"/> (thắng,
/// cấp thưởng và mốc vượt màn trong một lần lưu).
/// </remarks>
internal sealed class StageRewards
{
    #region State

    private readonly ICurrencyWallet currencies;
    private readonly IStageProgressReader stageProgress;
    private readonly IStageVictoryRewards victoryRewards;
    private readonly Dictionary<CurrencyType, long> granted = new Dictionary<CurrencyType, long>(); // Số đã cấp ở lượt này.

    /// <summary>Lượt hiện tại đã nhận thưởng kết quả chưa (chặn cấp hai lần).</summary>
    public bool Granted { get; private set; }

    #endregion

    #region Lifecycle

    internal StageRewards(ICurrencyWallet currencies, IStageProgressReader stageProgress, IStageVictoryRewards victoryRewards)
    {
        this.currencies = currencies;
        this.stageProgress = stageProgress;
        this.victoryRewards = victoryRewards;
    }

    /// <summary>Bắt đầu lượt mới hoặc đổi màn: quên thưởng của lượt trước.</summary>
    internal void Reset()
    {
        granted.Clear();
        Granted = false;
    }

    /// <summary>Tính thử cả thưởng thắng và thua để phát hiện cấu hình lỗi (ví dụ tràn số) trước khi chơi.</summary>
    internal void Validate(StageDataSO stage)
    {
        Build(stage, true);
        Build(stage, false);
    }

    #endregion

    #region Grant

    /// <summary>Cấp thưởng thắng/thua đúng một lần cho lượt hiện tại; gọi lại thì không làm gì.</summary>
    internal void Grant(StageDataSO stage, bool won)
    {
        if (Granted)
            return;
        Dictionary<CurrencyType, long> rewards = Build(stage, won);
        // Lỗi IO được hệ thống save giữ snapshot để thử lại; không bao giờ cộng thưởng lần thứ hai.
        if (won)
            victoryRewards.GrantVictoryRewards(stage.StageId, rewards);
        else
            currencies.AddBatch(rewards);
        Granted = true;
        foreach (KeyValuePair<CurrencyType, long> reward in rewards)
            granted.Add(reward.Key, reward.Value);
    }

    /// <summary>Số đã cấp của một loại tiền ở lượt này; 0 nếu không có.</summary>
    internal long Get(CurrencyType type) => granted.TryGetValue(type, out long amount) ? amount : 0L;

    // Gộp thưởng kết quả; chỉ thêm thưởng lần đầu nếu màn chưa từng được thắng.
    private Dictionary<CurrencyType, long> Build(StageDataSO stage, bool won)
    {
        var rewards = new Dictionary<CurrencyType, long>();
        if (won && !stageProgress.HasFirstClearedStage(stage.StageId))
            Add(rewards, stage.FirstClearStageRewards);
        Add(rewards, won ? stage.VictoryRewards : stage.DefeatRewards);
        return rewards;
    }

    // Cộng theo loại tiền; checked để phát hiện tràn số thay vì cấp sai.
    private static void Add(Dictionary<CurrencyType, long> totals, IReadOnlyList<StageDataSO.CurrencyReward> rewards)
    {
        foreach (StageDataSO.CurrencyReward reward in rewards)
        {
            totals.TryGetValue(reward.type, out long previous);
            totals[reward.type] = checked(previous + reward.amount);
        }
    }

    #endregion
}
