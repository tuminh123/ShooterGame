using System;

// Only creates runtime behaviors from their shared configuration templates.
public sealed class SkillFactory
{
    private readonly ShootingService shooting;

    public SkillFactory(ShootingService shooting)
    {
        this.shooting = shooting ?? throw new ArgumentNullException(nameof(shooting));
    }

    public ISkill Create(SkillDataSO definition, CharacterSkills owner)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        definition.Validate();

        switch (definition.Behavior)
        {
            case NormalAttackSkill _:
                return new NormalAttackSkill(owner);
            case ShootingSkill config:
                return new ShootingSkill(owner, config.Shooting, shooting);
            case RegenerationSkill config:
                return new RegenerationSkill(owner, config.HealAmount);
            case EmergencyShieldSkill config:
                return new EmergencyShieldSkill(owner, config.HealthThreshold, config.ShieldAmount);
            case StatBonusSkill config:
                return new StatBonusSkill(owner, config.Bonus);
            default:
                throw new InvalidOperationException($"Unsupported skill: {definition.Behavior.GetType().Name}.");
        }
    }
}
