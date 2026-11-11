# Stages, wave progression and character levels

`GameSceneInstaller.Stage Data` selects the stage asset for this scene. The supplied
`Assets/_Project/DataSO/Stages/Stage_01.asset` uses the existing desert map and
Default_Waves config; boss and reward lists are intentionally empty until configured.
Create another asset with Create → Shooter Game → Data → Stage, choose its stable
Stage ID, Map Key, Waves, Bosses and reward lists, then assign it to the installer.

`StageDataSO` contains only configuration. `StageManager` holds per-run state in
memory: current wave, activated boss entries, tracked bosses, deployment time and
reward-granted flag. No StageProgress class or mid-run save file is introduced.
GroundChunkManager.SetMap applies the stage Map Key through the existing map database.

Boss entries use a 1-based `After Wave`, a CharacterDataSO marked `Is Boss`, and
`Count`. After that wave spawns its full normal-enemy quota, StageManager asks the
spawner to create every configured boss at the existing perimeter. Bosses use the
shared CharacterFactory/EnemyController; this does not add special boss AI. Register
the boss CharacterType level config in LevelDatabase, and its definition in the
character database for prewarming. Normal wave enemy lists reject Is Boss entries.

`Default_Waves.asset` contains ordered wave templates and one global `Advance Condition` above the list:

- Every wave has `Enemy Count` (serialized as `spawnCount`): spawn one enemy per `Spawn Interval` until that many enemies have spawned successfully, then stop. There is no limit on simultaneously living enemies.
- `AfterDuration`: once the full normal-enemy count AND configured bosses have spawned, wait `Delay After Spawn` gameplay seconds, then start the next wave even if enemies/bosses remain. The delay is not measured from the beginning of the wave.
- `AfterEnemiesCleared`: after spawning the normal-enemy count and bosses, wait until no spawned enemies/bosses remain alive, then start the next wave without a delay. Death animations do not count as living enemies.

The selected condition applies to every wave. The custom inspector always shows Enemy Count and Spawn Interval, and shows Delay After Spawn only for time mode. The old serialized duration field migrates to delayAfterSpawn via FormerlySerializedAs. Pause freezes progression. New runs reset wave quotas, boss entries and stage rewards.

Both modes run the list once. After deploying the last wave and its bosses, clear every remaining enemy/boss from all waves to win; no inter-wave delay is required after the final wave. EnemySpawnManager emits WaveSpawnCompleted, while StageManager decides advancement and emits Cleared. GameManager.WinRun pauses gameplay, settles rewards, waits for Player Victory, then opens the result popup. Player loss settles defeat rewards, waits for Defeat, then opens its popup. Missing visual/animation states retain the existing immediate-result fallback.

Winning grants Clear Rewards plus Victory Rewards. Losing grants Defeat Rewards.
CurrencyService.AddBatch validates all resulting balances before mutation and saves
one wallet snapshot. StageManager grants once per run, independently of popup visibility;
IO failures retry the pending snapshot, not the transaction. Loot gold is already saved
when picked up and is not credited again. ResultController displays collected gold plus
Stage gold rewards, and the Stage diamond reward, using the existing currency icons.
Stage_01 starts with zero configured rewards rather than inventing economy values.

Enemy level at spawn is `wave index + 1`. With six templates, levels run from 1 through 6; there is no seventh round. Each enemy's `CharacterLevelConfigSO` supplies `Bonus Per Level` and `Max Level`; bonus is `(min(enemy level, max level) - 1) * Bonus Per Level`. Existing enemies retain the stats they received at spawn.

`CharacterLevelConfigSO` replaces `PermanentLevelConfigSO`, retaining its script GUID and existing player asset/save behavior. Player levels still come from the save; enemy levels come from waves and are not saved. Upgrade cost fields are only used by player progression. `LevelDatabase.asset` registers both player and enemy configs by CharacterType.

The Foraging Maggot config is `Assets/_Project/DataSO/Levels/ForagingMaggot_Levels.asset`: +20 max HP per level, capped at level 100. This value was migrated from its existing growth bonus; its base stats and skills are unchanged. The player's existing `BlessedGladiator_PermanentLevels.asset` remains at its original path with its original values.

The time-based `Growth Interval` / `Growth Per Step` fields and `EnemySpawnProgress` are removed. `WaveProgress` now only counts normal-enemy spawns for the wave supplied by StageManager. EnemySpawnManager does not own stage completion or automatically advance waves.
