using System;
using System.Collections.Generic;

/// <summary>Hành vi tăng/giảm chỉ số: giữ một modifier chỉ số trên chủ nhân trong suốt thời gian sở hữu skill.</summary>
/// <remarks>
/// Dùng chung modifier của hệ chỉ số nên không cần switch theo loại chỉ số. Lên cấp thì thay modifier tại chỗ.
/// Mọi chỉ số của một cấp (chính + thêm) là <b>một</b> nguồn bonus: lên cấp thay cả nhóm, gỡ skill gỡ cả nhóm. Data: <see cref="StatBonusData"/>.
/// </remarks>
public sealed class StatBonusSkill : Skill
{
    #region Runtime State

    private StatModifierLease modifier; // Token của đúng skill và đúng spawn.
    private StatModifier applied; // Snapshot config đã áp dụng, không giữ reference data có thể đổi trong Inspector.
    private StatModifier[] appliedExtras = Array.Empty<StatModifier>(); // Snapshot chỉ số thêm đã áp dụng (bản sao riêng).
    private bool disposed; // Chặn callback sau cleanup.
    public override SkillBehaviourType Type => SkillBehaviourType.StatBuff;

    #endregion

    #region Configuration

    // Runtime mới không thừa kế token của mẫu.
    protected override void InitializeRuntime()
    {
        modifier = null;
        applied = default;
        appliedExtras = Array.Empty<StatModifier>();
        disposed = false;
    }

    // Cùng đường xử lý cho mọi stat/phép tính; đổi cấp thay nguồn modifier trong một lần.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as StatBonusData ?? throw new ArgumentException("StatBonusSkill requires StatBonusData.");
        next.Validate();
        StatModifier value = next.Modifier;
        IReadOnlyList<StatModifier> extras = next.ExtraModifiers;
        if (disposed || !Owner.IsValid) return;
        if (modifier != null)
        {
            if (Same(applied, value) && SameExtras(extras)) return;
            // Chỉ một chỉ số (đa số buff): đường không cấp phát như cũ. Nhiều chỉ số: thay cả nhóm trong một lần.
            if (extras.Count == 0) modifier.Update(value);
            else modifier.Update(next.CreateModifiers());
            if (!disposed) Remember(value, extras);
            RestoreOnApply(next);
            return;
        }
        StatModifierLease token = extras.Count == 0
            ? Owner.Identity.Stats.AddSource(value)
            : Owner.Identity.Stats.AddSource(next.CreateModifiers());
        if (disposed) token.Dispose();
        else
        {
            modifier = token;
            Remember(value, extras);
        }
        RestoreOnApply(next);
    }

    // Hồi máu một lần khi cấp có Restore Percent On Apply được áp (sau khi max HP mới đã có hiệu lực).
    private void RestoreOnApply(StatBonusData next)
    {
        if (disposed || next.RestorePercentOnApply <= 0f || !Owner.CanRun) return;
        Owner.Owner.Restore(Owner.Identity.MaxHealth * next.RestorePercentOnApply / 100f);
    }

    private static bool Same(StatModifier first, StatModifier second)
        => first.stat == second.stat && first.operation == second.operation && first.value == second.value;

    private bool SameExtras(IReadOnlyList<StatModifier> extras)
    {
        if (extras.Count != appliedExtras.Length) return false;
        for (int index = 0; index < extras.Count; index++)
            if (!Same(extras[index], appliedExtras[index])) return false;
        return true;
    }

    // Bản sao riêng: Inspector có thể sửa mảng của asset trong Play Mode, so sánh phải dựa trên giá trị đã áp dụng.
    private void Remember(StatModifier value, IReadOnlyList<StatModifier> extras)
    {
        applied = value;
        if (extras.Count == 0)
        {
            appliedExtras = Array.Empty<StatModifier>();
            return;
        }
        if (appliedExtras.Length != extras.Count) appliedExtras = new StatModifier[extras.Count];
        for (int index = 0; index < extras.Count; index++) appliedExtras[index] = extras[index];
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
