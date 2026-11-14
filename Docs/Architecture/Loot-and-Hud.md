# Enemy drops and run HUD

## Gameplay

- `CharacterManager.CharacterDied` notifies `LootManager` before the enemy shell is returned to its pool. Despawning or hiding an enemy does not grant loot.
- `LootDatabaseSO` contains entries keyed by `PickupType`. The sample Experience entry has amount 1 and Drop Chance 1, so every enemy drops the same EXP. Gold has amount 1 and Drop Chance 0.2, adding a gold pile on 20% of deaths. Enemy type/level does not change these values yet. Entries can be disabled with chance 0 or omitted.
- `LootPickup.prefab` is a common pooled SpriteRenderer shell. `PickupView` stores reward type and amount; the loot manager assigns its sprite before activation.
- `LootManager.LateTick` runs after MovementManager, checks distance to the live Player, removes the pickup and returns its shell before granting its reward. There are no pickup colliders, per-pickup Update methods or automatic expiration. No attraction movement is implemented: walk within `pickupRadius` (default 0.25 world units) to collect.
- Experience goes through `LevelManager.AddRunExperience`, preserving the existing progression rules. Gold is sent to `RunSessionManager.AddGold`; it counts only collected rewards in this run and is not written to the permanent player save. LootManager has no UI references, gold balance or clock.
- `Core/Run/RunSessionManager` owns gold and the gameplay clock independently of the loot system. Zenject calls its ITickable.Tick; a double accumulator preserves fractional gameplay seconds and publishes TimeChanged only when the whole-second value changes. It stops advancing while paused or the Player is dead/inactive. Other reward sources can also call AddGold.
- `LevelManager.StartNewRun()` tells LootManager to clear remaining pickups and RunSessionManager to reset gold and the clock; the existing level service resets EXP. Scene disposal removes subscriptions and returns remaining pickups before PoolManager disposal.

## Configuration

`Assets/_Project/DataSO/Databases/LootDatabase.asset` is assigned in `GameSceneInstaller > Gameplay Databases > Loot Database`, alongside the Character/Equipment databases. The old nested LootManager.Settings and scene settings block were removed. The database script is `Combat/Loot/Data/LootDatabaseSO.cs`.

Edit each entry's Amount, Drop Chance and Sprite in that asset. Pickup Prefab and Pickup Radius (0.25) remain shared fields in the database. There is no Visual Width setting or size normalization: the common pickup prefab and each spawned clone use scale (1,1,1). Configure the desired size of each sprite through its texture Import Settings / Pixels Per Unit. The manager builds its type lookup once and keeps live pickups in runtime collections; no runtime state is stored on the asset.

Experience uses a single blue faceted gem (`ExperienceGem.png`); the original crystal-cluster sprite remains available as an unused alternative. Pickup radius and reward amounts are independent of sprite size.

## UI

- Only the DynamicUI prefab asset is deleted. The scene keeps a standalone DynamicUI hierarchy with Experience Bar, Gold Counter, Run Time and Run Level, wired to UIManager. The UIManager hierarchy binding is enabled. Deleting a prefab asset must not remove this unpacked scene hierarchy.
- `UI/Views/BarView.cs` and `TextView.cs` remain reusable presentation components. They only set displayed value/text, color and visibility; neither owns gameplay state.
- Character health/shield bars reuse the Parchment RedBar and BlueBar prefabs. Source UI package prefabs were not changed.
- `Character.prefab` contains `CharacterVitals.prefab`, a World Space Canvas under the character shell. `UI/Character/CharacterUI.cs` is attached to the character root so it still receives OnEnable while that child Canvas starts disabled. It shows bars only for Player or `CharacterView.IsBoss`. Ordinary enemies keep it disabled. Boss still belongs to team Enemy. Configure Is Boss on its CharacterData asset.
- Set the vitals group's position and scale manually in the prefab. CharacterUI does not recalculate position from sprite bounds, restore an offset, or convert to screen coordinates. The Canvas follows its character parent naturally. Existing prefab transform/layout overrides are preserved.
- `CharacterUI` determines Player/boss visibility on each enable and subscribes to the current runtime, visual-visibility notifications and deactivation. It updates the two generic BarViews, hides zero shield and detaches old runtime subscriptions before pool reuse. `CharacterView` only exposes spawn metadata and visual lifecycle events; it has no bar references or UI update methods. The child Canvas stays with the pooled character shell.
- RunSessionManager owns gameplay gold/time and LevelManager owns EXP; UIManager reads their events for the scene counters.

Generated art uses transparent PNGs with Point filtering. Original generation prompts and output paths are recorded in [Pickup-Art-Prompts.md](../Art/Pickup-Art-Prompts.md).

No Unity play test or build was run; validation for this change is a static review of source and serialized references.
