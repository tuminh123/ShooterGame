# Character skills and independent run skills

See [Skill-Shooting-Pipeline](../../../../Docs/Architecture/Skill-Shooting-Pipeline.md) for the full flow.

- `CharacterView.Skills` owns innate skills from character data. `CharacterControllerBase` advances their cooldowns.
- `SkillFactory` creates those character-bound strategies. Each behavior owns its gameplay logic.
- The scene `SkillManager` schedules run skills supplied by an external system, with an explicit owner and a stat modifier lease for each registration.
- Level-up choices call `skillManager.GrantOrUpgrade(skillData, ownerCharacter)`. The explicit owner may be Player or Enemy; the scene UI supplies the current Player. Low-level callers can still use `AddSkill(skillData, runtimeBehavior, ownerCharacter)`.
- Run-skill behaviors use the explicitly supplied owner; no implicit Player lookup is used. Their StatBonus contributes to that owner's final combat stats.
- `SkillRuntime` holds one live behavior and cooldown, plus the owner and stat lease for run registrations.
- Owner death/release disposes its run registrations. Run reset or explicit run cleanup removes only run skills and preserves innate bonuses.
- Shared skill assets remain configuration/templates, never live state.
- `CharacterManager` only tracks characters. `ShootingSkill` selects a shooter from `Dictionary<Type, IShooter>`; `CombatSpawner` creates bullets and visual effects through the pool.
- Edit character bonuses in `SkillDataSO.StatBonus`; edit shooting and effect settings on the behavior. `CombatAssets` in `DataSO/Databases` holds the shared bullet prefab, bullet visuals and effect prefabs.
- Bullet phase belongs to each live skill. Damage is snapshotted per bullet, and gameplay status effects remain separate from visual impacts.
- Run skill upgrades and selection UI are connected. UI offers only the next level and currently caps stars at 5. Persistent skill upgrade/save remains separate work.
