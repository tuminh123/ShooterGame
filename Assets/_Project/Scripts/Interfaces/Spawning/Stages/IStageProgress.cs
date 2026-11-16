using System.Collections.Generic;

#region StageProgressReader

/// <summary>Kiểm tra màn đã nhận thưởng thắng lần đầu hay chưa.</summary>
public interface IStageProgressReader
{
    bool HasFirstClearedStage(string stageId);
}

#endregion

#region StageVictoryRewards

/// <summary>Cấp thưởng thắng cùng mốc vượt màn trong một lần lưu.</summary>
public interface IStageVictoryRewards
{
    bool GrantVictoryRewards(string stageId, IReadOnlyDictionary<CurrencyType, long> rewards);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của StageProgressService; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IStageProgress
{
    IStageProgressReader GetStageProgressReader();
    IStageVictoryRewards GetStageVictoryRewards();
}

#endregion
