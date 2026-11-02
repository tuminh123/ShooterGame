using System;

// One live skill instance. Its owning collection decides when to tick and dispose it.
public sealed class SkillRuntime : IDisposable
{
    private readonly ISkill behavior;
    private readonly float cooldown;

    public SkillDataSO Definition { get; }
    public bool IsActive { get; private set; } = true;
    public float CooldownRemaining { get; private set; }

    internal SkillRuntime(SkillDataSO definition, ISkill behavior)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        cooldown = definition.Cooldown;
    }

    internal void Tick(float deltaTime)
    {
        if (!IsActive) return;

        CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaTime);
        if (CooldownRemaining > 0f) return;

        try
        {
            if (behavior.TryExecute() && IsActive)
                CooldownRemaining = cooldown;
        }
        catch (Exception execution)
        {
            try { Dispose(); }
            catch (Exception cleanup)
            {
                throw new AggregateException("Skill execution and cleanup failed.", execution, cleanup);
            }
            throw;
        }
    }

    internal bool UsesBehavior(ISkill instance)
    {
        return ReferenceEquals(behavior, instance);
    }

    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        if (behavior is IDisposable disposable)
            disposable.Dispose();
    }

    internal static void ValidateDeltaTime(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
    }
}
