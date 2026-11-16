using UnityEngine;

#region BossSummon

/// <summary>Dựng hoặc gỡ đấu trường và triển khai Boss.</summary>
public interface IBossSummon
{
    CharacterView Summon(CharacterDataSO data, int level, Vector2 arenaSize);
    void Clear();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của BossHandleSummon; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IBossSummonService
{
    IBossSummon GetBossSummon();
}

#endregion
