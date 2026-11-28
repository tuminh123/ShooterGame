using System;
using UnityEngine;

/// <summary>
/// Hành vi cộng dồn khi giết quái: nghe <see cref="CombatHits.Killed"/>, đòn của chủ giết được quái thì thêm một lớp buff
/// (tối đa N lớp); không giết thêm trong thời hạn thì mất hết. Data: <see cref="KillStackData"/>.
/// </summary>
public sealed class KillStackSkill : Skill
{
    private KillStackData data;
    private Action<CharacterHandle, CharacterView> onKilled; // Tạo một lần cho mỗi instance (pool), không cấp phát mỗi lượt.
    private bool listening;
    private int stacks;
    private float remaining;
    public override SkillBehaviourType Type => SkillBehaviourType.KillStack;

    protected override void InitializeRuntime()
    {
        data = null;
        stacks = 0;
        remaining = 0f;
        listening = false;
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as KillStackData ?? throw new ArgumentException("KillStackSkill requires KillStackData.");
        data.Validate();
        if (listening) return;
        onKilled ??= OnKilled;
        Services.Hits.Killed += onKilled;
        listening = true;
    }

    private void OnKilled(CharacterHandle attacker, CharacterView victim)
    {
        if (data == null || !Owner.CanRun || !attacker.Equals(Owner.Handle)) return;
        stacks = Mathf.Min(stacks + 1, data.MaxStacks);
        remaining = data.Duration;
        StatModifier one = data.PerStack;
        Services.Buffs.Apply(Owner.Handle, this, new StatModifier(one.stat, one.operation, one.value * stacks), data.Duration);
    }

    // Đồng bộ bộ đếm với buff: buff hết giờ thì lớp cũng về 0.
    public override void Tick(float deltaTime)
    {
        if (stacks == 0) return;
        remaining -= deltaTime;
        if (remaining <= 0f) stacks = 0;
    }

    public override void Dispose()
    {
        if (listening && Services != null) Services.Hits.Killed -= onKilled;
        listening = false;
        stacks = 0;
    }
}

/// <summary>
/// Hành vi chỉ số theo số skill nhận trong trận đang có (Aura of Valor): đếm lại mỗi nửa giây, cập nhật một nguồn modifier.
/// Data: <see cref="SkillScalingData"/>.
/// </summary>
public sealed class SkillScalingSkill : Skill
{
    private const float RefreshSeconds = 0.5f;
    private SkillScalingData data;
    private StatModifierLease lease;
    private int appliedCount = -1;
    private float timer;
    public override SkillBehaviourType Type => SkillBehaviourType.SkillScaling;

    protected override void InitializeRuntime()
    {
        data = null;
        lease = null;
        appliedCount = -1;
        timer = 0f;
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as SkillScalingData ?? throw new ArgumentException("SkillScalingSkill requires SkillScalingData.");
        data.Validate();
        appliedCount = -1; // Áp lại theo cấu hình mới ở tick sau.
        timer = 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (data == null || !Owner.IsValid) return;
        timer -= deltaTime;
        if (timer > 0f) return;
        timer = RefreshSeconds;
        int count = Services.GameplaySkills != null ? Services.GameplaySkills.CountOwned(Owner.Owner) : 0;
        if (count == appliedCount) return;
        appliedCount = count;
        StatModifier one = data.PerSkill;
        var modifier = new StatModifier(one.stat, one.operation, one.value * count);
        if (lease == null) lease = Owner.Identity.Stats.AddSource(modifier);
        else lease.Update(modifier);
    }

    public override void Dispose()
    {
        StatModifierLease previous = lease;
        lease = null;
        appliedCount = -1;
        previous?.Dispose();
    }
}

/// <summary>
/// Hành vi hồi giáp: không mất HP/giáp trong <c>delay</c> giây thì hồi giáp đã mất theo thời gian (không vượt giáp tối đa).
/// Data: <see cref="ShieldRegenData"/>.
/// </summary>
public sealed class ShieldRegenSkill : Skill
{
    private ShieldRegenData data;
    private float lastVitals, quiet;
    public override SkillBehaviourType Type => SkillBehaviourType.ShieldRegen;

    protected override void InitializeRuntime()
    {
        data = null;
        lastVitals = -1f;
        quiet = 0f;
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as ShieldRegenData ?? throw new ArgumentException("ShieldRegenSkill requires ShieldRegenData.");
        data.Validate();
    }

    public override void Tick(float deltaTime)
    {
        if (data == null || !Owner.CanRun) return;
        CharacterRuntimeData runtime = Owner.Identity;
        float vitals = runtime.CurrentHealth + runtime.CurrentShield;
        if (lastVitals >= 0f && vitals < lastVitals - 0.0001f) quiet = 0f; // Vừa bị đánh.
        else quiet += deltaTime;
        if (quiet >= data.Delay && runtime.CurrentShield < runtime.MaxShield)
            Owner.Owner.Restore(0f, data.PerSecond * deltaTime);
        lastVitals = runtime.CurrentHealth + runtime.CurrentShield;
    }
}
