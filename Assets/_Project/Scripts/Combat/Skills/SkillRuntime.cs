using System;
using System.Collections.Generic;

/// <summary>
/// Một skill đang chạy của một nhân vật: giữ cấp hiện tại, các hành vi đã mở ở cấp đó, và cooldown riêng của từng hành vi.
/// </summary>
/// <remarks>
/// Một skill có thể gồm nhiều hành vi (ví dụ vừa bắn vừa có aura). Lên cấp có thể mở thêm, gỡ bớt hoặc đổi
/// thông số hành vi, nhưng không reset cooldown đang chạy. Runtime tự kết thúc khi chủ nhân chết hoặc vỏ sang lượt spawn khác.
/// <para>
/// Dùng lại được (<see cref="IPoolable"/>): skill vốn có của mọi nhân vật được <see cref="SkillFactory"/> cho thuê và nhận
/// lại khi <see cref="CharacterSkills"/> dọn. Delegate, HashSet, danh sách tạm và wrapper hành vi được giữ giữa các lượt
/// nên một lượt spawn mới không cấp phát lại chúng. Skill nhận trong trận (SkillSystem) vẫn tạo mới vì UI có thể giữ tham chiếu.
/// </para>
/// </remarks>
public sealed class SkillRuntime : IDisposable, IPoolable
{
    #region Behaviour Scheduling

    // Cooldown thuộc runtime từng hành vi; SO chỉ giữ cấu hình, không giữ thời gian đang chạy.
    // Wrapper được runtime dùng lại (spareBehaviours) giữa các lượt nên mọi field đều gán lại trong Initialize.
    private sealed class Behaviour
    {
        internal ISkill Action; // Hành vi lấy từ pool của factory, riêng cho lượt này.
        private SkillRuntime runtime;
        private float remaining; // Giữ cooldown đang chạy khi thay dữ liệu cấp.
        private float cooldown; // Nhịp mới áp dụng cho lần cast thành công tiếp theo.
        private bool disposed; // Chặn tick và dispose lặp.
        private bool paused; // Ngừng cả tick liên tục lẫn cast.
        internal float Remaining => remaining;

        // Mỗi hành vi có bộ đếm riêng và sẵn sàng cast ở lần tick đầu.
        internal void Initialize(ISkill action, bool paused, SkillRuntime runtime)
        {
            Action = action;
            this.runtime = runtime;
            this.paused = paused;
            remaining = cooldown = 0f;
            disposed = false;
        }

        // Đổi config không khởi động lại cooldown đang chạy.
        internal void ApplyData(SkillBehaviourData data)
        {
            Action.ApplyData(data);
            cooldown = data.Cooldown;
        }

        // Hành vi liên tục vẫn cập nhật trong lúc chờ lượt cast tiếp theo.
        internal void Tick(float deltaTime)
        {
            if (disposed || paused) return;
            remaining = Math.Max(0f, remaining - deltaTime);
            Action.Tick(deltaTime);
            if (disposed || Remaining > 0f || !Action.TryExecute() || disposed) return;
            remaining = cooldown;
            runtime.NotifyVisualAction(Action.Type);
        }

        // Pause giữ nguyên cooldown và đồng bộ visual/state của hành vi.
        internal void SetPaused(bool value)
        {
            if (disposed) return;
            paused = value;
            Action.SetPaused(value);
        }

        /// <summary>Kết thúc hành vi; true nếu lần gọi này thực sự dọn (lần đầu).</summary>
        internal bool End()
        {
            if (disposed) return false;
            disposed = true;
            Action.Dispose();
            return true;
        }

        // Wrapper về kho dự phòng của runtime: bỏ tham chiếu để không giữ hành vi/chủ cũ.
        internal void Clear()
        {
            Action = null;
            runtime = null;
        }
    }

    #endregion

    #region Ownership And State

    private readonly List<Behaviour> behaviours = new List<Behaviour>(); // Chỉ chứa hành vi đã được mở ở cấp hiện tại.
    // Bản duyệt ổn định khi callback đổi danh sách. Mỗi lần duyệt mượn một list riêng (callback có thể gọi lồng
    // Dispose/SetPaused trong lúc đang duyệt), trả lại sau đó: chạy ổn định thì không cấp phát.
    private readonly Stack<List<Behaviour>> spareSnapshots = new Stack<List<Behaviour>>();
    private readonly Stack<Behaviour> spareBehaviours = new Stack<Behaviour>(); // Wrapper rảnh của runtime này.
    private readonly Action<CharacterView> ownerEnded; // Delegate tạo một lần: đăng ký lại mỗi lượt không cấp phát.
    private readonly Action ownerDeactivated;
    private CharacterSkills context; // Context đúng lượt spawn.
    private CharacterView owner; // Owner của toàn bộ hành vi.
    private SkillFactory factory; // Tạo/thu hồi hành vi khi cấp mới mở thêm hoặc gỡ bớt loại.
    private bool changingLevel; // Chặn nâng cấp lồng nhau từ callback.
    private bool paused; // Trạng thái truyền cho hành vi mở mới.
    private int revision; // Thay đổi danh sách giữa callback sẽ kết thúc vòng tick cũ.
    // Độ sâu "đang chạy code hành vi" (Tick, đổi cấp, pause, dọn). Một đòn đánh có thể kéo theo callback dọn chính skill này
    // (Player chết → kết thúc trận → dọn skill của mọi nhân vật) trong lúc hàm của hành vi còn chạy dở: trả về pool lúc đó sẽ
    // xóa Owner/Services dưới chân nó, hoặc cho chủ khác mượn lại ngay. Vì vậy mọi lần trả về pool được hoãn tới khi hết bận.
    private int busy;
    private bool releaseRequested; // Factory đã yêu cầu nhận lại runtime trong lúc bận.
    private readonly List<ISkill> pendingRelease = new List<ISkill>(); // Hành vi đã kết thúc, chờ hết bận mới trả về pool.
    private readonly List<Behaviour> pendingWrappers = new List<Behaviour>(); // Wrapper đã kết thúc, chờ hết bận mới cất vào kho.

    /// <summary>Đang chạy code của hành vi; factory hoãn việc nhận lại tới khi xong.</summary>
    internal bool IsBusy => busy > 0;

    /// <summary>Factory gọi khi muốn nhận lại runtime đang bận: runtime tự trả mình khi lượt chạy ngoài cùng kết thúc.</summary>
    internal void RequestRelease() => releaseRequested = true;

    public SkillDataSO Definition { get; private set; }
    public bool IsActive { get; private set; }
    public int BattleLevel { get; private set; }
    public int PermanentLevel => Definition.Level;
    public SkillSource Source => Definition.Source;
    public int BehaviourCount => behaviours.Count;
    public float CooldownRemaining
    {
        get
        {
            float remaining = float.MaxValue; // Giá trị tổng quan cho debug, không gộp cooldown giữa các hành vi.
            foreach (Behaviour behaviour in behaviours) remaining = Math.Min(remaining, behaviour.Remaining);
            // Chưa có hành vi nào thì không có cooldown.
            if (remaining == float.MaxValue)
                return 0f;
            return remaining;
        }
    }

    #endregion

    #region Initialization

    /// <summary>Factory tạo runtime rảnh (cho pool hoặc dùng ngay); chưa chạy cho tới <see cref="Initialize"/>.</summary>
    internal SkillRuntime()
    {
        ownerEnded = OnOwnerEnded;
        ownerDeactivated = OnOwnerDeactivated;
    }

    // Runtime đăng ký owner trước khi ApplyData có thể phát callback stats.
    // Cấp bắt đầu do nơi tạo truyền vào: runtime chỉ chạy skill, không biết level/sao của Player hay nguồn skill.
    internal void Initialize(SkillDataSO definition, CharacterSkills context, SkillFactory factory, int startLevel)
    {
        if (IsActive) throw new InvalidOperationException("Skill runtime is already running.");
        Definition = definition;
        this.context = context;
        this.factory = factory;
        owner = context.Owner;
        paused = !owner.isActiveAndEnabled;
        changingLevel = false;
        BattleLevel = startLevel;
        IsActive = true;
        owner.Died += ownerEnded;
        owner.Released += ownerEnded;
        owner.Deactivated += ownerDeactivated;
        try { ApplyLevel(BattleLevel); }
        catch (Exception creation)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("Skill initialization and cleanup failed.", creation, cleanup); }
            throw;
        }
    }

    #endregion

    #region Level Configuration

    // Enum hành vi có ít giá trị (thêm ở cuối, số nhỏ); kiểm tra để không tràn mask nếu sau này vượt 64.
    private static ulong Bit(SkillBehaviourType type)
    {
        int index = (int)type;
        if (index < 0 || index >= 64) throw new ArgumentOutOfRangeException(nameof(type), type, "Behaviour type does not fit the level mask.");
        return 1UL << index;
    }

    // Tìm theo enum loại; một skill có tối đa một hành vi của mỗi loại.
    private Behaviour FindBehaviour(SkillBehaviourType type)
    {
        foreach (Behaviour behaviour in behaviours) if (behaviour.Action.Type == type) return behaviour;
        return null;
    }

    // Cập nhật hành vi cũ, mở hành vi mới, thu hồi loại không còn trong cấu hình cấp này.
    private void ApplyLevel(int level)
    {
        Enter();
        try { ApplyLevelCore(level); }
        finally { Exit(); }
    }

    private void ApplyLevelCore(int level)
    {
        revision++;
        IReadOnlyList<SkillBehaviourData> data = Definition.GetLevel(level).Behaviours;
        ulong required = 0; // Bit theo enum: loại cần có ở cấp này (HashSet enum bị boxing trong Mono).
        for (int index = 0; index < data.Count; index++) // Chỉ số, không foreach: tránh cấp phát enumerator mỗi lần spawn.
        {
            SkillBehaviourData item = data[index];
            if (!IsActive) return;
            required |= Bit(item.Type);
            Behaviour behaviour = FindBehaviour(item.Type);
            if (behaviour == null)
            {
                ISkill action = factory.CreateBehaviour(Definition, item.Type, context);
                behaviour = spareBehaviours.Count > 0 ? spareBehaviours.Pop() : new Behaviour();
                behaviour.Initialize(action, paused, this);
                behaviours.Add(behaviour); // Nhận ownership trước callback ApplyData để reset/dispose có thể dọn ngay.
            }
            behaviour.ApplyData(item);
            if (!IsActive) return;
            behaviour.SetPaused(paused);
        }
        List<Behaviour> snapshot = RentSnapshot();
        try
        {
            foreach (Behaviour behaviour in snapshot)
            {
                if (!IsActive) return;
                if ((required & Bit(behaviour.Action.Type)) != 0) continue;
                behaviours.Remove(behaviour);
                EndBehaviour(behaviour);
            }
        }
        finally { ReturnSnapshot(snapshot); }
    }

    // Chỉ gameplay nâng trong trận; cấp mới dùng toàn bộ cấu hình từ SO.
    public bool TryUpgrade()
    {
        if (!IsActive || changingLevel || Source != SkillSource.Gameplay || BattleLevel >= Definition.MaxLevel) return false;
        Definition.Validate();
        changingLevel = true;
        try
        {
            ApplyLevel(BattleLevel + 1);
            if (!IsActive) return false;
            BattleLevel++;
            return true;
        }
        catch (Exception upgrade)
        {
            if (IsActive)
            {
                try { ApplyLevel(BattleLevel); }
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

    // Chỉ boss yêu cầu Attack/Cast; Player và enemy thường vẫn thực thi skill nhưng không phát ani tấn công.
    private void NotifyVisualAction(SkillBehaviourType type)
    {
        // context.IsValid đã kiểm tra đúng lượt spawn và còn sống; callback của hành vi có thể vừa làm owner chết.
        if (!IsActive || !context.IsValid || !owner.IsBoss) return;
        switch (type)
        {
            case SkillBehaviourType.NormalAttack:
                owner.Presentation.RequestVisualState(CharacterVisualState.Attack);
                break;
            case SkillBehaviourType.Shooting:
                CharacterVisualState state = Definition.ShootingVisualState;
                if (state == CharacterVisualState.Attack || state == CharacterVisualState.Cast)
                    owner.Presentation.RequestVisualState(state);
                break;
        }
    }

    // Chủ sở hữu runtime điều phối tick; callback đổi cấp sẽ kết thúc vòng duyệt cũ.
    internal void Tick(float deltaTime)
    {
        ValidateDeltaTime(deltaTime);
        if (deltaTime <= 0f) return;
        if (!IsActive) return;
        Enter();
        try { TickCore(deltaTime); }
        finally { Exit(); }
    }

    private void TickCore(float deltaTime)
    {
        // Owner chết hoặc vỏ đã sang lượt spawn khác: skill của lượt này kết thúc luôn.
        if (!context.IsValid) { Dispose(); return; }
        // Choáng/tuần tra giữ nguyên cooldown; chỉ Dispose khi chết hoặc pool đổi danh tính.
        SetPaused(!owner.isActiveAndEnabled || !owner.CanUseSkills);
        if (paused) return;
        int currentRevision = revision;
        try
        {
            for (int i = 0; i < behaviours.Count && IsActive && revision == currentRevision; i++) behaviours[i].Tick(deltaTime);
        }
        catch (Exception execution)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("Skill execution and cleanup failed.", execution, cleanup); }
            throw;
        }
    }

    // Manager tìm/thu hồi đúng owner, không phụ thuộc vị trí trong danh sách.
    internal bool BelongsTo(CharacterView character) => IsActive && ReferenceEquals(owner, character);

    // Owner chết hoặc trả pool thì dừng ngay, không đợi tick tiếp theo.
    private void OnOwnerEnded(CharacterView character) => Dispose();

    // Disable phải pause hành vi/visual ngay.
    private void OnOwnerDeactivated() => SetPaused(true);

    // Không reset cooldown khi đổi pause; hành vi mở thêm nhận cùng trạng thái này.
    internal void SetPaused(bool value)
    {
        if (!IsActive || paused == value) return;
        paused = value;
        Enter();
        List<Behaviour> snapshot = RentSnapshot();
        try
        {
            foreach (Behaviour behaviour in snapshot)
            {
                if (!IsActive) break;
                behaviour.SetPaused(value);
            }
        }
        finally
        {
            ReturnSnapshot(snapshot);
            Exit();
        }
    }

    // Thu hồi mọi hành vi kể cả khi một hành vi cleanup lỗi. Danh sách lỗi chỉ tạo khi thật sự có lỗi.
    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        revision++;
        owner.Died -= ownerEnded;
        owner.Released -= ownerEnded;
        owner.Deactivated -= ownerDeactivated;
        List<Exception> failures = null;
        Enter(); // Hành vi kết thúc chỉ về pool sau khi dọn xong cả skill (và sau lượt chạy ngoài cùng nếu đang lồng).
        List<Behaviour> snapshot = RentSnapshot();
        behaviours.Clear();
        try
        {
            foreach (Behaviour behaviour in snapshot)
                try { EndBehaviour(behaviour); }
                catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
        }
        finally
        {
            ReturnSnapshot(snapshot);
            Exit();
        }
        if (failures != null) throw new AggregateException("Skill cleanup failed.", failures);
    }

    // Kết thúc một hành vi; hành vi đã dọn sạch được trả về pool khi runtime hết bận, wrapper về kho dự phòng.
    // Dispose của hành vi lỗi thì không trả hành vi đó: trạng thái không chắc sạch, để GC thu.
    private void EndBehaviour(Behaviour behaviour)
    {
        ISkill action = behaviour.Action;
        bool ended;
        try { ended = behaviour.End(); }
        finally
        {
            behaviour.Clear();
            // Wrapper có thể còn đang chạy Tick của chính nó: cất vào kho khi hết bận, không cho ApplyLevel lấy lại ngay.
            if (busy > 0) pendingWrappers.Add(behaviour);
            else spareBehaviours.Push(behaviour);
        }
        if (!ended) return;
        if (busy > 0) pendingRelease.Add(action);
        else factory.ReleaseBehaviour(action);
    }

    private void Enter() => busy++;

    // Hết lượt chạy ngoài cùng: trả các hành vi đã kết thúc, rồi trả chính runtime nếu factory đã yêu cầu.
    private void Exit()
    {
        if (--busy > 0) return;
        for (int i = 0; i < pendingWrappers.Count; i++) spareBehaviours.Push(pendingWrappers[i]);
        pendingWrappers.Clear();
        SkillFactory owningFactory = factory;
        if (owningFactory == null) { pendingRelease.Clear(); return; }
        for (int i = 0; i < pendingRelease.Count; i++) owningFactory.ReleaseBehaviour(pendingRelease[i]);
        pendingRelease.Clear();
        if (!releaseRequested) return;
        releaseRequested = false;
        owningFactory.ReleaseNow(this);
    }

    private List<Behaviour> RentSnapshot()
    {
        List<Behaviour> snapshot = spareSnapshots.Count > 0 ? spareSnapshots.Pop() : new List<Behaviour>(4);
        snapshot.AddRange(behaviours);
        return snapshot;
    }

    private void ReturnSnapshot(List<Behaviour> snapshot)
    {
        snapshot.Clear();
        spareSnapshots.Push(snapshot);
    }

    // Kiểm tra thời gian của caller/manual simulation.
    internal static void ValidateDeltaTime(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
    }

    #endregion

    #region Pool

    // Factory gọi khi nhận lại: kết thúc nếu còn chạy, rồi bỏ mọi tham chiếu tới chủ, context và asset của lượt cũ.
    void IPoolable.ResetForPool()
    {
        Dispose();
        Definition = null;
        context = null;
        owner = null;
        factory = null;
        BattleLevel = 0;
        changingLevel = false;
        paused = false;
        releaseRequested = false;
        pendingRelease.Clear();
        for (int i = 0; i < pendingWrappers.Count; i++) spareBehaviours.Push(pendingWrappers[i]);
        pendingWrappers.Clear();
        busy = 0;
    }

    #endregion
}
