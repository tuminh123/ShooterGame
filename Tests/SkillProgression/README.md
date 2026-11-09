# Skill progression checks

Optional manual runner from the repository root:

    ./Tests/SkillProgression/Run.ps1

The harness includes production effect configuration, manager, runtime and pool code,
with host doubles for Unity, timers and memory pools.

The runner compiles production skill code plus host doubles outside Assets.
It covers complete level data, behavior unlocks, character/gameplay separation,
stat modifier replacement and retained UI/game-flow checks.
Effect checks cover additive slows, timed expiry, maintained aura entry/exit,
pause, upgrades, cleanup and stale handles after runtime pool reuse.
Enemy feedback checks cover repeated/lethal hit flashes, sprite frame playback,
death completion, pause, cancellation, pool cleanup and stale spawn callbacks.
Timer/tween host doubles check lifecycle, not Unity rendering or imported sprite references.
Old ShootingLevelTable and raw-action registration checks were removed because
those APIs no longer exist.

Requires an imported Unity project, Unity 2022.3.62f3 compiler and an installed
.NET SDK/reference pack. Outputs use ignored Temp/SkillProgressionVerification.

This does not verify Unity serialization, rendering, Inspector interaction,
pool activation callbacks or Play Mode. Test these in the Unity Editor.
