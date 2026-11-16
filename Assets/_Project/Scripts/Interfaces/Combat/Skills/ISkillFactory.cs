#region SkillRuntimeFactory

/// <summary>Tạo một skill hoàn chỉnh cho context của owner.</summary>
public interface ISkillRuntimeFactory
{
    SkillRuntime Create(SkillDataSO definition, CharacterSkills owner);
}

#endregion

#region SkillBehaviourFactory

/// <summary>Tạo hành vi cụ thể khi runtime mở hoặc nâng cấp skill.</summary>
public interface ISkillBehaviourFactory
{
    ISkill CreateBehaviour(SkillDataSO definition, SkillBehaviourType type, CharacterSkills owner);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của SkillFactory; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ISkillFactory
{
    ISkillRuntimeFactory GetRuntimeFactory();
    ISkillBehaviourFactory GetBehaviourFactory();
}

#endregion
