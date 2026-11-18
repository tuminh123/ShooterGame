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

    /// <summary>Tạo bản sao chạy được của hành vi <paramref name="type"/> từ template trong <paramref name="definition"/>.</summary>
    public ISkill CreateBehaviour(SkillDataSO definition, SkillBehaviourType type, CharacterSkills owner)
    {
        // Kiểm tra kiểu ở đây là kiểm tra dữ liệu, không phải chọn hành vi: chỉ class con của Skill
        // mới có phần clone template an toàn. SkillDataSO.Validate cũng chặn trường hợp này từ sớm.
        if (!(definition.GetBehaviour(type) is Skill template))
            throw new InvalidOperationException("Behaviour templates must inherit Skill.");
        return template.CreateRuntime(owner, services);
    }

    #endregion
}
