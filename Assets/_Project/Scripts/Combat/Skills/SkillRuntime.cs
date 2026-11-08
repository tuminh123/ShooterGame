using System;

// One live skill instance. Its owning collection decides when to tick and dispose it.
public sealed class SkillRuntime : IDisposable, ITimerListener
{
    private readonly ISkill behavior;
    private readonly float cooldown;
    private readonly ITimerManager timers;
    private TimerHandle cooldownTimer;
    private readonly CharacterView owner;
    private readonly CharacterRuntimeData identity;
    private readonly ulong generation;
    private IDisposable statLease;

    public SkillDataSO Definition { get; }
    public bool IsActive { get; private set; } = true;
    public float CooldownRemaining => timers.GetRemaining(cooldownTimer);
    public int RunLevel { get; private set; } = 1;
    // Hai cấp độc lập: không lấy Definition.Level làm RunLevel, không cộng hai cấp thành một.
    public int PermanentLevel => Definition.Level;
    private bool changingLevel;
    private bool resettingLevel;
    private int levelRevision;

    internal SkillRuntime(SkillDataSO definition, ISkill behavior, ITimerManager timers, bool paused = false,
        CharacterView owner = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        cooldown = definition.Cooldown;
        this.timers = timers ?? throw new ArgumentNullException(nameof(timers));
        cooldownTimer = timers.Register(0f, this, paused: paused); // Skill mới bắt đầu ở trạng thái sẵn sàng.
        try { (behavior as IRunLevelSkill)?.SetRunLevel(1); }
        catch { timers.Cancel(cooldownTimer); throw; }
        this.owner = owner;
        if (owner == null) return; // Innate bonuses are already included by InitializeSpawn.
        identity = owner.RuntimeData;
        generation = identity.Generation;
        owner.Died += OnOwnerEnded;
        owner.Released += OnOwnerEnded;
        owner.Deactivated += OnOwnerDeactivated;
        try
        {
            IDisposable lease = identity.AddTemporaryBonus(definition.StatBonus);
            // A stats subscriber can synchronously despawn the owner while applying the modifier.
            if (IsActive) statLease = lease;
            else lease.Dispose();
        }
        catch
        {
            DetachOwner();
            timers.Cancel(cooldownTimer);
            IsActive = false;
            throw;
        }
    }

    internal void Tick()
    {
        if (!IsActive) return;
        if (identity != null)
        {
            if (owner == null || !ReferenceEquals(owner.RuntimeData, identity) ||
                identity.Generation != generation || !identity.IsAlive)
            { Dispose(); return; }
            SetPaused(!owner.isActiveAndEnabled);
            if (!owner.isActiveAndEnabled) return;
        }

        try
        {
            if (behavior is IContinuousSkill continuous) continuous.Tick(timers.DeltaTime);
            if (!IsActive || CooldownRemaining > 0f) return;
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

    internal bool BelongsTo(CharacterView character) => ReferenceEquals(owner, character);

    public bool TryUpgrade()
    {
        if (!IsActive || changingLevel || resettingLevel || RunLevel >= Definition.MaxLevel || !(behavior is IRunLevelSkill levelled))
            return false;
        changingLevel = true;
        int revision = levelRevision;
        int nextLevel = RunLevel + 1;
        try
        {
            levelled.SetRunLevel(nextLevel);
            if (!IsActive || revision != levelRevision) return false;
            RunLevel = nextLevel;
            levelRevision++;
            return true;
        }
        finally { changingLevel = false; }
    }

    internal void ResetRunLevel()
    {
        if (!IsActive || resettingLevel || !(behavior is IRunLevelSkill levelled)) return;
        resettingLevel = true;
        levelRevision++;
        try { levelled.SetRunLevel(1); RunLevel = 1; }
        finally { resettingLevel = false; }
    }

    private void OnOwnerEnded(CharacterView character) => Dispose();
    private void OnOwnerDeactivated() => SetPaused(true);

    private void DetachOwner()
    {
        if (ReferenceEquals(owner, null)) return;
        owner.Died -= OnOwnerEnded;
        owner.Released -= OnOwnerEnded;
        owner.Deactivated -= OnOwnerDeactivated;
    }

    internal void SetPaused(bool paused)
    {
        timers.SetPaused(cooldownTimer, paused);
        (behavior as IContinuousSkill)?.SetPaused(paused);
    }

    // Job chỉ cập nhật cooldown. Bên sở hữu vẫn thử Execute và quyết định bắt đầu lượt hồi chiêu mới.
    public void OnTimer(TimerHandle handle, TimerResult result) { }

    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        DetachOwner();
        try
        {
            timers.Cancel(cooldownTimer);
            cooldownTimer = default;
            if (behavior is IDisposable disposable) disposable.Dispose();
        }
        finally
        {
            IDisposable previous = statLease;
            statLease = null;
            previous?.Dispose();
        }
    }

    internal static void ValidateDeltaTime(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
    }
}
