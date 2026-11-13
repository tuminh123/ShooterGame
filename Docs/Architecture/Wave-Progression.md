# Stage spawn architecture

## Stage: one direct asset list

StageDataSO stores List<ScriptableObject> spawnData. Every assigned asset must
implement IStageSpawnData. StageDataEditor rejects other assets and StageDataSO
validates them at runtime. There is no entry wrapper, Stage-owned wave list,
Stage spawn delay, Stage spawn duration, Timing enum or shared context object.

Stage_01 directly references:
- Default_Waves.asset: WaveConfigSO, the complete ordinary-enemy wave sequence.
- Stage_01_Formation.asset: EnemyPatternSO, an independent pattern deployment.

The map, stable Stage ID, first-clear flags and reward values remain unchanged.

## Registry and construction

IStageSpawnData supplies SystemType, CreateSystem(DiContainer) and Validate.
Each asset inherits ScriptableObject directly and implements that interface.

StageManager walks the flat list once at construction and creates one system
per configured Type in Dictionary<Type, IStageSpawnSystem>. Data factories use
the existing Zenject container's Instantiate<T>() to supply constructor dependencies.
No StageSpawnContext, Initialize(context), reflection scan, type switch or
per-system binding in GameSceneInstaller is required.

WaveSpawnSystem and BossSpawnSystem receive EnemySpawnManager directly.
FormationSpawnSystem receives EnemySpawnManager, PlayerManager and ISpatialManager.

Stage starts each configured asset once, on the first gameplay tick after the
player exists. It ticks the systems and waits for their DeploymentComplete and
AliveCount statuses. Each system owns its detailed spawn logic. No wave index,
ordinary quota or inter-wave delay is held by StageManager.

## Separate contracts and one Stage clock

- IStageSpawnSystem: Validate, Execute and Stop only.
- IStageSpawnStatus: DeploymentComplete and AliveCount; required for spawn systems.
- IUpdateStage: Tick(stageTime, deltaTime); implemented only when updates are needed.
- ILateUpdateStage: LateTick; currently implemented only by FormationSpawnSystem.

StageManager builds separate interface lists once when registering each system.
The registry remains Dictionary<Type, IStageSpawnSystem>; data factories and the
direct asset list do not change. There are no empty update methods on systems
that do not need that update phase.

StageTime resets to zero at BeginRun and advances only on active gameplay ticks
after the player is ready. Every system receives the same StageTime and deltaTime.
It freezes with scaled gameplay pause and stops at the end of the run.
Spawn deadlines use StageTime; group movement integrates the Stage deltaTime.
Individual movement and knockback use the existing enemy controller/gameplay timers.

## WaveConfigSO retains the complete sequence

WaveConfigSO owns Advance Condition and its ordered Wave array. Every wave has
spawnCount, spawnInterval, enemies and delayAfterSpawn.

WaveSpawnSystem snapshots the full sequence per Execute and manages its own wave
index, successful-spawn quota, interval, enemy level and absolute Stage deadlines:

- Enemy level is the 1-based index inside that wave sequence.
- AfterDuration waits delayAfterSpawn after the quota is deployed, then advances
  even if ordinary enemies remain alive.
- AfterEnemiesCleared waits for that sequence's ordinary enemies, including its
  prior-wave survivors, to die before advancing.
- The final wave stops spawning after its quota. Stage still waits for all
  registered systems' living actors to clear before victory.
- The first spawn is scheduled at Stage time 0. Subsequent births and timed
  wave transitions retain their scheduled deadlines, including across a slow frame.
- AfterEnemiesCleared starts the next wave when clearing is observed; its exact
  start time therefore depends on the player's kills.

The wave roster belongs to this sequence. Pattern/boss actors do not consume its
quota or control its internal wave transition. Multiple executions of the same
sequence retain independent progress.

Default_Waves.asset again contains its Wave array: currently one wave of 50
Maggots, interval 0.5 seconds, with the existing 20-second inter-wave delay value.
A single final wave does not use an inter-wave delay.

## Patterns and bosses

EnemyPatternSO routes Formation, SequentialPath and Area to FormationSpawnSystem.
Execute registers a pending deployment. Each configured pattern starts when
StageTime reaches its delay; Stage does not wrap it in an extra schedule.
The runtime and route are created then, using the current player/camera position.
Pending patterns keep DeploymentComplete false even while AliveCount is zero.

- Formation creates every geometry slot at Path A plus shape offsets.
- SequentialPath creates one member at A, then the system schedules subsequent
  births at Spawn Interval.
- Area creates perimeter-only closed geometry around the current player, follows
  that center and shrinks over Shrink Duration. Path is hidden and unused for Area.

EnemyFormationRuntime is a group entity implementing IStageSpawnEntity. It receives
actors through Add/Seal, owns their identities/lifetimes and computes targets for
attached slots, without creating or scheduling enemies itself. Formation uses one
group path progress and speed; SequentialPath keeps per-birth progress without
geometry slots. Area uses player center and shrinkDuration instead of path speed.

Only a touching member receives knockback; there is no contact stun or stun field.
CharacterView lets ready innate skills resolve first, preserving damage, on-hit
effects and cooldown. EnemyController uses the same StartKnockback/ApplyKnockback
mechanism for ordinary enemies and pattern members, with pattern recoil values
supplied at JoinFormation. On first contact, the group hands a path member its
remaining route, passes and group speed. Its controller then completes that route
independently, without returning to its slot or chasing the player. The group still
owns its lifetime. Continuous overlap does not restart knockback repeatedly.
Area members remain linked to the shrinking perimeter after their own recoil.

BossSpawnDataSO owns boss, count, enemyLevel and delay. BossSpawnSystem waits for
StageTime to reach delay, then creates that deployment using its configured level,
without reading a shared WaveNumber/context. Pending boss deployments also prevent
premature Stage victory. Existing assets default to delay 0; existing GUIDs are kept.

All systems create actors through EnemySpawnManager and CharacterFactory/Pool.
Boss presentation uses CharacterDataSO.IsBoss; existing shared AI is unchanged.

## Lifetime, rewards and cleanup

StageSpawnRoster owns IStageSpawnEntity instances. An ordinary enemy or boss uses
a private CharacterEntity adapter that guards its pool identity/generation. A
pattern registers its entire EnemyFormationRuntime as one entity before creating
members. Roster sums entity AliveCount values and calls Dispose for cleanup; it
does not also register the group's individual members. The group guards member
identities and cleans attached and detached members, including death animations
still awaiting release. Completed entities are pruned when reading roster counts.

DeploymentComplete means that the system has finished its spawn program; AliveCount
is separate. Stage wins only when all systems finished and no living actors remain.
Pattern members may satisfy this by naturally reaching the route end or Area expiry.

Real kills still raise CharacterDied and use LootManager EXP/gold rules. Natural
despawn returns actors without kill rewards. Identity/generation guards prevent
stale owners from affecting a reused pooled shell.

Stop/reset/dispose stops all systems and cancels pending births. Cleanup continues
across release failures and reports aggregated exceptions. The late-update hook
is removed before factory/pool services are disposed.

The existing first-clear/victory/defeat economy is unchanged. Stage_01 rewards are
100 first-clear Gold plus 50 victory Gold, or 10 defeat Gold, excluding picked-up
loot already saved on collection.

No test, build, forced compilation or Editor Play action was run for this revision.
