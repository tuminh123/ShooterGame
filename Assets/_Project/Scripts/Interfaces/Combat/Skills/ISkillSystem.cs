using System.Collections.Generic;

#region SkillOwnerLifecycle

/// <summary>Pause và thu hồi skill gameplay của đúng owner.</summary>
public interface ISkillOwnerLifecycle
{
    void SetOwnerPaused(CharacterView owner, bool paused);
    void RemoveOwner(CharacterView owner);
}

#endregion

#region SkillGrant

/// <summary>Cấp mới hoặc nâng skill gameplay cho một owner.</summary>
public interface ISkillGrant
{
    bool GrantOrUpgrade(SkillDataSO definition, CharacterView owner);
}

#endregion

#region OwnedSkillReader

/// <summary>Đọc các skill gameplay của owner, không thay đổi cấp.</summary>
public interface IOwnedSkillReader
{
    void GetOwnedSkills(CharacterView owner, List<SkillRuntime> result);
    bool TryGetRuntime(SkillDataSO definition, CharacterView owner, out SkillRuntime runtime);
}

#endregion

#region RunSkillCleanup

/// <summary>Thu hồi skill gameplay khi lượt chơi kết thúc hoặc reset.</summary>
public interface IRunSkillCleanup
{
    void ClearRunSkills();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của SkillManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ISkillSystem
{
    ISkillOwnerLifecycle GetOwnerLifecycle();
    ISkillGrant GetGrant();
    IOwnedSkillReader GetReader();
    IRunSkillCleanup GetRunCleanup();
}

#endregion
