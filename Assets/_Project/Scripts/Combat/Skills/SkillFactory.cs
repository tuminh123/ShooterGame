using System;

/// <summary>
/// Tạo skill đang chạy (<see cref="SkillRuntime"/>) và các hành vi của nó từ template trong <see cref="SkillDataSO"/>.
/// </summary>
/// <remarks>
/// Không quản lý cấp hay vòng tick; việc đó thuộc <see cref="SkillRuntime"/> và <see cref="SkillSystem"/>.
/// Do <see cref="CombatManager"/> tạo; không bind vào Zenject.
/// </remarks>
public sealed class SkillFactory
{
    #region Dependencies

    private readonly SkillServices services; // Truyền vào mọi hành vi được tạo; factory không sở hữu vòng đời của chúng.

    public SkillFactory(SkillServices services)
        => this.services = services ?? throw new ArgumentNullException(nameof(services));

    #endregion

    #region Creation

    /// <summary>
    /// Tạo một skill hoàn chỉnh cho <paramref name="owner"/>. Runtime tự mở các hành vi có data ở cấp bắt đầu.
    /// </summary>
    public SkillRuntime Create(SkillDataSO definition, CharacterSkills owner)
    {
        definition.Validate();
        return new SkillRuntime(definition, owner, this);
    }

    /// <summary>Tạo hành vi <paramref name="type"/> mới cho <paramref name="owner"/>; mỗi lần gọi là một object riêng.</summary>
    public ISkill CreateBehaviour(SkillDataSO definition, SkillBehaviourType type, CharacterSkills owner)
    {
        if (!definition.HasBehaviour(type))
            throw new InvalidOperationException($"No {type} behaviour in {definition.name}.");
        Skill behaviour = New(type);
        behaviour.Bind(owner, services);
        return behaviour;
    }

    // Nơi DUY NHẤT ánh xạ enum → class hành vi. Thêm hành vi mới = thêm giá trị enum (ở cuối) + một dòng ở đây.
    private static Skill New(SkillBehaviourType type)
    {
        switch (type)
        {
            case SkillBehaviourType.NormalAttack: return new NormalAttackSkill();
            case SkillBehaviourType.Shooting: return new ShootingSkill();
            case SkillBehaviourType.OrbitSaw: return new OrbitSawSkill();
            case SkillBehaviourType.SlowAura: return new FrostAuraSkill();
            case SkillBehaviourType.Regeneration: return new RegenerationSkill();
            case SkillBehaviourType.EmergencyShield: return new EmergencyShieldSkill();
            case SkillBehaviourType.StatBuff: return new StatBonusSkill();
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "No behaviour class for this type.");
        }
    }

    #endregion
}
