# Demo: Player + Chasing Enemy + Boss

This is a concrete, small-scale version of the character architecture. It is intentionally limited to one Player, one normal Enemy type, and one Boss type.

## Demo scope

```text
Player
├── moves from joystick input
├── receives contact damage
└── has one active skill and one passive skill

Normal Enemy
├── spawns from EnemySpawnDirector
├── follows Player
└── deals contact damage

Boss
├── follows Player as a baseline behavior
├── changes phase at 50% HP
├── uses Charge as an active skill
└── gains Enrage as a passive in phase 2
```

## Class map

```text
                         ┌───────────────────────────┐
                         │     CharacterDefinition   │
                         │   (config / immutable)    │
                         └─────────────┬─────────────┘
                                       │ creates
                                       ▼
┌──────────────┐              ┌───────────────────────────┐
│ CharacterView│ <----------> │         Character         │
│ Unity adapter│              │ pure runtime domain object │
└──────┬───────┘              ├───────────────────────────┤
       │                      │ CharacterStats            │
       │ collision/input      │ SkillCollection            │
       ▼                      │ CharacterState             │
┌──────────────┐              └─────────────┬─────────────┘
│ Application  │                            │ controlled by
│ services     │                            ▼
└──────┬───────┘              ┌───────────────────────────┐
       │                      │ ICharacterController      │
       │                      └───────┬─────────┬─────────┘
       │                              │         │
       ▼                              ▼         ▼
DamageService                 PlayerController  ChaseEnemyBrain / BossBrain
```

## Data composition

No character data inherits from another character data class.

```text
PlayerDefinition
├── CharacterStatsConfig
│   ├── MaxHealth: 100
│   ├── Damage: 10
│   ├── Defense: 2
│   ├── MoveSpeed: 4
│   ├── DodgeChance: 0.05
│   ├── CritChance: 0.10
│   └── CritDamageMultiplier: 1.5
├── ActiveSkills: [PlayerDash]
├── PassiveSkills: [CritBonus]
└── PlayerSpecificData
    ├── PickupRange: 2
    └── ReviveCount: 0

ChaserEnemyDefinition
├── CharacterStatsConfig
│   ├── MaxHealth: 25
│   ├── Damage: 8
│   ├── Defense: 0
│   ├── MoveSpeed: 1.8
│   └── other values: 0/default
├── EnemySpecificData
│   ├── ExperienceReward: 1
│   └── StopDistance: 0.25
└── Behaviours: [ChasePlayer]

BossDefinition
├── CharacterStatsConfig
│   ├── MaxHealth: 1000
│   ├── Damage: 20
│   ├── Defense: 5
│   └── MoveSpeed: 1.2
├── BossPhaseDefinitions
│   ├── Phase 1: 100% → 51%, skills [Charge]
│   └── Phase 2: 50% → 0%, skills [Charge], passives [Enrage]
└── BossSpecificData
    └── IntroDelay: 1 second
```

## Runtime objects created after spawn

Every spawned object receives its own runtime state. Definitions above are shared templates and never store current HP.

```text
Character
├── Id: runtime GUID/int
├── DefinitionId: "chaser_enemy"
├── Stats: CharacterStats
│   ├── CurrentHealth: 25
│   └── modifiers: []
├── State: Alive
├── Skills: SkillCollection
└── Controller: ChaseEnemyBrain
```

`CharacterStats` is the pure class requested for fundamental stats. It owns validated mutations. Its public API is conceptually:

```text
Get(statType)
SetBaseValue(statType, value)
AddModifier(modifier)
RemoveModifier(modifierId)
RestoreHealth(amount)
ApplyHealthChange(amount)
Reset(config)
```

The object calling `ApplyHealthChange` is normally `DamageService`, not a View or collider script.

## Spawn flow

```text
EnemySpawnDirector (ITickable)
    │ every spawn interval
    ▼
SpawnRule chooses ChaserEnemyDefinition
    │
    ▼
RectangleSpawnPointProvider chooses one fixed point around camera
    │
    ▼
EnemyFactory obtains EnemyView from Zenject MemoryPool
    │
    ├── creates fresh Character from definition
    ├── creates ChaseEnemyBrain
    ├── binds View ↔ Character
    ├── CharacterRegistry.Register(character)
    └── publishes CharacterSpawnedSignal
```

The factory is responsible for matching the definition to the correct pool/prefab. The Spawn Director never knows how pooling works.

## Contact damage flow

```text
EnemyView Collider2D touches PlayerView Collider2D
    │
    ▼
EnemyView forwards ContactHit command to DamageService
    │
    ▼
DamageService builds DamageRequest
    ├── attacker: ChaserEnemy Character
    ├── target: Player Character
    └── base damage: attacker.Stats.Damage
    │
    ▼
DamagePipeline
    ├── Dodge check using target.Stats.DodgeChance
    ├── Crit check using attacker.Stats.CritChance
    ├── Apply outgoing/incoming modifiers
    ├── Apply defense formula
    └── target.Stats.ApplyHealthChange(-finalDamage)
    │
    ▼
Publish DamageAppliedSignal
    │
    ├── PlayerHealthPresenter updates HUD
    ├── PlayerView plays hit animation
    └── Audio/VFX listeners react
    │
    ▼ if HP <= 0
Publish CharacterDiedSignal
```

Damage is a direct command because it must be ordered and deterministic. `DamageAppliedSignal` is emitted afterward because it is a fact that other systems may observe.

## Boss phase and skills

`BossBrain` is a pure class. It owns phase selection; it does not know about Animator, SpriteRenderer, or Collider2D.

```text
BossBrain.Tick(deltaTime)
    │
    ├── evaluate CurrentHealth / MaxHealth
    │       └── at <= 50%: enter Phase 2
    │               ├── add Enrage modifier (+30% move speed)
    │               └── publish BossPhaseChangedSignal
    │
    ├── baseline: run ChasePlayer behaviour
    │
    └── if Charge cooldown finished
            └── ChargeSkill.TryCast(skillContext)
```

`ChargeSkill` is an `IActiveSkill`. It calculates a target direction and asks a movement service to execute the charge. `EnragePassive` is an `IPassiveSkill`; it adds/removes a `StatModifier`, rather than editing MoveSpeed directly.

## Event ownership

```text
System / service              Owns the command             Publishes facts
---------------------------   --------------------------   ------------------------
DamageService                 Resolve and apply damage     DamageApplied, CharacterDied
EnemyFactory                  Create / reset enemy         CharacterSpawned
EnemyLifecycleService         Despawn pooled enemy         CharacterDespawned
BossBrain                     Change boss phase            BossPhaseChanged
SkillExecutor                 Execute a skill              SkillCastStarted/Finished
```

Views do not publish gameplay outcomes directly. They report Unity facts such as collision/input to the owning service. This prevents two systems from applying contact damage twice.

## Zenject dependency sketch

```text
SceneInstaller
├── SignalBus
├── IPlayerTarget              -> PlayerTargetService       AsSingle
├── ICharacterRegistry         -> CharacterRegistry          AsSingle
├── IDamageService             -> DamageService              AsSingle
├── ISpawnPointProvider        -> RectangleSpawnPointProvider AsSingle
├── IEnemyFactory              -> EnemyFactory               AsSingle
├── EnemySpawnDirector         -> ITickable                  AsSingle
└── CharacterSystem            -> ITickable                  AsSingle
```

`PlayerTargetService` is the replacement for a static `GameManager.Instance.PlayerTransform`. Enemy/Boss logic depends on `IPlayerTarget`, so it can be tested with a fake target and reused in another project.

## What stays MonoBehaviour

```text
PlayerView
EnemyView
BossView
CharacterCollisionReceiver
PlayerInputAdapter
CameraBoundsAdapter
HealthBarPresenter
```

Everything else in this demo is either pure C# or a ScriptableObject configuration asset.

## Build order for this demo

1. Create `CharacterStatsConfig` and pure `CharacterStats`.
2. Create `DamageRequest`, `DamageService`, and health/death signals.
3. Make Player and the current Enemy use those shared stats.
4. Replace current enemy spawn with `EnemySpawnDirector` + factory/pool.
5. Add one Boss prefab, `BossDefinition`, phase 2, `ChargeSkill`, and `EnragePassive`.
6. Move `Update()` logic into `CharacterSystem` only when normal Enemy behavior is working.

Do not refactor movement, UI, skills, and pooling in one step. Verify damage and death first, then add the factory/pool, then add Boss behavior.
