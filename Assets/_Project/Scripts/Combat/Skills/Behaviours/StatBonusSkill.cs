using System;

// Hành vi buff owner bằng modifier chung; không switch riêng theo damage/crit/giáp.
[Serializable]
public sealed class StatBonusSkill : Skill
{
    #region Runtime State

    [NonSerialized] private StatModifierLease modifier; // Token của đúng skill và đúng spawn.
    [NonSerialized] private StatModifier applied; // Snapshot config đã áp dụng, không giữ reference data có thể đổi trong Inspector.
    [NonSerialized] private bool disposed; // Chặn callback sau cleanup.
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
        StatModifierLease token = Owner.Identity.AddModifier(value);
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
