using System;
using System.Collections.Generic;
using Zenject;

/// <summary>Lưu tiến trình thắng lần đầu và kết hợp giao dịch thưởng.</summary>
public sealed class StageProgressService : IInitializable
{
    #region Dependency và dữ liệu lưu

    private readonly SaveManager saves;
    private readonly CurrencyService currencies;
    private StageProgressSaveData data;

    #endregion

    #region Khởi tạo và nạp save

    /// <summary>Nhận dịch vụ save và tiền tệ; từ chối dependency null.</summary>
    public StageProgressService(SaveManager saves, CurrencyService currencies)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
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
    internal bool GrantVictoryRewards(string stageId, IReadOnlyDictionary<CurrencyType, long> rewards)
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
}
