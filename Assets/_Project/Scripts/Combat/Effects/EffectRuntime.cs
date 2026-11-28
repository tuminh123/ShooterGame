/// <summary>
/// Handle của một effect duy trì (ví dụ slow của aura), để hành vi skill kiểm tra hoặc tự gỡ effect đó sau này.
/// </summary>
/// <remarks>Giữ phiên bản của runtime, nên handle cũ không gỡ nhầm runtime đã được pool cho effect khác thuê lại.</remarks>
public readonly struct EffectHandle
{
    #region Runtime Identity

    internal readonly EffectRuntime Runtime; // Tham chiếu object trong pool, không sở hữu vòng đời của object.
    internal readonly ulong Version; // Chụp phiên bản lúc tạo handle; so sánh qua EffectSystem.IsActive.

    // Handle mặc định không chứa runtime; dùng khi yêu cầu áp dụng effect thất bại.
    internal EffectHandle(EffectRuntime runtime)
    {
        Runtime = runtime;
        Version = runtime.Version;
    }

    #endregion
}

/// <summary>
/// Một effect đang chạy trên đúng một lượt spawn của mục tiêu. Class con chỉ thực hiện tác động
/// (thêm modifier, gây sát thương định kỳ); thời hạn và quyền sở hữu do <see cref="EffectSystem"/> quản lý.
/// </summary>
/// <remarks>Được pool tái sử dụng: mọi state phải được xóa trong <see cref="Reset"/>.</remarks>
public abstract class EffectRuntime : IPoolable
{
    #region Context And Manager State

    internal IGameplayEffect Configuration; // Cấu hình chỉ đọc dùng để nhận diện lần áp dụng lại.
    internal CharacterView Owner; // Vỏ Unity có thể được tái sử dụng cho lượt spawn khác.
    internal CharacterRuntimeData Identity; // Runtime của mục tiêu: key tra effect theo mục tiêu, và nơi thêm modifier chỉ số.

    /// <summary>Danh tính lượt spawn của mục tiêu; kiểm tra lại giữa các tick và sau callback sát thương.</summary>
    internal CharacterHandle Target;
    protected CharacterTeam SourceTeam; // Chốt phe nguồn lúc áp dụng; runtime không cần giữ người bắn.
    internal float SourceDamage; // Damage của bên gây effect lúc trúng; effect theo % Damage dùng số này.
    internal double Remaining, TickElapsed; // Thời hạn và pha tick riêng của lượt effect này.
    internal bool Paused;
    internal EffectSystem Manager; // Bên duy nhất cập nhật và trả runtime về pool; handle so sánh để chặn handle của manager khác.
    internal int ActiveIndex; // Cho phép manager gỡ O(1) bằng đổi chỗ phần tử cuối.
    internal int TargetIndex; // Chỉ số trong nhóm effect của đúng cá thể.
    internal bool IsActive; // False ngay khi bị gỡ, dù Remove/Reset có thể đang chờ callback kết thúc.
    internal bool IsMaintained; // Skill chủ động gỡ; không tham gia bảng gia hạn của effect có thời hạn.
    internal ulong Version; // Không reset khi trả pool; mỗi lượt thuê có phiên bản riêng.
    internal bool FeedbackApplied;

    #endregion

    #region Spawn Initialization

    // Chụp danh tính mục tiêu và tăng phiên bản effect cho mỗi lượt thuê từ pool.
    internal void Initialize(IGameplayEffect configuration, CharacterView owner, CharacterTeam sourceTeam)
    {
        Version = checked(Version + 1);
        Configuration = configuration;
        Owner = owner;
        Identity = owner.RuntimeData;
        Target = new CharacterHandle(owner);
        SourceTeam = sourceTeam;
        IsActive = true;
    }

    #endregion

    #region Gameplay Behavior

    public abstract void Apply(); // Thiết lập tác động ban đầu sau khi manager đã đăng ký runtime.
    public virtual void Tick(int tickCount) { } // Manager tính nhịp đến hạn; runtime chỉ thực hiện hành vi.
    public abstract void Remove(); // Hoàn tác tài nguyên/modifier runtime sở hữu trước khi Reset.

    #endregion

    #region Pool Reset

    // Chỉ pool gọi sau Remove. Giải phóng tham chiếu nhưng giữ Version để vô hiệu handle cũ.
    internal virtual void Reset()
    {
        Configuration = null;
        Owner = null;
        Identity = null;
        Target = default;
        SourceTeam = default;
        SourceDamage = 0f;
        Remaining = TickElapsed = 0d;
        Paused = false;
        Manager = null;
        ActiveIndex = -1;
        TargetIndex = -1;
        IsActive = false;
        IsMaintained = false;
        FeedbackApplied = false;
    }

    // Pool<T> gọi qua interface; Reset vẫn internal virtual để lớp con dọn phần riêng.
    void IPoolable.ResetForPool() => Reset();

    #endregion
}

/// <summary>Runtime làm chậm: giữ một modifier giảm tốc độ di chuyển trên mục tiêu cho tới khi bị gỡ.</summary>
/// <remarks>Đạn và aura dùng chung runtime này; chỉ khác thời điểm bị gỡ (hết hạn hoặc rời vùng aura).</remarks>
public sealed class SlowEffectRuntime : EffectRuntime
{
    #region Slow State

    internal float SlowFraction; // 0..1; 0.3 được chuyển thành modifier -30 phần trăm.
    private StatModifierLease modifier; // Mỗi runtime sở hữu một nguồn giảm tốc độc lập.

    #endregion

    #region Modifier Lifecycle

    // Mỗi runtime thêm nguồn Percent riêng; hệ stat cộng các nguồn và chặn tốc độ cuối ở 0.
    public override void Apply()
    {
        StatModifierLease token = Identity.Stats.AddSource(
            new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -SlowFraction * 100f));
        // AddSource phát Changed đồng bộ: callback có thể vừa dọn effect hoặc đổi lượt spawn.
        if (!IsActive || !Target.IsCurrent) token.Dispose();
        else modifier = token;
    }

    // Bỏ ownership trước Dispose vì gỡ modifier cũng có thể phát callback stat lồng nhau.
    public override void Remove()
    {
        StatModifierLease previous = modifier;
        modifier = null;
        previous?.Dispose();
    }

    #endregion

    #region Pool Reset

    // Modifier đã được Remove giải phóng; không mang mức slow sang lần thuê tiếp theo.
    internal override void Reset()
    {
        modifier = null;
        SlowFraction = 0f;
        base.Reset();
    }

    #endregion
}

/// <summary>Runtime sát thương theo thời gian: gây một lượng sát thương cố định mỗi nhịp.</summary>
/// <remarks>Cháy và độc dùng chung runtime này; mỗi runtime giữ thời hạn và pha nhịp riêng.</remarks>
public sealed class DamageOverTimeEffectRuntime : EffectRuntime
{
    #region Periodic Damage State

    internal float DamagePerTick; // Damage cố định mỗi nhịp, không tự nhân damage hiện tại của nguồn.
    internal float DamagePercentPerTick; // % Damage của nguồn mỗi nhịp (chụp SourceDamage lúc trúng).
    internal int Stacks = 1; // Số lớp cộng dồn hiện tại.

    #endregion

    #region Damage Lifecycle

    // Không gây damage ngay khi áp dụng; nhịp đầu xảy ra sau TickInterval.
    public override void Apply() { }

    // Frame dài có thể đến hạn nhiều nhịp; kiểm tra lại trạng thái sau mỗi lần gây damage.
    public override void Tick(int tickCount)
    {
        for (int index = 0; index < tickCount && IsActive && Target.TryGetCharacter(out CharacterView target); index++)
        {
            // Đi qua luồng damage chung để vẫn trừ giáp trước rồi mới trừ máu.
            float amount = (DamagePerTick + DamagePercentPerTick / 100f * SourceDamage) * Stacks;
            if (amount > 0f) target.ReceiveDamage(new DamageInfo(amount, SourceTeam));
        }
    }

    // Không giữ modifier hay visual riêng; manager đã ngừng cập nhật trước khi gọi Remove.
    public override void Remove() { }

    #endregion

    #region Pool Reset

    internal override void Reset()
    {
        DamagePerTick = DamagePercentPerTick = 0f;
        Stacks = 1;
        base.Reset();
    }

    #endregion
}

/// <summary>
/// Runtime của choáng/đóng băng/dấu săn: giữ một modifier chỉ số và/hoặc một nguồn choáng trên mục tiêu cho tới khi bị gỡ.
/// </summary>
/// <remarks>Choáng = đứng yên (MoveSpeed −100%) + không đánh. Boss miễn nhiễm phần choáng.</remarks>
public sealed class ControlEffectRuntime : EffectRuntime
{
    #region State

    internal bool Stuns; // Có khóa đánh/đứng yên hay không.
    internal StatModifier Modifier; // value 0 = không có modifier riêng.
    private StatModifierLease modifier;
    private StatModifierLease stunMove;
    private bool stunned;

    #endregion

    #region Lifecycle

    public override void Apply()
    {
        if (Modifier.value != 0f) Keep(ref modifier, Identity.Stats.AddSource(Modifier));
        if (!Stuns || Owner.IsBoss || !IsActive || !Target.IsCurrent) return;
        Identity.AddStun();
        stunned = true;
        Keep(ref stunMove, Identity.Stats.AddSource(new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -100f)));
    }

    // AddSource phát Changed đồng bộ: callback có thể vừa dọn effect hoặc đổi lượt spawn.
    private void Keep(ref StatModifierLease slot, StatModifierLease token)
    {
        if (!IsActive || !Target.IsCurrent) token.Dispose();
        else slot = token;
    }

    public override void Remove()
    {
        StatModifierLease previousModifier = modifier, previousMove = stunMove;
        modifier = stunMove = null;
        // Lượt spawn đã kết thúc thì runtime nhân vật đã reset bộ đếm choáng; không trừ nhầm lượt mới.
        if (stunned && Target.IsCurrent) Identity.RemoveStun();
        stunned = false;
        previousModifier?.Dispose();
        previousMove?.Dispose();
    }

    internal override void Reset()
    {
        modifier = stunMove = null;
        stunned = Stuns = false;
        Modifier = default;
        base.Reset();
    }

    #endregion
}
