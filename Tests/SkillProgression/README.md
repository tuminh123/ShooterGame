# Skill progression checks

Run from the repository root:

```powershell
./Tests/SkillProgression/Run.ps1
```

Requires the imported Unity project, its Unity Editor compiler (default 2022.3.62f3) and an installed .NET SDK/reference pack. `-UnityEditor` accepts another Editor directory. No package restore or downloads are required. Outputs go to ignored `Temp/SkillProgressionVerification`.

The checks compile production skill schedulers, level lookup, shooting strategies, saw/aura runtimes, stat calculations, target identities and spatial geometry. `TestEnvironment.cs` provides only host dependencies that require Unity native lifecycle, rendering or scene services. This folder lives outside `Assets` and never enters the game build.

Coverage includes Player/Enemy ownership, independent permanent/run levels, caps, duplicate grants, reset, source disable/release, per-blade hit throttling, aura entry/exit/overlap, sparse unordered shooting milestones, old-projectile snapshots, independent spiral state and reset during a stat callback.

These are logic checks, not Unity Play Mode or rendering tests.

The same runner also checks level-up choice generation (three distinct offers, only the next level, repeat appearances across rolls, single-level skills, five-star cap, stale offers), multi-level reward queues and nested gameplay pause tokens.
