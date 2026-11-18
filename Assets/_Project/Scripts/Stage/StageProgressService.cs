using System;
using System.Collections.Generic;
using Zenject;

/// <summary>Lưu tiến trình thắng lần đầu và kết hợp giao dịch thưởng.</summary>
/// <remarks>
/// ⏳ Thuộc miền Progression (dữ liệu lưu giữa các lượt chơi), không thuộc Stage; tạm để cạnh Stage vì Stage là nơi dùng duy nhất.
/// Giai đoạn 5 chuyển vào ProgressionManager cùng ví tiền, level vĩnh viễn và trang bị.
/// </remarks>
public sealed class StageProgressService : IInitializable, IStageProgress, IStageProgressReader, IStageVictoryRewards
{
    #region Dependency và dữ liệu lưu
    private readonly ISaveReader saves;
    private readonly IStageRewardTransaction currencies;
    private StageProgressSaveData data;

    #endregion

    #region Khởi tạo và nạp save

    /// <summary>Nhận dịch vụ save và tiền tệ; từ chối dependency null.</summary>
    public StageProgressService(ISaveService saveService, ICurrencyService currencyService)
    {
        this.saves = saveService.GetSaveReader() ?? throw new ArgumentNullException(nameof(saveService));
        this.currencies = currencyService.GetStageRewardTransaction() ?? throw new ArgumentNullException(nameof(currencyService));
    }

    /// <summary>Nạp cờ thắng lần đầu một lần sau khi SaveManager đã sẵn sàng.</summary>
    public void Initialize()
    {
        if (!saves.IsLoaded || data != null) return;
        // Existing saves have no history: absent section means no first clears yet.
        data = saves.Load(GameConstants.SaveKeys.StageProgress, new StageProgressSaveData(), ValidateData);
    }

    #endregion

    #region Kiểm tra dữ liệu lưu

    /// <summary>Kiểm tra dữ liệu cờ tiến trình và mọi Stage ID đã lưu.</summary>
    internal static void ValidateData(StageProgressSaveData value)
    {
        if (value == null || value.firstClearStages == null)
            throw new InvalidOperationException("Missing stage first-clear flags.");
        foreach (string stageId in value.firstClearStages.Keys) ValidateStageId(stageId);
    }

    #endregion

    #region Tra cứu và cấp thưởng

    /// <summary>Đọc trạng thái thắng lần đầu sau khi kiểm tra dịch vụ và Stage ID.</summary>
    public bool HasFirstClearedStage(string stageId)
    {
        EnsureAvailable();
        ValidateStageId(stageId);
        return data.firstClearStages.TryGetValue(stageId, out bool cleared) && cleared;
    }

    /// <summary>Cộng thưởng cùng snapshot tiến trình; false nghĩa là snapshot đang chờ ghi lại.</summary>
    public bool GrantVictoryRewards(string stageId, IReadOnlyDictionary<CurrencyType, long> rewards)
    {
        EnsureAvailable();
        ValidateStageId(stageId);
        StageProgressSaveData snapshot = data.Clone();
        snapshot.firstClearStages[stageId] = true;
        bool saved = currencies.AddBatch(rewards, snapshot);
        data = snapshot;
        return saved;
    }

    #endregion

    #region Điều kiện truy cập

    /// <summary>Từ chối Stage ID trống để tránh lưu nhầm cờ tiến trình.</summary>
    private static void ValidateStageId(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            throw new ArgumentException("Stage ID must not be empty.", nameof(stageId));
    }

    /// <summary>Báo lỗi nếu save hoặc tiến trình chưa được nạp.</summary>
    private void EnsureAvailable()
    {
        if (data == null || !saves.IsLoaded)
            throw new InvalidOperationException("Stage progress has not loaded.");
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IStageProgressReader GetStageProgressReader() => this;

    public IStageVictoryRewards GetStageVictoryRewards() => this;
    #endregion
}
