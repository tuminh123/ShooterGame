using System;
using System.Collections.Generic;
using Zenject;

// Owns persistent first-clear flags; reward balances and flags share one file commit.
public sealed class StageProgressService : IInitializable
{
    private readonly SaveManager saves;
    private readonly CurrencyService currencies;
    private StageProgressSaveData data;

    public StageProgressService(SaveManager saves, CurrencyService currencies)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
    }

    public void Initialize()
    {
        if (!saves.IsLoaded || data != null) return;
        // Existing saves have no history: absent section means no first clears yet.
        data = saves.Load(GameConstants.SaveKeys.StageProgress, new StageProgressSaveData(), ValidateData);
    }

    internal static void ValidateData(StageProgressSaveData value)
    {
        if (value == null || value.firstClearStages == null)
            throw new InvalidOperationException("Missing stage first-clear flags.");
        foreach (string stageId in value.firstClearStages.Keys) ValidateStageId(stageId);
    }

    public bool HasFirstClearedStage(string stageId)
    {
        EnsureAvailable();
        ValidateStageId(stageId);
        return data.firstClearStages.TryGetValue(stageId, out bool cleared) && cleared;
    }

    // false means the combined snapshot is pending IO retry, not that rewards should be granted again.
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
}
