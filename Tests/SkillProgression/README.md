# Skill progression checks

Optional manual runner from the repository root:

    ./Tests/SkillProgression/Run.ps1

The current refactor was compile-checked only; the harness was not executed.

The runner compiles production skill code plus host doubles outside Assets.
It covers complete level data, behavior unlocks, character/gameplay separation,
stat modifier replacement and retained UI/game-flow checks.
Old ShootingLevelTable and raw-action registration checks were removed because
those APIs no longer exist.

Requires an imported Unity project, Unity 2022.3.62f3 compiler and an installed
.NET SDK/reference pack. Outputs use ignored Temp/SkillProgressionVerification.

This does not verify Unity serialization, rendering, Inspector interaction,
pool activation callbacks or Play Mode. Test these in the Unity Editor.
