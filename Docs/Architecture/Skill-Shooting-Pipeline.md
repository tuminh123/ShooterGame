# Skill lifecycle and standalone shooting

## Skill structure

Each `SkillDataSO` contains exactly three serialized parts:

- `SkillData`: a value-type struct containing initial level, maximum level and cooldown.
- `SkillVisual`: a plain class containing display name, description and icon.
- `ISkill`: a serialized behavior with only one method, `Execute(owner, target, in data)`.

`Execute` returns `(bool succeeded, IDisposable lifetime)`. A successful instant action returns a null lifetime. Persistent modifiers/subscriptions return a disposable owned by the manager. Behavior instances are shared asset configuration; never store per-owner targets, timers or mutable state on them.

`CharacterDataSO.ActiveSkills` and `PassiveSkills` contain skill assets. Category is determined by the character array, not duplicated on the asset.

## Shared manager

Zenject creates one `SkillManager` and ticks it before normal character Updates. Characters register their definitions while initializing inactive shells. OnEnable activates passives after setup; each successfully applied passive runs once. The manager copies each asset's `SkillData` into private per-slot state, owns cooldowns and disposables, and never changes the asset.

`CharacterView.TryUseSkill(index, target)` forwards to `SkillManager.TryExecute`. Controllers supply targets directly; there is no Combat layer. The manager validates registration, owner identity, alive/active state, slot index and cooldown. Behavior checks its own action conditions. Only successful active execution starts cooldown. A per-slot execution guard prevents recursive use of that slot without blocking unrelated skills.

Temporary disable preserves levels, cooldowns and applied passive lifetimes; timers pause. Death/despawn/destroy unregisters the current handle and disposes its resources. Every registration has a new handle, so callbacks cannot use stale handles against a pooled shell's next lifetime. Callback-triggered despawns are checked again after behavior execution before retaining returned resources.

`TryGetData` returns a copy; `TryLevelUp` increments only the selected slot up to maxLevel. Level currently has no automatic damage/cooldown scaling. A leveled passive is removed and reapplied with its new data. A failed passive application is retried on its configured cooldown.

## Enemy normal attack

`ForagingMaggot_Character.asset` has `EnemyNormalAttack.asset` in active slot zero. Its data starts at level 1, max level 5 and cooldown 0.8 seconds. `EnemyController` requests slot zero against the current Player. `NormalAttackSkill` validates contact/opposing teams and sends `owner.RuntimeData.CreateDamage()` to the victim.

Normal attack damage and crit come exclusively from the character's current FinalStats, which combine CharacterDataSO BaseStats and current bonuses. No skill damage field, multiplier or level-based damage formula is added. The victim computes defense and HP loss using the existing receive-damage path.

EnemyController owns chase, separation and recoil movement. EnemySpawnManager only configures recoil distance/duration; attack cooldown is edited on the skill asset.

## Projectile attacks remain separate

For this migration, `CharacterDataSO.Shooting` holds `ShootingDataSO[]`. Blessed Gladiator keeps HolyBolt here. The shot assets were moved from DataSO/Skills to DataSO/Shooting with their GUIDs preserved. `ShootingDataSO` is an independent ScriptableObject and has no ISkill behavior or SkillDataSO inheritance.

CharacterView keeps one ShootingRuntime per configured shot and calls ShootingService.Tick. This temporary shooting path owns its own cooldown/retry counters and target search, preserving previous auto-fire behavior. ShootingService.Fire still delegates pattern geometry and projectile spawning to the existing services.

Projectile pattern/flight/hit definitions and status effects are unchanged. Burn/Poison use source damage snapshotted at fire time; Slow uses temporary stat modifiers. Projectile damage multipliers remain part of the existing projectile configuration; enemy normal attack does not use them.

## Authoring

1. Create an asset using Shooter Game / Skills / Skill.
2. Set Data (level, maxLevel, cooldown) and Visual.
3. Select Behavior in the Inspector. Serializable ISkill implementations with public default constructors are discovered automatically, and their serialized fields are editable below the selector.
4. Put the asset into a character's Active Skills or Passive Skills list.
5. Request active use through CharacterView.TryUseSkill with a zero-based slot index and a CharacterTarget. No factory registration is necessary.

The old SkillFactory, SkillContext, SkillRuntime, CharacterSkillHandler, ProjectileSkill and specialized skill SO subclasses have been removed.

## Validation

SkillPipelineTests covers asset wiring, struct data isolation, character-derived normal attack damage, independent owner/slot cooldowns, paused timers, levels, passive cleanup/reapplication, stale pool handles, callback despawns, standalone auto-fire, patterns, effects and projectile pool reuse. CharacterSystemTests retains character factory/pooling and damage feedback coverage with the shared manager injected.
