using System;

// Factory cung cấp dịch vụ và tạo instance hành vi từ mẫu; không quản lý cấp hoặc vòng tick.
public sealed class SkillFactory
{
    #region Scene Services

    internal CombatSpawner Spawner { get; }
    internal ShootBehaviorFactory Shooters { get; }
    internal ProjectileMoveFactory Moves { get; }
    internal PoolManager Pools { get; }
    internal EffectManager Effects { get; }

    // Zenject cấp các dịch vụ dùng chung; runtime con chỉ sở hữu state của riêng nó.
    public SkillFactory(CombatSpawner spawner, ShootBehaviorFactory shooters, ProjectileMoveFactory moves, PoolManager pools, EffectManager effects = null)
    { Spawner = spawner; Shooters = shooters; Moves = moves; Pools = pools; Effects = effects; }

    #endregion

    #region Runtime Creation

    // Điểm tạo một skill hoàn chỉnh; runtime sẽ mở các hành vi có data ở cấp bắt đầu.
    internal SkillRuntime Create(SkillDataSO definition, CharacterSkills owner)
    {
        definition.Validate();
        return new SkillRuntime(definition, owner, this);
    }

    // Enum tìm đúng mẫu; abstract Skill đã cung cấp việc clone và khởi tạo context.
    internal ISkill CreateBehaviour(SkillDataSO definition, SkillBehaviourType type, CharacterSkills owner)
    {
        if (!(definition.GetBehaviour(type) is Skill template))
            throw new InvalidOperationException("Behaviour templates must inherit Skill.");
        return template.CreateRuntime(owner, this);
    }

    #endregion
}
