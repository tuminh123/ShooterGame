# Code File Blueprint: Character, Combat, Spawn, Boss

This is the proposed file-level design for the first Zenject refactor. It supports one Player, one chasing normal Enemy, and one Boss with phases and skills. It is a blueprint; build it in the listed order instead of creating every file at once.

## Dependency direction

```text
Presentation (Unity MonoBehaviour)
          ↓ reports Unity input/collision
Application (systems, factories, use cases)
          ↓ calls
Domain (pure character, stats, combat, skills)
          ↑ reads definitions from
Data (ScriptableObject configuration)
          ↑ instantiated and wired by
Infrastructure (Zenject installer, pools, Unity adapters)
```

No Domain file may reference `UnityEngine`, `MonoBehaviour`, `Transform`, `GameObject`, `LeanPool`, or Zenject. Zenject only creates/wires objects; it is not domain logic.

## 1. Domain: shared character core

Path: `Assets/_Project/Scripts/Domain/Characters/`

| File | Type | Responsibility | Depends on |
|---|---|---|---|
| `CharacterKind.cs` | enum | `Player`, `NormalEnemy`, `Boss` | nothing |
| `StatType.cs` | enum | `MaxHealth`, `Damage`, `Defense`, `MoveSpeed`, `DodgeChance`, `CritChance`, `CritDamageMultiplier` | nothing |
| `StatModifier.cs` | readonly value object | Modifier id, target stat, flat/percent operation, value, source id | `StatType` |
| `CharacterStatsConfig.cs` | config DTO | Initial core stat values; immutable after construction | nothing |
| `CharacterStats.cs` | pure mutable class | Owns current health, base values, modifier list, validation and stat events | prior files |
| `CharacterState.cs` | enum | `Alive`, `Dead`, `Despawned` | nothing |
| `Character.cs` | pure entity | Id, kind, stats, state, skills; receives damage result and death transition | stats, skills |

### `CharacterStats.cs` public contract

This is the class requested for basic stats and controlled mutations. It should have private fields, public read-only properties, and these methods:

```text
Reset(CharacterStatsConfig config)
Get(StatType stat)
SetBaseValue(StatType stat, float value)
AddModifier(StatModifier modifier)
RemoveModifier(string modifierId)
RestoreHealth(float amount)
ApplyHealthChange(float signedAmount)
```

`ApplyHealthChange(-10)` reduces health. `CharacterStats` must clamp health to `0..MaxHealth`, chance values to `0..1`, and non-negative stats to `>= 0`. It must not know who attacked or which VFX should play.

## 2. Domain: combat and events

Path: `Assets/_Project/Scripts/Domain/Combat/`

| File | Type | Responsibility |
|---|---|---|
| `DamageRequest.cs` | immutable value object | Attacker id, target id, raw damage, damage tags/source |
| `DamageResult.cs` | immutable value object | Final damage, dodged/critical flags, target remaining health |
| `IDamageModifier.cs` | interface | Allows passive skills/effects to alter a request in the pipeline |
| `DamageCalculator.cs` | pure class | Dodge → crit → modifier → defense calculation; no state mutation |
| `DamageService.cs` | application-facing service | Gets characters from registry, calls calculator, applies final health change, emits facts |
| `CharacterDamagedEvent.cs` | immutable domain event | Published after valid damage resolves |
| `CharacterDiedEvent.cs` | immutable domain event | Published once when state becomes dead |

### Why two damage files?

`DamageCalculator` is deterministic and easy to unit test with numbers. `DamageService` has orchestration responsibility: find the target, apply a result, and publish events. This avoids a giant `Character.TakeDamage()` method that knows about registries, random numbers, UI, and pooling.

## 3. Domain: skills

Path: `Assets/_Project/Scripts/Domain/Skills/`

| File | Type | Responsibility |
|---|---|---|
| `SkillId.cs` | value object/enum | Stable id for a skill |
| `SkillContext.cs` | pure context | Owner character id, time, target snapshot, command interfaces |
| `ISkill.cs` | interface | `Initialize`, `Tick`, `Dispose` |
| `IActiveSkill.cs` | interface | `TryCast(SkillContext)` for requested actions |
| `IPassiveSkill.cs` | interface | Installs/removes modifiers and reacts to relevant events |
| `SkillCollection.cs` | pure class | Owns skill instances for one character and ticks them |
| `ChargeSkill.cs` | active skill | Boss charge state/cooldown; emits movement command |
| `EnragePassive.cs` | passive skill | Adds a movement/damage modifier in boss phase 2 |

Skills depend on interfaces such as `IMovementCommandSink`, `IProjectileCommandSink`, and `ICharacterQuery`. They must not use `Instantiate`, `FindObjectOfType`, or `Transform`.

## 4. Domain: controllers/brains

Path: `Assets/_Project/Scripts/Domain/Characters/Controllers/`

| File | Type | Responsibility |
|---|---|---|
| `ICharacterController.cs` | interface | `Tick(character, deltaTime)` |
| `PlayerController.cs` | pure class | Turns `IPlayerInput` into movement and active-skill requests |
| `ChaseEnemyController.cs` | pure class | Requests movement toward `IPlayerTarget` |
| `BossController.cs` | pure class | Selects phase, delegates basic chase, manages boss active/passive skills |
| `BossPhase.cs` | value object | Threshold and enabled skill ids for one phase |

The controller produces a movement command. It does not move a Unity Transform. An application system later applies the command to the View.

## 5. Data: authored definitions

Path: `Assets/_Project/Scripts/Data/Characters/`

| File | Unity type | Responsibility |
|---|---|---|
| `PlayerDefinitionAsset.cs` | ScriptableObject | Holds player core stats, player-specific config, active/passive skill definitions |
| `NormalEnemyDefinitionAsset.cs` | ScriptableObject | Holds core stats, prefab/pool key, spawn rules, chase settings |
| `BossDefinitionAsset.cs` | ScriptableObject | Holds core stats, prefab/pool key, phase definitions, boss skills |
| `CharacterPrefabKey.cs` | serializable value/config | Stable prefab/pool identifier rather than direct gameplay dependency |
| `SkillDefinition.cs` | serializable config DTO | Skill id, cooldown, tunable values |
| `BossPhaseDefinition.cs` | serializable config DTO | Health threshold plus enabled skill ids |
| `EnemySpawnRuleAsset.cs` | ScriptableObject | Spawn rate, caps, normal-enemy selection weights |

Definition assets use composition:

```text
BossDefinitionAsset
├── CharacterStatsConfig
├── BossPhaseDefinition[]
└── SkillDefinition[]
```

No `BossDefinitionAsset : EnemyDefinitionAsset` inheritance is needed.

## 6. Application: character lifecycle and systems

Path: `Assets/_Project/Scripts/Application/Characters/`

| File | Type | Responsibility |
|---|---|---|
| `ICharacterRegistry.cs` | interface | Lookup/register/unregister runtime characters by id |
| `CharacterRegistry.cs` | pure service | Stores active `Character` entities |
| `CharacterSystem.cs` | Zenject `ITickable` | Ticks all controllers/skills centrally and dispatches movement commands |
| `CharacterLifecycleService.cs` | service | Handles spawn/death/despawn ordering and publishes lifecycle events |
| `IPlayerTarget.cs` | small interface | Supplies player id, position snapshot, alive state |
| `PlayerTargetService.cs` | adapter service | Updates `IPlayerTarget` from Player view/entity |

`CharacterSystem` is where many enemies are updated in one loop. Do not let every enemy prefab own a gameplay `Update()` after this system is in place.

## 7. Application: spawning

Path: `Assets/_Project/Scripts/Application/Spawning/`

| File | Type | Responsibility |
|---|---|---|
| `ISpawnPointProvider.cs` | interface | Returns a valid spawn position |
| `RectangleSpawnPointProvider.cs` | pure/service | Builds evenly spaced camera-relative perimeter points and chooses one |
| `IEnemyFactory.cs` | interface | Spawns correct view/entity from an enemy definition |
| `EnemyFactory.cs` | service | Obtains a pooled prefab, creates domain character/controller, registers both |
| `EnemySpawnDirector.cs` | Zenject `ITickable` | Uses spawn rules, alive cap, time, and factory to spawn normal enemies |
| `BossSpawnDirector.cs` | service | Spawns boss based on explicit timeline/condition |
| `SpawnedEnemy.cs` | small binding object | Holds runtime character id and its pooled view instance |

`EnemySpawnDirector` may choose an enemy definition, but it must never call a concrete pool or instantiate a prefab. That responsibility belongs to `EnemyFactory`.

## 8. Presentation: Unity-facing files

Path: `Assets/_Project/Scripts/Presentation/`

| File | Type | Responsibility |
|---|---|---|
| `CharacterView.cs` | MonoBehaviour | Holds transform/sprite/animator; receives movement/render state from application layer |
| `PlayerView.cs` | MonoBehaviour | Inherits/composes CharacterView; forwards player movement/UI input to input adapter |
| `EnemyView.cs` | MonoBehaviour | Reports hit/collision and returns render state; no chase/damage calculation |
| `BossView.cs` | MonoBehaviour | Boss-specific animations/VFX hooks only |
| `CharacterCollisionReceiver.cs` | MonoBehaviour | Converts `OnTriggerEnter2D`/`OnCollisionEnter2D` to contact-hit commands |
| `JoystickInputAdapter.cs` | MonoBehaviour | Reads current virtual joystick and implements `IPlayerInput` |
| `CameraBoundsAdapter.cs` | MonoBehaviour | Supplies current camera rectangle to `ISpawnPointProvider` |
| `PlayerHealthPresenter.cs` | MonoBehaviour | Subscribes to damage/health signals and updates UI |

Views should be slim. For example, `EnemyView.OnTriggerEnter2D` reports a contact event to a service; it must not call `targetHp -= damage`.

## 9. Infrastructure: Zenject and pools

Path: `Assets/_Project/Scripts/Infrastructure/`

| File | Type | Responsibility |
|---|---|---|
| `GameSceneInstaller.cs` | MonoInstaller | Binds all interfaces/services/signals for the gameplay scene |
| `EnemyPoolInstaller.cs` | Installer | Binds Zenject MemoryPool for each normal enemy prefab |
| `BossPoolInstaller.cs` | Installer | Binds Zenject MemoryPool for boss prefab(s) |
| `SignalInstaller.cs` | Installer | Declares `CharacterDamaged`, `CharacterDied`, `CharacterSpawned`, etc. |
| `PooledEnemyView.cs` | MonoBehaviour/pool callback | Clears view references and collision state on despawn |

Keep Lean Pool only for Ground chunks during migration. For enemy/boss/bullet gameplay objects, use Zenject `MemoryPool` consistently. Do not make the same object managed by both Lean Pool and Zenject MemoryPool.

## Exact first vertical slice

Build this small set first. It gives a playable proof of the architecture without boss complexity.

```text
Domain/Characters/
  CharacterKind.cs
  StatType.cs
  CharacterStatsConfig.cs
  CharacterStats.cs
  Character.cs

Domain/Combat/
  DamageRequest.cs
  DamageResult.cs
  DamageCalculator.cs
  DamageService.cs

Application/Characters/
  ICharacterRegistry.cs
  CharacterRegistry.cs
  IPlayerTarget.cs

Application/Spawning/
  ISpawnPointProvider.cs
  RectangleSpawnPointProvider.cs

Presentation/
  CharacterView.cs
  EnemyView.cs
  CharacterCollisionReceiver.cs

Infrastructure/
  GameSceneInstaller.cs
  SignalInstaller.cs
```

Success condition: a pooled normal enemy appears at a rectangular camera-edge point, follows Player through `ChaseEnemyController`, contact damage passes through `DamageService`, Player HP changes, and the UI reacts to `CharacterDamagedEvent`.

Only after this works should the project add `BossController`, `ChargeSkill`, `EnragePassive`, boss phases, and boss pooling.

## Prohibited dependencies

Use this list as a review checklist.

```text
Domain files must not:
- call FindObjectOfType
- access static GameManager
- access Transform/GameObject/MonoBehaviour
- call Instantiate, Destroy, LeanPool, or MemoryPool
- update UI, audio, animation, or VFX

Presentation files must not:
- calculate final damage
- change CharacterStats directly from a collider callback
- choose spawn rules
- own gameplay cooldowns/phases

Application files must not:
- know SpriteRenderer/Animator implementation details
```
