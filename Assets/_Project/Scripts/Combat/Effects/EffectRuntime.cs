// Một bản đang chạy trên đúng một lượt spawn; chỉ EffectManager quản lý thời gian và quyền sở hữu.
public abstract class EffectRuntime : ITimerListener
{
    internal IGameplayEffect Configuration;
    internal CharacterView Owner;
    internal CharacterRuntimeData Identity; // Giữ danh tính cũ để không áp dụng nhầm khi vỏ được pool tái sử dụng.
    protected CharacterTarget Target;
    protected CharacterTeam SourceTeam;
    internal TimerHandle Timer;
    internal EffectManager Manager;
    internal int ActiveIndex; // Cho phép manager gỡ O(1) bằng đổi chỗ phần tử cuối.
    internal int TargetIndex; // Chỉ số trong nhóm effect của đúng cá thể.
    internal bool IsActive;

    internal void Initialize(IGameplayEffect configuration, CharacterView owner, CharacterTeam sourceTeam)
    {
        Configuration = configuration;
        Owner = owner;
        Identity = owner.RuntimeData;
        Target = new CharacterTarget(owner);
        SourceTeam = sourceTeam;
        IsActive = true;
    }

    public abstract void Apply();
    public virtual void Tick(int tickCount) { } // TimerJob đã tính nhịp; runtime chỉ thực hiện hành vi.
    public abstract void Remove();

    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        if (IsActive && Timer.Equals(handle)) Manager.HandleTimer(this, result);
    }

    internal virtual void Reset()
    {
        Configuration = null;
        Owner = null;
        Identity = null;
        Target = default;
        SourceTeam = default;
        Timer = default;
        Manager = null;
        ActiveIndex = -1;
        TargetIndex = -1;
        IsActive = false;
    }
}

public sealed class SlowEffectRuntime : EffectRuntime
{
    internal float SlowFraction;
    private CharacterEffectHandler modifiers; // Handler của lượt spawn đã nhận modifier.
    private long modifierId; // Token để gỡ đúng phần giảm tốc; không tạo IDisposable/closure mỗi lần áp dụng.

    public override void Apply()
    {
        modifiers = Owner.Effects;
        modifierId = modifiers.AddSpeedMultiplier(1f - SlowFraction);
    }

    public override void Remove()
    {
        modifiers?.RemoveSpeedMultiplier(modifierId);
    }

    internal override void Reset()
    {
        modifiers = null;
        modifierId = 0;
        SlowFraction = 0f;
        base.Reset();
    }
}

public sealed class DamageOverTimeEffectRuntime : EffectRuntime
{
    internal float DamagePerTick;

    public override void Apply() { }

    public override void Tick(int tickCount)
    {
        for (int index = 0; index < tickCount && IsActive && Target.TryGetCharacter(out CharacterView target); index++)
        {
            // Đi qua luồng damage chung để vẫn trừ giáp trước rồi mới trừ máu.
            target.ReceiveDamage(new DamageInfo(DamagePerTick, SourceTeam));
        }
    }

    public override void Remove() { }

    internal override void Reset()
    {
        DamagePerTick = 0f;
        base.Reset();
    }
}
