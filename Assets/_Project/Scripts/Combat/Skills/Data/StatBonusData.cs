using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thông số của <see cref="StatBonusSkill"/> tại một cấp: chỉ số nào, cộng thẳng hay phần trăm, giá trị bao nhiêu (âm là debuff).
/// Một cấp có thể cộng nhiều chỉ số cùng lúc qua <see cref="ExtraModifiers"/> (ví dụ Might Lv5: +40% Damage và +10% BossDamage).
/// </summary>
/// <remarks>
/// Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.
/// Lý do giữ cặp stat/operation/value riêng thay vì đổi hẳn sang mảng: asset cũ đang lưu các field này, đổi sẽ mất dữ liệu.
/// </remarks>
[Serializable]
public sealed class StatBonusData : SkillBehaviourData
{
    #region Configuration

    [SerializeField][Tooltip("Chỉ số nhân vật sẽ bị thay đổi.")] private StatType stat = StatType.Defense;
    [SerializeField][Tooltip("Add cộng trực tiếp; Percent thay đổi theo phần trăm.")] private StatOperation operation;
    [SerializeField][Tooltip("Giá trị cộng hoặc tỷ lệ phần trăm; số âm tạo debuff.")] private float value = 2f;
    [SerializeField][Tooltip("Chỉ số cộng thêm trong cùng cấp (cùng một nguồn bonus với chỉ số chính). Bonus chiến đấu (BossDamage…) chỉ dùng Add.")]
    private StatModifier[] extraModifiers = Array.Empty<StatModifier>();
    [SerializeField][Tooltip("% HP tối đa hồi (máu đã mất) ngay khi cấp này được áp, ví dụ Vitality Lv5 hồi đầy = 100. 0 = không.")]
    [Min(0f)] private float restorePercentOnApply;

    /// <summary>Chỉ số nhân vật sẽ bị thay đổi.</summary>
    public StatType Stat => stat;
    /// <summary>Add cộng trực tiếp; Percent thay đổi theo phần trăm.</summary>
    public StatOperation Operation => operation;
    /// <summary>Giá trị cộng hoặc tỷ lệ phần trăm; số âm tạo debuff.</summary>
    public float Value => value;

    public StatModifier Modifier => new StatModifier(stat, operation, value);

    /// <summary>Chỉ số cộng thêm trong cùng cấp; rỗng ở asset cũ.</summary>
    public IReadOnlyList<StatModifier> ExtraModifiers => extraModifiers ?? Array.Empty<StatModifier>();

    /// <summary>% HP tối đa hồi khi cấp này được áp.</summary>
    public float RestorePercentOnApply => restorePercentOnApply;

    /// <summary>Tạo mảng mới gồm chỉ số chính rồi các chỉ số thêm (một nguồn bonus duy nhất của skill).</summary>
    public StatModifier[] CreateModifiers()
    {
        IReadOnlyList<StatModifier> extra = ExtraModifiers;
        var all = new StatModifier[extra.Count + 1];
        all[0] = Modifier;
        for (int index = 0; index < extra.Count; index++) all[index + 1] = extra[index];
        return all;
    }
    public override SkillBehaviourType Type => SkillBehaviourType.StatBuff;

    #endregion

    #region Validation

    // Cùng một bộ kiểm tra với effect và các caller khác.
    public override void Validate()
    {
        Modifier.Validate();
        foreach (StatModifier extra in ExtraModifiers) extra.Validate();
        if (!NonNegative(restorePercentOnApply)) throw new InvalidOperationException("Invalid restore percent.");
    }

    #endregion
}
