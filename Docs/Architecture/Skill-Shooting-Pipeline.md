# Skill and shooting pipeline

See [Skill Run Progression](Skill-Run-Progression.md) for the current model.

SkillDataSO stores complete per-level behavior data and a separate ISkill template
list. Enum types pair the data with the behavior. Abstract Skill supplies default
lifecycle methods. SkillRuntime activates only behaviors with data at its level.
There is no ShootingLevelTable, root stat bonus, string behavior ID or wrapper entry.

ShootingData owns the shooter/movement/projectile settings for a single level.
ShootingSkill snapshots a volley, uses shared shooting/movement services and gives
projectiles independent damage/effect snapshots. The existing projectile/VFX
pipeline below remains in use.

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
- `EffectManager` checks spawn identity, team, chance and resistance, then rents an `EffectRuntime`. Reapplying the same timed configuration object to the same target refreshes duration without creating another runtime or resetting periodic cadence. Separate configuration entries remain independent. Each slow runtime owns a percentage MoveSpeed modifier; slows add together and final speed is clamped at zero.
- Configuration types derive from `GameplayEffectData`; running behaviors derive from `EffectRuntime`, which owns `Apply`, `Tick` and `Remove`. Slow-specific modifier ownership lives in `SlowEffectRuntime`; damage behavior lives in `DamageOverTimeEffectRuntime`. Duration and interval accumulation are calculated by the shared `TimerJob`; runtime Tick receives the number of due ticks.
- `ClassPool<T>` wraps Zenject `MemoryPool<T>` for plain C# objects. `EffectRuntimePool` registers pools by runtime type. The installer preallocates 16 slow runtimes and 32 periodic-damage runtimes; pools expand when necessary. Burn and poison can reuse the same runtime type while remaining separate configurations.
- `EffectManager` has no per-frame tick. It receives timer events on the main thread, uses dictionaries to find reapplications and per-target groups for pause/cleanup, and removes runtimes by swapping the final list element. Group lists are reused. Returns are deferred until the current effect operation finishes, so synchronous damage/stat callbacks cannot recycle a runtime while it is still executing. Removal clears modifiers; reset clears target/configuration references before returning to the pool.
- `CharacterView` directly asks `EffectManager` to clear effects on death, destruction and `ClearSpawn`, and pauses timers on temporary disable. `FrostAuraSkill` applies a maintained `SlowEffectRuntime` on entry and removes its versioned handle on exit, pause, data replacement or disposal. Maintained applications create independent runtimes even for a shared configuration; aura slow keeps its guaranteed application behavior. Target identity and handle versions prevent effects from crossing pooled lifetimes.
- Gameplay duration is independent of VFX lifetime. Periodic damage is a fixed `damagePerTick` chosen in each skill and goes through `ReceiveDamage`, including shield absorption. Sample burn uses 2 damage each second for 3 seconds, poison uses 2.5 each second for 4 seconds, and slow reduces speed by 30% for 2 seconds. These periodic values match the old multiplier-based assets at source damage 10; future character damage changes no longer scale them implicitly. Application chance still considers effect hit/resistance when enabled.
- Sample skill YAML now embeds managed effect configurations. The old effect SO assets/classes remain as unused source data; they are no longer applied by the gameplay pipeline.
- Adding an effect requires a serializable configuration and runtime behavior. Register a pool in `GameSceneInstaller` and `EffectRuntimePool` only when introducing a new runtime type. The inspector discovers new concrete serializable configurations automatically.

The previous `ShootingDataSO`, pattern assets, per-projectile assets, pattern/flight/hit factories and separate projectile runtime are removed. The unused homing prefab is also removed; homing uses the same bullet shell.

Skill upgrades, permanent skill progression and selection UI are not implemented. Character/EXP level progression remains in the existing level services.
