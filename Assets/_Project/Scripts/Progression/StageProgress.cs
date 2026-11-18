using System;
using System.Collections.Generic;

/// <summary>Mốc thắng lần đầu của từng màn (key <c>stage_progress</c>) và giao dịch thưởng thắng đi kèm.</summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo; thay cho StageProgressService cũ (trước đây tạm nằm trong Scripts/Stage).
/// Nhận <see cref="Wallet"/> qua constructor: thưởng và cờ "đã thắng" phải ghi trong cùng một lần lưu.
/// </remarks>
public sealed class StageProgress
{
    #region Fields

    private readonly ISaveStore saves;
    private readonly Wallet wallet;
    private StageProgressSaveData data;

    #endregion

    #region Constructor / Load

    public StageProgress(ISaveStore saves, Wallet wallet)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
    }

    /// <summary>Đọc cờ một lần sau khi Save đã mở file. Save cũ chưa có section này nghĩa là chưa thắng màn nào.</summary>
    public void Load()
    {
        if (!saves.IsLoaded || data != null) return;
        data = saves.Load(GameConstants.SaveKeys.StageProgress, new StageProgressSaveData(), StageProgressSaveData.Validate);
    }

    #endregion

    #region Query / Grant

    /// <summary>Màn đã nhận thưởng thắng lần đầu chưa.</summary>
    public bool HasFirstCleared(string stageId)
    {
        EnsureAvailable();
        ValidateStageId(stageId);
        return data.firstClearStages.TryGetValue(stageId, out bool cleared) && cleared;
    }

    /// <summary>Cộng thưởng cùng snapshot tiến trình; false nghĩa là snapshot đang chờ ghi lại (không cấp lại).</summary>
    public bool GrantVictoryRewards(string stageId, IReadOnlyDictionary<CurrencyType, long> rewards)
    {
        EnsureAvailable();
        ValidateStageId(stageId);
        StageProgressSaveData snapshot = data.Clone();
        snapshot.firstClearStages[stageId] = true;
        bool saved = wallet.AddBatch(rewards, snapshot);
        // Ghi nhận cả khi IO lỗi: RAM đã cộng tiền, nên cờ cũng phải đổi để không cấp thưởng lần đầu hai lần.
        data = snapshot;
        return saved;
    }

    #endregion

    #region Private Helpers

    private static void ValidateStageId(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            throw new ArgumentException("Stage ID must not be empty.", nameof(stageId));
    }

    private void EnsureAvailable()
    {
        if (data == null || !saves.IsLoaded)
            throw new InvalidOperationException("Stage progress has not loaded.");
    }

    #endregion
}
