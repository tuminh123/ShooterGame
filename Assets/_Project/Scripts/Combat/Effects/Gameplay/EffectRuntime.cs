// Handle giữ phiên bản để không gỡ nhầm runtime đã được pool tái sử dụng.
public readonly struct EffectHandle
{
    #region Runtime Identity

    internal readonly EffectRuntime Runtime; // Tham chiếu object trong pool, không sở hữu vòng đời của object.
    internal readonly ulong Version; // Chụp phiên bản lúc tạo handle; so sánh qua EffectManager.IsActive.

    // Handle mặc định không chứa runtime; dùng khi yêu cầu áp dụng effect thất bại.
    internal EffectHandle(EffectRuntime runtime)
    { Runtime = runtime; Version = runtime.Version; }

    #endregion
}

// Một bản đang chạy trên đúng một lượt spawn; chỉ EffectManager quản lý thời gian và quyền sở hữu.
public abstract class EffectRuntime : ITimerListener
{
    #region Context And Manager State

    internal IGameplayEffect Configuration; // Cấu hình chỉ đọc dùng để nhận diện lần áp dụng lại.
    internal CharacterView Owner; // Vỏ Unity có thể được tái sử dụng cho lượt spawn khác.
    internal CharacterRuntimeData Identity; // Giữ danh tính cũ để không áp dụng nhầm khi vỏ được pool tái sử dụng.
    internal ulong Generation; // Phiên bản spawn của mục tiêu, khác với Version của chính effect runtime.
    protected CharacterTarget Target; // Kiểm tra lại mục tiêu giữa các tick/callback damage.
    protected CharacterTeam SourceTeam; // Chốt phe nguồn lúc áp dụng; runtime không cần giữ người bắn.
    internal TimerHandle Timer; // Mặc định nếu effect duy trì không có tick định kỳ.
    internal EffectManager Manager; // Bên duy nhất đăng ký, hủy và trả runtime về pool.
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
        Generation = Identity.Generation;
        Target = new CharacterTarget(owner);
        SourceTeam = sourceTeam;
        IsActive = true;
    }

    #endregion

    #region Gameplay Behavior And Timer Callback

    public abstract void Apply(); // Thiết lập tác động ban đầu sau khi manager đã đăng ký runtime.
    public virtual void Tick(int tickCount) { } // TimerJob đã tính nhịp; runtime chỉ thực hiện hành vi.
    public abstract void Remove(); // Hoàn tác tài nguyên/modifier runtime sở hữu trước khi Reset.

    // Bỏ callback của lịch cũ; manager tiếp tục kiểm tra danh tính và vòng đời mục tiêu.
    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        if (IsActive && Timer.Equals(handle)) Manager.HandleTimer(this, result);
    }

    #endregion

    #region Pool Reset

    // Chỉ pool gọi sau Remove. Giải phóng tham chiếu nhưng giữ Version để vô hiệu handle cũ.
    internal virtual void Reset()
    {
        Configuration = null;
        Owner = null;
        Identity = null;
        Generation = 0;
        Target = default;
        SourceTeam = default;
        Timer = default;
        Manager = null;
        ActiveIndex = -1;
        TargetIndex = -1;
        IsActive = false;
        IsMaintained = false;
        FeedbackApplied = false;
    }

    #endregion
}

// Bullet và aura dùng chung runtime này; khác nhau ở thời điểm manager yêu cầu gỡ.
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
        StatModifierLease token = Identity.AddModifier(
            new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -SlowFraction * 100f));
        // AddModifier phát Changed đồng bộ: callback có thể vừa dọn effect hoặc đổi lượt spawn.
        if (!IsActive || Identity.Generation != Generation) token.Dispose();
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

// Burn/Poison dùng cùng hành vi; cấu hình quyết định lượng damage và nhịp timer.
public sealed class DamageOverTimeEffectRuntime : EffectRuntime
{
    #region Periodic Damage State

    internal float DamagePerTick; // Damage cố định mỗi nhịp, không tự nhân damage hiện tại của nguồn.

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
            target.ReceiveDamage(new DamageInfo(DamagePerTick, SourceTeam));
        }
    }

    // Không giữ modifier hay visual riêng; manager đã hủy timer trước khi gọi Remove.
    public override void Remove() { }

    #endregion

    #region Pool Reset

    internal override void Reset()
    {
        DamagePerTick = 0f;
        base.Reset();
    }

    #endregion
}
