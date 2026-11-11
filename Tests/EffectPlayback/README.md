# Effect playback verification

Run `powershell -ExecutionPolicy Bypass -File Tests/EffectPlayback/Run.ps1` from the project root.

Compiles the production IEffectVisual, EffectView, AnimationEffectVisual, ParticleEffectVisual and ObjectEffectVisual against small test doubles outside Assets. Checks long animation completion, replay/cancel cleanup, particle child completion, timed object activation/cancellation, shell release and stale callback rejection. Native Unity rendering, activation callbacks and PlayableGraph evaluation still require a Play Mode check in Unity.
