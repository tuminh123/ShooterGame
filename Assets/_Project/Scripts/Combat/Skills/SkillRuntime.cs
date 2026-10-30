using System;
// One instance per attached skill; shared assets/strategies never own its mutable state.
public sealed class SkillRuntime : IDisposable
{
    public SkillDataSO Data { get; }
    public ISkill Strategy { get; }
    public SkillContext Context { get; }
    public float CooldownRemaining { get; internal set; }
    public ShootingRuntime Shooting { get; } = new ShootingRuntime();
    public bool IsAttached { get; private set; } = true;
    internal float RetryRemaining;
    internal bool PassiveApplied;
    internal IDisposable Lifetime;
    internal SkillRuntime(SkillDataSO data, ISkill strategy, SkillContext context)
    { Data = data; Strategy = strategy; Context = context; }
    public void Dispose()
    {
        if (!IsAttached) return;
        IsAttached = false;
        Lifetime?.Dispose();
        Lifetime = null;
    }
}
