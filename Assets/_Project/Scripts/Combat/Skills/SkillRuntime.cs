using System;
using System.Collections.Generic;

// Một skill của một owner: cấp chung, nhiều hành vi và cooldown riêng cho từng hành vi.
public sealed class SkillRuntime : IDisposable
{
    #region Behaviour Scheduling

    // Timer chỉ thuộc runtime; config và hành vi trên SO không giữ timer.
    private sealed class Behaviour : IDisposable, ITimerListener
    {
        internal readonly ISkill Action; // Bản sao riêng, không chạy template.
        private readonly ITimerManager timers; // TimerManager của scene.
        private TimerHandle timer; // Cooldown đang chạy, được giữ khi đổi data.
        private float cooldown; // Nhịp mới áp dụng cho lần cast thành công tiếp theo.
        private bool disposed; // Chặn tick và dispose lặp.
        private bool paused; // Ngừng cả tick liên tục lẫn cast.
        internal float Remaining => timers.GetRemaining(timer);

        // Giao ownership action ngay khi timer đăng ký thành công.
        internal Behaviour(ISkill action, ITimerManager timers, bool paused)
        {
            Action = action;
            this.timers = timers;
            this.paused = paused;
            timer = timers.Register(0f, this, paused: paused);
        }

        // Đổi config không khởi động lại cooldown đang chạy.
        internal void ApplyData(SkillBehaviourData data)
        {
            Action.ApplyData(data);
            cooldown = data.Cooldown;
        }

        // Hành vi liên tục vẫn cập nhật trong lúc chờ lượt cast tiếp theo.
        internal void Tick()
        {
            if (disposed || paused) return;
            Action.Tick(timers.DeltaTime);
            if (!disposed && Remaining <= 0f && Action.TryExecute() && !disposed)
                timers.Restart(timer, cooldown);
        }

        // Đồng bộ timer và visual/state của hành vi.
        internal void SetPaused(bool value)
        {
            if (disposed) return;
            paused = value;
            timers.SetPaused(timer, value);
            Action.SetPaused(value);
        }

        // Timer không tự thực thi gameplay; SkillManager gọi Tick trên main thread.
        public void OnTimer(TimerHandle handle, TimerResult result) { }

        // Dọn action kể cả khi timer cancellation lỗi.
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { timers.Cancel(timer); }
            finally { Action.Dispose(); }
        }
    }

    #endregion

    #region Ownership And State

    private readonly List<Behaviour> behaviours = new List<Behaviour>(); // Chỉ chứa hành vi đã được mở ở cấp hiện tại.
    private readonly CharacterSkills context; // Context đúng lượt spawn.
    private readonly CharacterView owner; // Owner của toàn bộ hành vi.
    private readonly CharacterRuntimeData identity; // Không dùng runtime cũ trên spawn mới.
    private readonly ulong generation; // Phân biệt lượt pool tái sử dụng cùng runtime data.
    private readonly SkillFactory factory; // Tạo hành vi mới khi cấp mới mở thêm loại.
    private bool changingLevel; // Chặn nâng cấp lồng nhau từ callback.
    private bool paused; // Trạng thái truyền cho hành vi mở mới.
    private int revision; // Thay đổi danh sách giữa callback sẽ kết thúc vòng tick cũ.

    public SkillDataSO Definition { get; }
    public bool IsActive { get; private set; } = true;
    public int RunLevel { get; private set; }
    public int PermanentLevel => Definition.Level;
    public SkillSource Source => Definition.Source;
    public int BehaviourCount => behaviours.Count;
    public float CooldownRemaining
    {
        get
        {
            float remaining = float.MaxValue; // Giá trị tổng quan cho debug, không gộp timer.
            foreach (Behaviour behaviour in behaviours) remaining = Math.Min(remaining, behaviour.Remaining);
            return remaining == float.MaxValue ? 0f : remaining;
        }
    }

    #endregion

    #region Initialization

    // Runtime đăng ký owner trước khi ApplyData có thể phát callback stats.
    internal SkillRuntime(SkillDataSO definition, CharacterSkills context, SkillFactory factory)
    {
        Definition = definition;
        this.context = context;
        this.factory = factory;
        owner = context.Owner;
        identity = context.Identity;
        generation = identity.Generation;
        paused = !owner.isActiveAndEnabled;
        RunLevel = definition.Source == SkillSource.Gameplay ? 1 : definition.Level;
        owner.Died += OnOwnerEnded;
        owner.Released += OnOwnerEnded;
        owner.Deactivated += OnOwnerDeactivated;
        try { ApplyLevel(RunLevel); }
        catch (Exception creation)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("Skill initialization and cleanup failed.", creation, cleanup); }
            throw;
        }
    }

    #endregion

    #region Level Configuration

    // Tìm theo enum loại; một skill có tối đa một hành vi của mỗi loại.
    private Behaviour FindBehaviour(SkillBehaviourType type)
    {
        foreach (Behaviour behaviour in behaviours) if (behaviour.Action.Type == type) return behaviour;
        return null;
    }

    // Cập nhật hành vi cũ, mở hành vi mới, thu hồi loại không còn trong cấu hình cấp này.
    private void ApplyLevel(int level)
    {
        revision++;
        IReadOnlyList<SkillBehaviourData> data = Definition.GetLevel(level).Behaviours;
        var required = new HashSet<SkillBehaviourType>(); // Chỉ cấp phát khi đổi cấp, không nằm trong Tick.
        foreach (SkillBehaviourData item in data)
        {
            if (!IsActive) return;
            required.Add(item.Type);
            Behaviour behaviour = FindBehaviour(item.Type);
            if (behaviour == null)
            {
                ISkill action = factory.CreateBehaviour(Definition, item.Type, context);
                try { behaviour = new Behaviour(action, owner.Timers, paused); }
                catch { action.Dispose(); throw; }
                behaviours.Add(behaviour); // Nhận ownership trước callback ApplyData để reset/dispose có thể dọn ngay.
            }
            behaviour.ApplyData(item);
            if (!IsActive) return;
            behaviour.SetPaused(paused);
        }
        foreach (Behaviour behaviour in behaviours.ToArray())
        {
            if (!IsActive) return;
            if (required.Contains(behaviour.Action.Type)) continue;
            behaviours.Remove(behaviour);
            behaviour.Dispose();
        }
    }

    // Chỉ gameplay nâng trong trận; cấp mới dùng toàn bộ cấu hình từ SO.
    public bool TryUpgrade()
    {
        if (!IsActive || changingLevel || Source != SkillSource.Gameplay || RunLevel >= Definition.MaxLevel) return false;
        Definition.Validate();
        changingLevel = true;
        try
        {
            ApplyLevel(RunLevel + 1);
            if (!IsActive) return false;
            RunLevel++;
            return true;
        }
        catch (Exception upgrade)
        {
            if (IsActive)
            {
                try { ApplyLevel(RunLevel); }
                catch (Exception rollback)
                {
                    try { Dispose(); }
                    catch (Exception cleanup) { throw new AggregateException("Upgrade, rollback and cleanup failed.", upgrade, rollback, cleanup); }
                    throw new AggregateException("Upgrade rollback failed; skill removed.", upgrade, rollback);
                }
            }
            throw;
        }
        finally { changingLevel = false; }
    }

    #endregion

    #region Execution And Owner Lifecycle

    // Một vòng tick chung do SkillManager điều phối; callback đổi cấp sẽ kết thúc vòng duyệt cũ.
    internal void Tick()
    {
        if (!IsActive) return;
        if (!context.IsValid || owner == null || !ReferenceEquals(owner.RuntimeData, identity) ||
            identity.Generation != generation || !identity.IsAlive)
        { Dispose(); return; }
        SetPaused(!owner.isActiveAndEnabled);
        if (paused) return;
        int currentRevision = revision;
        try
        {
            for (int i = 0; i < behaviours.Count && IsActive && revision == currentRevision; i++) behaviours[i].Tick();
        }
        catch (Exception execution)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("Skill execution and cleanup failed.", execution, cleanup); }
            throw;
        }
    }

    // Manager tìm/thu hồi đúng owner, không phụ thuộc vị trí trong danh sách.
    internal bool BelongsTo(CharacterView character) => ReferenceEquals(owner, character);

    // Owner chết hoặc trả pool thì dừng ngay, không đợi tick tiếp theo.
    private void OnOwnerEnded(CharacterView character) => Dispose();

    // Disable phải pause timer/visual ngay.
    private void OnOwnerDeactivated() => SetPaused(true);

    // Không reset timer khi đổi pause; hành vi mở thêm cũng nhận cùng trạng thái này.
    internal void SetPaused(bool value)
    {
        if (!IsActive || paused == value) return;
        paused = value;
        foreach (Behaviour behaviour in behaviours.ToArray())
        {
            if (!IsActive) break;
            behaviour.SetPaused(value);
        }
    }

    // Thu hồi mọi hành vi kể cả khi một hành vi cleanup lỗi.
    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        revision++;
        owner.Died -= OnOwnerEnded;
        owner.Released -= OnOwnerEnded;
        owner.Deactivated -= OnOwnerDeactivated;
        var failures = new List<Exception>(); // Chỉ cấp phát trong cleanup.
        foreach (Behaviour behaviour in behaviours.ToArray())
            try { behaviour.Dispose(); } catch (Exception exception) { failures.Add(exception); }
        behaviours.Clear();
        if (failures.Count > 0) throw new AggregateException("Skill cleanup failed.", failures);
    }

    // Kiểm tra thời gian của caller/manual simulation.
    internal static void ValidateDeltaTime(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
    }

    #endregion
}
