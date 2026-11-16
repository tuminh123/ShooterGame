using System;

// Factory cung cấp dịch vụ và tạo instance hành vi từ mẫu; không quản lý cấp hoặc vòng tick.
public sealed class SkillFactory :
    ISkillFactory,
    ISkillServices,
    ISkillRuntimeFactory,
    ISkillBehaviourFactory,
    ISkillShootingServices,
    ISkillEffectServices
{
    #region Scene Services

    public IProjectileSpawner Spawner { get; }
    public IShootBehaviorFactory Shooters { get; }
    public IProjectileMoveSelection Moves { get; }
    public IGameObjectPool Pools { get; }
    public IEffectApplication Effects { get; }

    // Zenject cấp các dịch vụ dùng chung; runtime con chỉ sở hữu state của riêng nó.
    public SkillFactory(
        ICombatSpawnSystem combatSpawnSystem,
        IShootBehaviorFactory shooters,
        IProjectileMoveFactory projectileMoveFactory,
        IPoolSystem poolSystem,
        IEffectSystem effectSystem)
    {
        Spawner = combatSpawnSystem.GetProjectileSpawner();
        Shooters = shooters;
        Moves = projectileMoveFactory.GetProjectileMoveSelection();
        Pools = poolSystem.GetGameObjectPool();
        Effects = effectSystem.GetEffectApplication();
        EffectHandles = effectSystem.GetEffectHandleControl();
    }

    public IEffectHandleControl EffectHandles { get; }

    #endregion

    #region Runtime Creation

    // Điểm tạo một skill hoàn chỉnh; runtime sẽ mở các hành vi có data ở cấp bắt đầu.
    public SkillRuntime Create(SkillDataSO definition, CharacterSkills owner)
    {
        definition.Validate();
        return new SkillRuntime(definition, owner, this);
    }

    // Enum tìm đúng mẫu; abstract Skill đã cung cấp việc clone và khởi tạo context.
    public ISkill CreateBehaviour(SkillDataSO definition, SkillBehaviourType type, CharacterSkills owner)
    {
        if (!(definition.GetBehaviour(type) is Skill template))
            throw new InvalidOperationException("Behaviour templates must inherit Skill.");
        return template.CreateRuntime(owner, this);
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public ISkillRuntimeFactory GetRuntimeFactory() => this;

    public ISkillBehaviourFactory GetBehaviourFactory() => this;

    public ISkillShootingServices GetShooting() => this;

    public ISkillEffectServices GetEffects() => this;

    #endregion
}
