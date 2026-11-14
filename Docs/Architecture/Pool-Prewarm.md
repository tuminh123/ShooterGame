# Pool preparation before gameplay

`GameSceneInstaller.poolPrewarmSettings` points to
`Assets/_Project/DataSO/Databases/PoolPrewarmSettings.asset`.

Initialization order is SaveManager (-200), PoolPrewarmService (-150),
PlayerManager (-100), then the run session (-50). A preparation failure throws
before the initial player spawn. No loading scene or scene persistence is added.

The profile prepares shared character/bullet/effect/pickup shells, visual prefabs
from CharacterDatabase and CombatAssets, CharacterRuntimeData, the registered
slow/periodic-damage effect runtimes, and movement/projectile/spatial buffers. Cooldown and effect duration live directly on their managed runtimes; no timer buffer is allocated.
Additional Prefabs can include future VFX and world objects. Counts for a shared
prefab are added together. Zero disables preparation for that category.

GameObject preparation holds all requested clones under the inactive Preparing
holder until injection finishes, then returns them. It does not initialize a
character, activate gameplay, play VFX, or register live projectile lifetimes.
LeanPool's direct PreloadOneMore is deliberately not used: it creates an active
prefab at the scene root before deactivation, which can call OnEnable too early.

CharacterFactory rents CharacterRuntimeData for the normal player/enemy spawn
paths. CharacterView returns it after skills, effects, controller and release
callbacks are cleaned. HP, shield, bonuses and Changed subscribers are reset.
Generation is checked by cached targets, skill owners, effects, spatial entries
and modifier leases so old references cannot act on a reused runtime.

`PoolPrewarmService.Prewarm()` is idempotent after success and exposes IsReady and
PreparedObjectCount. These are integration points for a later loading flow.
Pools still grow when usage exceeds prepared counts; the profile is a starting
budget, not a spawn cap or a zero-allocation guarantee. Controllers, skill
behaviors/closures and some per-spawn collections are still initialized during
spawn. Animator/UI first activation and rendering/shader warmup are separate from
inactive object preparation. Profile startup time, memory and peak counts on the
target device before increasing counts substantially.

Pool lifetime is the gameplay scene. Returning to another scene releases these
pools; a future cross-scene preparation flow will need a persistent container or
an explicit transfer of ownership.
