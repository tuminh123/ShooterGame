using System;
using System.Collections.Generic;

// Only creates runtime behaviors from their shared configuration templates.
public sealed class SkillFactory
{
    private readonly Dictionary<Type, Func<ISkillData, CharacterSkills, float, ISkill>> creators;
    private readonly CombatSpawner combatSpawner;
    private readonly ShootBehaviorFactory shootFactory;
    private readonly ProjectileMoveFactory moveFactory;

    public SkillFactory(CombatSpawner spawner, ShootBehaviorFactory shootFactory, ProjectileMoveFactory moveFactory)
    {
        if (spawner == null) throw new ArgumentNullException(nameof(spawner));
        combatSpawner = spawner;
        this.shootFactory = shootFactory ?? throw new ArgumentNullException(nameof(shootFactory));
        this.moveFactory = moveFactory ?? throw new ArgumentNullException(nameof(moveFactory));

        creators = new Dictionary<Type, Func<ISkillData, CharacterSkills, float, ISkill>>
        {
            {
                typeof(NormalAttackSkill),
                (_, owner, range) => new NormalAttackSkill(owner, range)
            },
            {
                typeof(RegenerationSkill),
                (template, owner, _) =>
                {
                    var skillData = (RegenerationSkill)template;
                    return new RegenerationSkill(owner, skillData.HealAmount);
                }
            },
            {
                typeof(EmergencyShieldSkill),
                (template, owner, _) =>
                {
                    var skillData = (EmergencyShieldSkill)template;
                    return new EmergencyShieldSkill(owner, skillData.HealthThreshold, skillData.ShieldAmount);
                }
            },
            {
                typeof(StatBonusSkill),
                (template, owner, _) =>
                {
                    var skillData = (StatBonusSkill)template;
                    return new StatBonusSkill(owner, skillData.Bonus);
                }
            }
        };
    }

    public ISkill Create(SkillDataSO definition, CharacterSkills owner)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        definition.Validate();

        ISkillData template = definition.Data;
        // Mọi lớp data bắn dùng cùng action; kiểu bắn cụ thể do ShootBehaviorFactory chọn.
        if (template is ShootingSkill shootingData)
            return owner.CreateShootingAction(shootingData, combatSpawner, shootFactory, moveFactory, definition.Range);
        Type skillDataType = template.GetType();

        if (!creators.TryGetValue(skillDataType, out var createRuntimeSkill))
            throw new InvalidOperationException($"Unsupported skill: {skillDataType.Name}.");

        return createRuntimeSkill(template, owner, definition.Range);
    }
}
