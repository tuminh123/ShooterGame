# Character architecture: OOP + Event-Driven + Zenject

This document is a reusable architecture for a Unity action game with a Player, normal Enemies, and Bosses. It combines composition-based OOP, a pure C# domain layer, events for loose coupling, and Zenject for dependency injection and lifecycle management.

## Goals

- Player, normal Enemy, and Boss share the same core combat model.
- Character-specific data uses composition, not a deep inheritance tree.
- Runtime logic does not depend on `MonoBehaviour`, `Transform`, or a static `GameManager`.
- The game can add skills, enemy variants, UI, sound, VFX, achievements, and pooling listeners without coupling them to combat code.
- Damage is calculated once in a deterministic pipeline before state is changed.

## The boundary: config, runtime state, and Unity view

Keep these three layers separate.

```text
ScriptableObject asset      Pure C# runtime domain          Unity adapter
-----------------------     --------------------------      ----------------
Definition/config       ->  Character + CharacterStats  <-  CharacterView
Skill definitions       ->  Skills / AI / damage rules      Sprite/Animator
Prefab reference        ->  Events and commands             Collider/Transform
```

### 1. Definition/config data

Config is the template authored in the Inspector. It must not be changed while the game is running. Multiple enemies can share one definition asset.

### 2. Runtime domain data

Every spawned character owns its own pure C# runtime state. This is where current HP, cooldowns, buffs, and active phase live. A pooled enemy gets a fresh runtime state when it spawns.

### 3. Unity view

`CharacterView`, `PlayerView`, and `EnemyView` are `MonoBehaviour` adapters. They handle only Unity concerns: transform, sprite, animator, collider callbacks, and VFX. They forward input/collision information to domain services; they do not calculate combat rules.

## Character stats

Use two related classes, because configuration and mutable runtime data have different responsibilities.

```text
CharacterStatsConfig       immutable template data
CharacterStats             mutable runtime data + public adjustment methods
```

`CharacterStatsConfig` contains the starting values:

```text
MaxHealth, Damage, Defense, MoveSpeed,
DodgeChance, CritChance, CritDamageMultiplier
```

`CharacterStats` is a pure C# class. It is created from `CharacterStatsConfig` when a character spawns. It has private state and public methods such as:

```text
SetBaseValue(stat, value)
AddModifier(modifier)
RemoveModifier(modifierId)
GetValue(stat)
RestoreHealth(amount)
ApplyHealthChange(amount)
Reset(config)
```

Do not expose public mutable fields. A caller should not be able to set health to an invalid value or change damage without the stat system knowing about it. `CharacterStats` clamps valid ranges:

- `CurrentHealth`: `0 .. MaxHealth`
- `DodgeChance` and `CritChance`: `0 .. 1`
- `CritDamageMultiplier`: at least `1`
- `Defense` and `MoveSpeed`: non-negative

Use a `StatModifier` value object for buffs, equipment, passives, and debuffs. It needs an id, stat type, operation (flat/percent), value, and optional source id. A modifier can then be removed cleanly when a buff expires.

### Health and damage

`MaxHealth` is a basic stat; `CurrentHealth` is runtime state. The `DamageService` calculates final damage, then calls a public `CharacterStats` method to apply the result. This keeps formulas out of Views and stops UI/collision code from directly editing HP.

## Composition for character-specific data

Do **not** make `PlayerData : CharacterData : EnemyData : BossData`. Use a shared member instead.

```text
PlayerDefinition
├── CoreStats: CharacterStatsConfig
├── ActiveSkills
├── PassiveSkills
└── PlayerSpecificData
    ├── pickupRange
    ├── experience settings
    └── revive count

NormalEnemyDefinition
├── CoreStats: CharacterStatsConfig
├── ContactDamage settings
├── SpawnData
└── ChaseData
    ├── stop distance
    └── target priority

BossDefinition
├── CoreStats: CharacterStatsConfig
├── ContactDamage settings
├── BossPhaseDefinitions
└── BossSkillDefinitions
```

Each of these definitions can be represented by a thin `ScriptableObject` asset wrapper for Inspector authoring. The underlying DTO/config classes should have no gameplay methods and should never hold current HP or cooldown values.

## Runtime character model

The common domain object is composition-based.

```text
Character
├── CharacterId
├── CharacterKind                 Player / NormalEnemy / Boss
├── CharacterStats                mutable runtime stats
├── SkillCollection
├── CharacterState                Alive / Dead / Disabled
└── Event publisher
```

Player, normal Enemy, and Boss may use separate controller/brain classes, but they do not need separate copies of health, damage, crit, dodge, or death code.

```text
PlayerController                  input -> movement/active skill command
ChaseEnemyBrain                   target position -> chase movement command
BossBrain                         phase + cooldown -> movement/skill commands
```

`Character` owns state. A controller/brain chooses actions. A view renders the outcome. This is Single Responsibility Principle in practice.

## Skills

Skills come in two categories but share a common lifecycle and context.

```text
ISkill
├── SkillId
├── Initialize(context)
├── Tick(deltaTime)
└── Dispose()

IActiveSkill : ISkill
└── TryCast(command)

IPassiveSkill : ISkill
└── Reacts to relevant domain events or contributes modifiers
```

Examples:

```text
Active:  PlayerDash, BossCharge, BossCircleShot, BossSummon
Passive: +Damage, +CritChance, Thorns, HealOnKill, BossEnrageAtLowHealth
```

Use direct methods for a deliberate command such as `TryCast`; use events for facts that already happened, such as `DamageApplied` or `CharacterDied`.

### Important damage rule

Passives that alter incoming/outgoing damage must participate in the **damage pipeline before health changes**. They should not wait for a post-damage event.

```text
DamageRequest
    -> Dodge check
    -> Crit check
    -> Damage modifiers
    -> Defense mitigation
    -> Apply health change
    -> Publish DamageApplied / HealthChanged / CharacterDied
```

## Event-Driven Architecture

Events communicate completed facts. They remove direct dependencies between combat, UI, audio, VFX, quests, and spawning.

Core domain events:

```text
CharacterSpawned
CharacterDespawned
HealthChanged
DamageApplied
CharacterDied
SkillCastStarted
SkillCastFinished
BossPhaseChanged
EnemyKilled
ExperienceCollected
```

Recommended event flow:

```text
Enemy collision view
    -> DamageService.Apply(DamageRequest)          direct command
    -> CharacterStats.ApplyHealthChange()          direct state update
    -> HealthChanged event                          completed fact
    -> CharacterDied event if HP reaches zero       completed fact

Listeners:
    HealthBarPresenter -> update UI
    CharacterView      -> play hit/death animation
    AudioService        -> play sound
    ExperienceService   -> spawn experience
    EnemySystem         -> unregister and return view to pool
```

### Zenject SignalBus

Use Zenject `SignalBus` for cross-system events. Bind and fire typed signal classes; do not use global static C# events.

```text
Signal: CharacterDiedSignal
{ CharacterId, KillerId, CharacterKind, Position }
```

Signals should be small immutable messages. They carry data, not references to mutable domain objects or Unity GameObjects unless a Unity-only listener specifically requires one.

### Event rules

1. Events describe the past: `CharacterDied`, never `PleaseKillCharacter`.
2. Commands request work: `DamageService.Apply`, `Spawner.Spawn`.
3. Do not use an event bus for tight internal calculations such as a stat getter.
4. Subscribe/unsubscribe pooled views on spawn/despawn. Never leave stale listeners in a pool.
5. Avoid event chains with hidden gameplay changes. The damage pipeline remains the single owner of damage resolution.

## Zenject services and bindings

Replace `GameManager.Instance` with focused interfaces. Each service is bound with `AsSingle()` in the scene installer.

```text
IPlayerTarget              provides current Player position/state
ICharacterRegistry         tracks active runtime characters
IDamageService             owns the damage pipeline
IEnemySpawnDirector        decides when and what to spawn
IEnemyFactory              obtains the correct enemy view from a pool
ISpawnPointProvider        returns points on the camera-relative rectangle
IClock                     exposes game time / pause-aware delta time
```

Suggested Zenject ownership:

```text
SceneInstaller
├── Bind<IPlayerTarget>().To<PlayerTargetService>().AsSingle()
├── Bind<ICharacterRegistry>().To<CharacterRegistry>().AsSingle()
├── Bind<IDamageService>().To<DamageService>().AsSingle()
├── BindInterfacesAndSelfTo<CharacterSystem>().AsSingle()
├── BindInterfacesAndSelfTo<EnemySpawnDirector>().AsSingle()
├── Bind<ISpawnPointProvider>().To<RectangleSpawnPointProvider>().AsSingle()
└── BindSignalBus()
```

`CharacterSystem` can implement `ITickable` and update runtime character controllers in one central loop. This avoids one `Update()` per enemy when the game reaches hundreds of enemies.

Use Zenject `MemoryPool` for enemies, bosses, bullets, and experience objects. Each pooled view must reset its runtime `Character` from its definition during spawn, and clear references/listeners during despawn.

## Enemy and boss spawning

The spawn director chooses a definition, asks `ISpawnPointProvider` for a camera-relative point, then asks the factory for an instance.

```text
EnemySpawnDirector
    -> choose NormalEnemyDefinition using spawn rules
    -> spawn point on rectangle perimeter
    -> EnemyFactory.Spawn(definition, point)
    -> CharacterRegistry.Register(character)
    -> publish CharacterSpawned
```

Bosses use a separate `BossSpawnDirector` or an explicit timeline trigger. A boss uses the same `Character`, stats, damage, registry, pool, and death pipeline as a normal enemy; only its `BossBrain`, phase data, and skill collection differ.

## SOLID mapping

| Principle | Application |
|---|---|
| Single Responsibility | Stats store stats; `DamageService` resolves damage; Views render; SpawnDirector schedules spawns. |
| Open/Closed | New boss skill implements `IActiveSkill`; new passive implements `IPassiveSkill`; no edit to existing boss logic. |
| Liskov Substitution | Callers depend on `IPlayerTarget`, `ISkill`, `IDamageService`, not concrete Player/Boss types. |
| Interface Segregation | Small interfaces: a chasing enemy only needs `IPlayerTarget`, not a complete GameManager. |
| Dependency Inversion | Controllers depend on `IDamageService`, `IClock`, and factories injected by Zenject. |

## Suggested folder structure

```text
Scripts/
├── Domain/
│   ├── Characters/
│   │   ├── Character.cs
│   │   ├── CharacterStats.cs
│   │   ├── CharacterStatsConfig.cs
│   │   └── CharacterRuntimeState.cs
│   ├── Combat/
│   │   ├── DamageRequest.cs
│   │   └── DamageService.cs
│   └── Skills/
├── Application/
│   ├── Spawning/
│   ├── Systems/
│   └── Registries/
├── Presentation/
│   ├── Player/
│   ├── Enemies/
│   ├── Bosses/
│   └── UI/
├── Infrastructure/
│   ├── Zenject/
│   ├── Pools/
│   └── UnityAdapters/
└── Data/
    ├── Characters/
    ├── Enemies/
    └── Bosses/
```

## First implementation order

1. Implement `CharacterStatsConfig`, `CharacterStats`, and `DamageService` with tests.
2. Create Player, normal Enemy, and Boss definitions using composition.
3. Add `Character`, `CharacterRegistry`, and typed death/damage signals.
4. Refactor Player and normal Enemy views to call the domain services.
5. Replace enemy Lean Pool with Zenject `MemoryPool`.
6. Add `BossBrain`, phases, and one active boss skill.

Do not introduce every system at once. Build the stat/damage core first; all characters and skills will depend on it.
