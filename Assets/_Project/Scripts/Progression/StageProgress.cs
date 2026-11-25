using System;
using System.Collections.Generic;

/// <summary>Mốc thắng lần đầu của từng màn (key <c>stage_progress</c>) và giao dịch thưởng thắng đi kèm.</summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo; thay cho StageProgressService cũ (trước đây tạm nằm trong Scripts/Stage).
/// Nhận <see cref="RewardGranter"/> qua constructor: thưởng và cờ "đã thắng" phải ghi trong cùng một lần lưu.
/// </remarks>
public sealed class StageProgress
{
    #region Fields

    private readonly ISaveStore saves;
    private readonly RewardGranter granter;
    private StageProgressSaveData data;

    #endregion

    #region Constructor / Load

    internal StageProgress(ISaveStore saves, RewardGranter granter)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.granter = granter ?? throw new ArgumentNullException(nameof(granter));
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

    /// <summary>Trao thưởng cùng snapshot tiến trình; <paramref name="saved"/> = false nghĩa là đang chờ ghi lại (không trao lại).</summary>
    internal RewardResult GrantRewards(string stageId, IReadOnlyList<IReward> rewards, bool markCleared, out bool saved)
    {
        EnsureAvailable();
        ValidateStageId(stageId);
        StageProgressSaveData snapshot = data.Clone();
        if (markCleared) snapshot.firstClearStages[stageId] = true;
        RewardResult result = granter.Grant(rewards, SaveEntry.Of(GameConstants.SaveKeys.StageProgress, snapshot), out saved);
        // Ghi nhận cả khi IO lỗi: RAM đã trao, nên cờ cũng phải đổi để không cấp thưởng lần đầu hai lần.
        data = snapshot;
        return result;
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
