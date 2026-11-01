# Skills, character composition and projectiles

See [Skill-Shooting-Pipeline](../../../../Docs/Architecture/Skill-Shooting-Pipeline.md) for the current implementation.

- SkillDataSO retains configuration, visuals and an ISkill template.
- ISkill only exposes bool TryExecute(); concrete behaviors own their specific fields and supplied callbacks.
- SkillManager only schedules independent behavior instances and owns innate/run registration lists.
- CharacterManager publishes character lifetime events; it does not know SkillManager.
- CharacterSkillBridge connects both systems, checks spawn identity and wires behavior dependencies.
- CharacterDataSO.Skills is the single list of innate skill assets, without active/passive categories.
- CharacterView and controllers do not call SkillManager or a shooting scheduler. The bridge reads CharacterDataSO.Skills.
- HolyBolt is an automatically scheduled ShootingSkill; ShootingService only fires projectile patterns.
- Normal attack is automatically scheduled; enemy recoil remains in EnemyController.
- Level/EXP remain in LevelManager. Run selection calls AddRunSkill; RunReset clears run registrations.
- Temporary disable freezes skill timers. Death/despawn cleans up owned registrations and persistent modifiers.
