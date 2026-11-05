# Character skills and independent run skills

## Boundaries

- `CharacterManager` tracks characters in the scene; it does not schedule skills.
- `CharacterView.Skills` (`CharacterSkills`) owns only innate skills from `CharacterDataSO.Skills`.
- `CharacterControllerBase.Tick` advances innate skills before calling the derived movement hook. The view only forwards its frame update to the controller.
- The scene `SkillManager` independently owns run skills received from an external source. It has no character, character factory, position anchor or character stats dependency.
- `SkillRuntime` is a live behavior and cooldown entry. Its cooldown uses a versioned handle in the shared `TimerManager`; it has no character dependency. Each collection decides when to execute, pause and dispose its entries.
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
- `ProjectileManager` is the single Zenject tick for heading and batched spatial sweeps; lifetime is handled by `TimerManager`. `ProjectileView` has no `Update`. `OnEnable` registers movement, lifetime and projectile state; `OnDisable` unregisters all three. A snapshot plus unique movement handles prevents callbacks from updating an old pooled spawn or ticking a newly spawned bullet mid-iteration.
- Speed and damage are copied directly from skill data when a bullet spawns. Projectile damage no longer scales with character damage or rolls character critical stats. Melee damage is unchanged. Sample assets use 10 damage (PoisonBolt: 5), preserving their old damage at a base character damage of 10 before crits/bonuses. IceBolt uses `HomingProjectileMove`; the other sample shooting assets use `StraightProjectileMove`.
- `CombatAssetsSO` is the resource catalog: one shared `ProjectileView` prefab, sprite/color/width and impact effect type per bullet type. Each VFX entry has an `EffectView` shell prefab and a separate `visualPrefab` with `IEffectVisual` on its root. It contains no damage, cooldown or live phase.
- `CombatSpawner` reads the catalog and initializes bullets/effects before the pool activates them. `EffectType.None` skips visual effect spawning.
- `ProjectileView` snapshots damage/team, speed, hit radius, lifetime and the resolved movement strategy at spawn. It does not retain the skill or the character source.
- In the current Built-in Render Pipeline, `ProjectileManager` checks the gameplay `MainCamera` at `Camera.onPreCull`, after movement and camera follow. A bullet is returned to the pool once its entire sprite bounds leave the camera frustum plus a 0.1-world-unit margin. Scene view cameras are ignored, checks pause with gameplay time, and lifetime remains an independent expiry condition. Bullets spawned entirely outside this region are also returned at that check. The manager unsubscribes when disposed.
- Bullet collision uses a batch of swept-circle versus oriented-box jobs over the shared spatial grid. Sweeps include initial overlap and the entire movement segment. No Physics2D query is used. The shared character prefab no longer has a collider; `CharacterDataSO.Hitbox` supplies the shape.
- An accepted hit applies gameplay effects through `EffectManager`, then releases the shell. Dead/despawned targets cannot receive effects. Pooled views reset their movement reference, effect references and per-bullet state when disabled.
- Accepted projectile hits spawn the catalog's impact VFX at the swept-hit position, using data captured before damage callbacks. TODO: Connect skill-start and gameplay-effect-application VFX separately. Gameplay effects do not spawn presentation objects.

## VFX child lifecycle

- Scripts are grouped in `Combat/Effects/Visuals`. `EffectView` is a pooled shell with a duration driven by `ITimerManager`; it has no renderer or per-frame Update. `IEffectVisual` exposes `Play()` and `StopAndReset()`.
- `CombatSpawner.SpawnEffect` prepares the shell while inactive. The shell rents a separate visual prefab from `PoolManager`, parents it under itself and copies the prefab's local transform. On enable, the shell starts its timer and calls `Play`.
- `CombatSpawner.Release` calls `EffectView.Clear` before despawning the shell. Clear cancels the timer, stops/resets the visual and returns the child to its pool before the parent. OnDisable only cancels/stops playback: it does not reparent children while Unity is disabling the hierarchy. A directly destroyed shell cancels its timer and Unity destroys the child normally.
- `ParticleEffectVisual` plays the root ParticleSystem with children, disables particle auto-destroy/disable actions and stops with `StopEmittingAndClear`, including world-space particles. `AnimationEffectVisual` resets and plays an Animator Controller's default state; stopping rebinds and disables its Animator. Use gameplay-scaled animation/particle time to match the shell timer.
- `SpriteFlashEffectVisual` preserves the existing HolyImpact sprite fade/scale as another implementation. `HolyImpact.prefab` retains its original shell GUID; `HolyImpactVisual.prefab` contains the renderer and flash script. Both are referenced by the sample catalog entry.
- To author a VFX: assign the shell to `EffectEntry.prefab`, set its duration, assign the child prefab to `EffectEntry.visualPrefab`, and put the appropriate `IEffectVisual` component on the child prefab root. Animation visuals need an Animator Controller with the desired default state. Particle visuals need a root ParticleSystem; configure Play On Awake off in the prefab. The shell duration is the maximum playback time and forcibly clears remaining particles when it expires.

## Gameplay effects

- `ShootingSkill` stores `[SerializeReference] List<IGameplayEffect>`. Expand Effects, add an entry and choose `SlowEffect`, `BurnEffect` or `PoisonEffect` in the skill inspector. Each selected object contains configuration only.
- `ProjectileSpawnData` carries those configurations plus the source effect-hit stat. The bullet keeps no skill/source-character reference. On an accepted damage hit it calls `EffectManager.Apply(configuration, target, sourceTeam, effectHit)` directly; there is no combat event hub. Other hit producers, such as a future area skill, use the same method.
- `EffectManager` checks spawn identity, team, chance and resistance, then rents an `EffectRuntime`. Reapplying the same configuration object to the same target refreshes duration without creating another runtime or resetting periodic cadence. Separate configuration entries remain independent. Multiple slows resolve to the strongest multiplier.
- Configuration types derive from `GameplayEffectData`; running behaviors derive from `EffectRuntime`, which owns `Apply`, `Tick` and `Remove`. Slow-specific modifier ownership lives in `SlowEffectRuntime`; damage behavior lives in `DamageOverTimeEffectRuntime`. Duration and interval accumulation are calculated by the shared `TimerJob`; runtime Tick receives the number of due ticks.
- `ClassPool<T>` wraps Zenject `MemoryPool<T>` for plain C# objects. `EffectRuntimePool` registers pools by runtime type. The installer preallocates 16 slow runtimes and 32 periodic-damage runtimes; pools expand when necessary. Burn and poison can reuse the same runtime type while remaining separate configurations.
- `EffectManager` has no per-frame tick. It receives timer events on the main thread, uses dictionaries to find reapplications and per-target groups for pause/cleanup, and removes runtimes by swapping the final list element. Group lists are reused. Returns are deferred until the current effect operation finishes, so synchronous damage/stat callbacks cannot recycle a runtime while it is still executing. Removal clears modifiers; reset clears target/configuration references before returning to the pool.
- `CharacterEffectHandler` only owns speed modifiers and forwards death/despawn cleanup to the manager. `CharacterView` pauses effect timers on temporary disable; death, destruction and `ClearSpawn` cancel them. Target identity prevents effects from crossing pooled character lifetimes.
- Gameplay duration is independent of VFX lifetime. Periodic damage is a fixed `damagePerTick` chosen in each skill and goes through `ReceiveDamage`, including shield absorption. Sample burn uses 2 damage each second for 3 seconds, poison uses 2.5 each second for 4 seconds, and slow reduces speed by 30% for 2 seconds. These periodic values match the old multiplier-based assets at source damage 10; future character damage changes no longer scale them implicitly. Application chance still considers effect hit/resistance when enabled.
- Sample skill YAML now embeds managed effect configurations. The old effect SO assets/classes remain as unused source data; they are no longer applied by the gameplay pipeline.
- Adding an effect requires a serializable configuration and runtime behavior. Register a pool in `GameSceneInstaller` and `EffectRuntimePool` only when introducing a new runtime type. The inspector discovers new concrete serializable configurations automatically.

The previous `ShootingDataSO`, pattern assets, per-projectile assets, pattern/flight/hit factories and separate projectile runtime are removed. The unused homing prefab is also removed; homing uses the same bullet shell.

Skill upgrades, permanent skill progression and selection UI are not implemented. Character/EXP level progression remains in the existing level services.
