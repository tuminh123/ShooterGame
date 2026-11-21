using System;

/// <summary>Hành vi tăng/giảm chỉ số: giữ một modifier chỉ số trên chủ nhân trong suốt thời gian sở hữu skill.</summary>
/// <remarks>Dùng chung modifier của hệ chỉ số nên không cần switch theo loại chỉ số. Lên cấp thì thay modifier tại chỗ. Data: <see cref="StatBonusData"/>.</remarks>
public sealed class StatBonusSkill : Skill
{
    #region Runtime State

    private StatModifierLease modifier; // Token của đúng skill và đúng spawn.
    private StatModifier applied; // Snapshot config đã áp dụng, không giữ reference data có thể đổi trong Inspector.
    private bool disposed; // Chặn callback sau cleanup.
    public override SkillBehaviourType Type => SkillBehaviourType.StatBuff;

    #endregion

    #region Configuration

    // Runtime mới không thừa kế token của mẫu.
    protected override void InitializeRuntime()
    {
        modifier = null;
        applied = default;
        disposed = false;
    }

    // Cùng đường xử lý cho mọi stat/phép tính; đổi cấp thay nguồn modifier trong một lần.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as StatBonusData ?? throw new ArgumentException("StatBonusSkill requires StatBonusData.");
        next.Validate();
        StatModifier value = next.Modifier;
        if (disposed || !Owner.IsValid) return;
        if (modifier != null)
        {
            if (applied.stat == value.stat && applied.operation == value.operation && applied.value == value.value) return;
            modifier.Update(value);
            if (!disposed) applied = value;
            return;
        }
        StatModifierLease token = Owner.Identity.Stats.AddSource(value);
        if (disposed) token.Dispose();
        else { modifier = token; applied = value; }
    }

    #endregion

    #region Cleanup

    // Gỡ nguồn modifier này, giữ nguyên buff/debuff từ các nguồn khác.
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        StatModifierLease previous = modifier;
        modifier = null;
        previous?.Dispose();
    }

    #endregion
}
