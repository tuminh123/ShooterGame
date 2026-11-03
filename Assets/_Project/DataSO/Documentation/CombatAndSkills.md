# Character skills and independent run skills

See [Skill-Shooting-Pipeline](../../../../Docs/Architecture/Skill-Shooting-Pipeline.md) for the full flow.

- `CharacterView.Skills` owns innate skills from character data. `CharacterControllerBase` advances their cooldowns.
- `SkillFactory` creates those character-bound strategies. Each behavior owns its gameplay logic.
- The scene `SkillManager` independently schedules run skills supplied by an external system. It has no character dependency.
- Future integration calls `skillManager.AddSkill(skillData, runtimeBehavior)`. No card/reward source is connected yet.
- Run-skill position and combat stats are deferred. They are not implicitly taken from the player.
- `SkillRuntime` holds one live behavior and its cooldown without knowing a character.
- Character cleanup affects only innate skills. Run reset or explicit run cleanup affects only manager-owned skills.
- Shared skill assets remain configuration/templates, never live state.
- `CharacterManager` only tracks characters. `ShootingSkill` selects a shooter from `Dictionary<Type, IShooter>`; `CombatSpawner` creates bullets and visual effects through the pool.
- Edit shooting/gameplay settings directly on each skill behavior. `CombatAssets` in `DataSO/Databases` holds the shared bullet prefab, bullet visuals and effect prefabs.
- Bullet phase belongs to each live skill. Damage is snapshotted per bullet, and gameplay status effects remain separate from visual impacts.
- Skill upgrades and selection UI are not implemented yet.
