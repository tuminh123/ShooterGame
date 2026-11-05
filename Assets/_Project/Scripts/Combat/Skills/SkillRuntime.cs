using System;

// One live skill instance. Its owning collection decides when to tick and dispose it.
public sealed class SkillRuntime : IDisposable, ITimerListener
{
    private readonly ISkill behavior;
    private readonly float cooldown;
    private readonly ITimerManager timers;
    private TimerHandle cooldownTimer;

    public SkillDataSO Definition { get; }
    public bool IsActive { get; private set; } = true;
    public float CooldownRemaining => timers.GetRemaining(cooldownTimer);

    internal SkillRuntime(SkillDataSO definition, ISkill behavior, ITimerManager timers, bool paused = false)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        cooldown = definition.Cooldown;
        this.timers = timers ?? throw new ArgumentNullException(nameof(timers));
        cooldownTimer = timers.Register(0f, this, paused: paused); // Skill mới bắt đầu ở trạng thái sẵn sàng.
    }

    internal void Tick()
    {
        if (!IsActive) return;

        if (CooldownRemaining > 0f) return;

        try
        {
            if (behavior.TryExecute() && IsActive)
                timers.Restart(cooldownTimer, cooldown);
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

    internal void SetPaused(bool paused) => timers.SetPaused(cooldownTimer, paused);

    // Job chỉ cập nhật cooldown. Bên sở hữu vẫn thử Execute và quyết định bắt đầu lượt hồi chiêu mới.
    public void OnTimer(TimerHandle handle, TimerResult result) { }

    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        timers.Cancel(cooldownTimer);
        cooldownTimer = default;
        if (behavior is IDisposable disposable)
            disposable.Dispose();
    }

    internal static void ValidateDeltaTime(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
    }
}
