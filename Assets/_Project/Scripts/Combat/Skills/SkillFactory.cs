using System;
using System.Collections.Generic;

// Only creates runtime behaviors from their shared configuration templates.
public sealed class SkillFactory
{
    private readonly Dictionary<Type, Func<ISkill, CharacterSkills, ISkill>> creators;

    public SkillFactory(ShootingService shooting)
    {
        if (shooting == null) throw new ArgumentNullException(nameof(shooting));

        creators = new Dictionary<Type, Func<ISkill, CharacterSkills, ISkill>>
        {
            {
                typeof(NormalAttackSkill),
                (_, owner) => new NormalAttackSkill(owner)
            },
            {
                typeof(ShootingSkill),
                (template, owner) =>
                {
                    var config = (ShootingSkill)template;
                    return new ShootingSkill(owner, config.Shooting, shooting);
                }
            },
            {
                typeof(RegenerationSkill),
                (template, owner) =>
                {
                    var config = (RegenerationSkill)template;
                    return new RegenerationSkill(owner, config.HealAmount);
                }
            },
            {
                typeof(EmergencyShieldSkill),
                (template, owner) =>
                {
                    var config = (EmergencyShieldSkill)template;
                    return new EmergencyShieldSkill(owner, config.HealthThreshold, config.ShieldAmount);
                }
            },
            {
                typeof(StatBonusSkill),
                (template, owner) =>
                {
                    var config = (StatBonusSkill)template;
                    return new StatBonusSkill(owner, config.Bonus);
                }
            }
        };
    }

    public ISkill Create(SkillDataSO definition, CharacterSkills owner)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        definition.Validate();

        ISkill template = definition.Behavior;
        Type type = template.GetType();

        if (!creators.TryGetValue(type, out var create))
            throw new InvalidOperationException($"Unsupported skill: {type.Name}.");

        return create(template, owner);
    }
}
