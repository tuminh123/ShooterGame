# Character skills and independent run skills

## Boundaries

- `CharacterManager` tracks characters in the scene; it does not schedule skills.
- `CharacterView.Skills` (`CharacterSkills`) owns only innate skills from `CharacterDataSO.Skills`.
- `CharacterControllerBase.Tick` advances innate skills before calling the derived movement hook. The view only forwards its frame update to the controller.
- The scene `SkillManager` independently owns run skills received from an external source. It has no character, character factory, position anchor or character stats dependency.
- `SkillRuntime` is a live behavior and cooldown entry. It has no owner/character dependency. Each collection decides when to tick and dispose its entries.
- `SkillFactory` creates character-bound runtime actions from `SkillDataSO.Data`. `ISkillData` identifies serialized settings; `ISkill` identifies scheduled runtime actions.
- `ShootingSkill` stores configuration and executes a complete volley in `ExecuteSkill`. Each scheduled character gets a runtime copy with its own shooter instance. `CharacterSkills` only prepares targeting/position/direction and calls that method.

## Innate skill flow

1. `CharacterView.Initialize` creates `CharacterSkills` from the character data before activating the pooled shell.
2. The factory creates fresh behaviors bound to that spawn; shared asset templates are never executed or mutated.
3. The base controller advances only the character's innate collection. The collection checks active/alive/spawn identity before each entry.
4. Disable freezes innate cooldowns. Death, despawn and destruction dispose only that character's innate skills and persistent modifiers.
5. Character cleanup never calls `SkillManager` or removes run registrations.

## Run skills: integration deferred

The card/reward source is not implemented. There are currently no gameplay callers that grant run skills.

The integration entry point is:

```csharp
skillManager.AddSkill(selectedDefinition, runtimeBehavior);
```

- A future external source supplies the `SkillDataSO` configuration and a separately created `ISkill` instance.
- The manager registers the instance and advances its cooldown through Zenject's tick. It does not create character-bound behaviors or discover assets automatically.
- The shared behavior template cannot be registered directly. Duplicate live definitions are rejected within the run collection.
- Position sourcing, standalone run-skill damage/range, card selection and progression are intentionally not connected yet. Do not obtain them from character runtime as an implicit fallback.
- Character disable, death, despawn or replacement does not reset or remove run skills.
- `LevelManager.RunReset` clears only run skills. Explicit run-exit coordination can call `ClearRunSkills()` later. Innate skills and their timers are untouched.

## Common scheduling rules

- New entries start ready. A successful `TryExecute()` starts cooldown; failure leaves the entry ready for a future tick.
- A skill attempts at most once per tick. Zero delta does not execute skills.
- Run registrations added during a manager tick wait until its next tick.
- Execution failure disposes the failed entry and is logged while other skills continue.
- Cleanup marks entries inactive before releasing modifiers and attempts every affected entry even when a disposer fails.

## Character behaviors

- `NormalAttackSkill` finds an opponent, checks contact and asks its character to deal damage.
- `ShootingSkill.ExecuteSkill` validates settings, resolves movement, builds `ProjectileSpawnData`, calculates each bullet direction and requests individual spawns. A successful volley calls `IShooter.OnShotCompleted`; only `SpiralShooter` advances its own rotation. Cooldown stays in `SkillRuntime`.
- Other skill types retain their existing template/runtime implementation; this refactor separates shooting data without changing their gameplay.
- `RegenerationSkill` heals its injured owner; the sample heals 1 HP every 2 seconds after success.
- `EmergencyShieldSkill` grants 30 shield at <=20% HP, with a 5-second cooldown.
- `StatBonusSkill` applies once and owns an idempotent modifier lease. It grants only newly added defense-derived shield capacity, never refilling previously spent shield. Synchronous despawn during application releases the lease.

## Authoring

1. Configure a `SkillDataSO` using the inspector skill-data selector.
2. For a new character behavior, implement its gameplay logic and add its constructor call to `SkillFactory`.
3. Equip innate assets in `CharacterDataSO.Skills`.
4. Connect the future card source to the independent manager entry point when run behavior creation and its inputs are defined.

## Shooting and visual effects

`CharacterSkills` prepares origin and aim, then calls the runtime `ShootingSkill.ExecuteSkill`. That method loops over the volley and calls `CombatSpawner.SpawnBullet` for each direction. `IShooter` receives only aim and bullet index/count; it never receives a skill or externally managed rotation. `CombatSpawner` and `ProjectileView` receive `ProjectileSpawnData`, with no skill dependency.

- Select `ShootingSkill` in the skill asset, then choose `StraightShooter`, `CircularShooter` or `SpiralShooter` inside its Shooter field. Pattern-specific angles live directly on those serializable implementations. Common count, speed, damage, targeting and lifetime stay on the skill. Separate shooting-data subclasses are removed.
- Select `StraightProjectileMove` or `HomingProjectileMove` directly in the Projectile Move field. There are no empty movement-data classes, string type selectors or custom type attributes. Factories register `Dictionary<Type, IShooter>` and `Dictionary<Type, IProjectileMove>` keyed by the implementation types. Shooter instances retain their serialized angle settings; live rotation is private to each runtime `SpiralShooter`. `IShooter.CreateRuntime` copies configuration and resets its own runtime state; no shooter instance is shared between running skills. Movement strategies retain no per-bullet state.
- `IProjectileMove.GetDirection` only determines heading. Homing continues in its last direction if its original target spawn is no longer valid. `MovementManager.SetMovement` receives heading and skill speed; `MovementManager.LateTick` applies position changes.
- `ProjectileManager` is the single Zenject tick for projectile lifetime, heading and collision checks. `ProjectileView` has no `Update`. `OnEnable` registers with both managers; `OnDisable` unregisters. A snapshot plus unique movement handles prevents callbacks from updating an old pooled spawn or ticking a newly spawned bullet mid-iteration.
- Speed and damage are copied directly from skill data when a bullet spawns. Projectile damage no longer scales with character damage or rolls character critical stats. Melee damage is unchanged. Sample assets use 10 damage (PoisonBolt: 5), preserving their old damage at a base character damage of 10 before crits/bonuses. IceBolt uses `HomingProjectileMove`; the other sample shooting assets use `StraightProjectileMove`.
- `CombatAssetsSO` is the resource catalog: one shared `ProjectileView` prefab, sprite/color/width and impact effect type per bullet type, and pooled `EffectView` prefabs per effect type. It contains no damage, cooldown or live phase.
- `CombatSpawner` reads the catalog and initializes bullets/effects before the pool activates them. `EffectType.None` skips visual effect spawning.
- `ProjectileView` snapshots damage/team, speed, hit radius, lifetime and the resolved movement strategy at spawn. It does not retain the skill or the character source.
- Bullet collision checks initial overlaps and sweeps the entire movement segment with a circle cast. It selects the first valid opponent without clamping travel distance during slow frames.
- A hit applies damage and releases the shell. Pooled views reset their movement reference and per-bullet state when disabled.
- TODO: Add a separate on-hit system for status effects and impact visuals. Shooting currently applies neither. Existing effect assets and effect-system code remain available; legacy effect references in sample YAML are retained for later migration and are not consumed by shooting data.

The previous `ShootingDataSO`, pattern assets, per-projectile assets, pattern/flight/hit factories and separate projectile runtime are removed. The unused homing prefab is also removed; homing uses the same bullet shell.

Skill upgrades, permanent skill progression and selection UI are not implemented. Character/EXP level progression remains in the existing level services.
