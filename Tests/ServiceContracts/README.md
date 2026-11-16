# Service contract verification

Production integration checks are in `Assets/_Project/Editor/Verification/ServiceContractVerification.cs` and run inside the actual Unity Editor/container.

- Edit Mode: **Shooter Game > Verification > Check Service Contracts**.
- Play Mode, after Home is ready: **Shooter Game > Verification > Run Service Smoke Checks**.
- Diagnostics: **Shooter Game > Verification > Report Missing Scripts**.

The checks reject interfaces declaring more than three ordinary methods, unstable facade getter results or directly bound leaf roles, concrete service bindings and duplicate/missing lifecycle registrations. They exercise character movement/search, projectile sweep/damage/release, skill grants/upgrades, maintained effects, stale handles, lethal damage, owner cleanup, nested pause, restart and Home. The Play Mode menu waits for prewarm to reach Home before starting checks.

Checks do not grant stage result rewards, write wallet transactions or verify rendered animation/UI interaction. Exit Play Mode after the checks.

The legacy `SkillProgression` and `EffectPlayback` standalone runners predate the current scheduler and reference removed timer/wave/feedback source files. Their moved contract paths are updated, but their host doubles still need a separate migration before those runners can be used against the current production code. They are not counted as passing checks for this refactor.

Aggregate facades are getter-only and exempt from the three-method limit. Only system facades and Zenject lifecycle interfaces are bound; the small roles are obtained through getters.
