# Wave progression and character levels

`Default_Waves.asset` contains ordered wave templates and one global `Advance Condition` above the list:

- `AfterDuration`: spawn at `Spawn Interval`, up to `Max Alive Enemies`, and advance after `Duration` gameplay seconds even if enemies remain. `Spawn Count` is unused.
- `AfterEnemiesCleared`: spawn exactly `Spawn Count` enemies successfully, respecting the same interval and alive cap, then wait until no spawned enemies remain alive. This includes survivors of previous timed waves. Death animations do not count as living enemies. `Duration` is unused.

The selected condition applies to every wave. The custom inspector shows Duration only for time mode and Spawn Count only for clear mode. Pause freezes progression. New runs reset the wave index, completed-wave count and spawn quota.

After the final template completes, repeat that final template in either mode. In time mode, its enemies keep spawning and each completed Duration raises the level. In clear mode, finish its spawn quota and wait for all surviving enemies to die before starting a fresh batch with the same final quota. A one-template list repeats that template. Completion does not automatically end the run. Long time steps preserve elapsed time across template boundaries and repeated final-wave durations.

Enemy level at spawn is `total completed waves + 1`, independent of the template index. With six templates, the seventh round still uses template 6 and enemy level 7, then template 6 at level 8 and so on. Each enemy's `CharacterLevelConfigSO` supplies `Bonus Per Level` and `Max Level`; bonus is `(min(enemy level, max level) - 1) * Bonus Per Level`. Existing enemies retain the stats they received at spawn.

`CharacterLevelConfigSO` replaces `PermanentLevelConfigSO`, retaining its script GUID and existing player asset/save behavior. Player levels still come from the save; enemy levels come from waves and are not saved. Upgrade cost fields are only used by player progression. `LevelDatabase.asset` registers both player and enemy configs by CharacterType.

The Foraging Maggot config is `Assets/_Project/DataSO/Levels/ForagingMaggot_Levels.asset`: +20 max HP per level, capped at level 100. This value was migrated from its existing growth bonus; its base stats and skills are unchanged. The player's existing `BlessedGladiator_PermanentLevels.asset` remains at its original path with its original values.

The time-based `Growth Interval` / `Growth Per Step` fields and `EnemySpawnProgress` are removed. `WaveProgress` owns per-run wave timing, template index, completed-wave count and spawn quota.
