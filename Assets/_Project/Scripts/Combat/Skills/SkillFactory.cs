using System;
using System.Collections.Generic;

// Only creates runtime behaviors from their shared configuration templates.
public sealed class SkillFactory
{
    private readonly Dictionary<Type, Func<ISkillData, CharacterSkills, float, ISkill>> creators;
    private readonly CombatSpawner combatSpawner;
    private readonly ShootBehaviorFactory shootFactory;
    private readonly ProjectileMoveFactory moveFactory;
    private readonly PoolManager pools;

    public SkillFactory(CombatSpawner spawner, ShootBehaviorFactory shootFactory, ProjectileMoveFactory moveFactory, PoolManager pools)
    {
        if (spawner == null) throw new ArgumentNullException(nameof(spawner));
        combatSpawner = spawner;
        this.shootFactory = shootFactory ?? throw new ArgumentNullException(nameof(shootFactory));
        this.moveFactory = moveFactory ?? throw new ArgumentNullException(nameof(moveFactory));
        this.pools = pools ?? throw new ArgumentNullException(nameof(pools));

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
                (_, owner, _) => new StatBonusSkill(owner)
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
            return new ShootingSkillAction(shootingData, owner, combatSpawner, shootFactory, moveFactory,
                definition.Range, definition.MaxLevel);
        if (template is OrbitSawSkill saw) return new OrbitSawSkillRuntime(saw, owner, pools);
        if (template is FrostAuraSkill frost) return new FrostAuraSkillRuntime(frost, owner, pools);
        Type skillDataType = template.GetType();

        if (!creators.TryGetValue(skillDataType, out var createRuntimeSkill))
            throw new InvalidOperationException($"Unsupported skill: {skillDataType.Name}.");

        return createRuntimeSkill(template, owner, definition.Range);
    }
}
