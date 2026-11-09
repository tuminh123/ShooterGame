using System;
using UnityEngine;

// Một tác động stat ở cấp hiện tại, dùng được cho cả buff dương và debuff âm.
[Serializable]
public sealed class StatBonusData : SkillBehaviourData
{
    #region Configuration

    public StatType stat = StatType.Defense; // Enum chung với toàn bộ hệ stat; giữ thứ tự asset cũ.
    public StatOperation operation; // Add hoặc Percent, mặc định Add để giữ hành vi asset hiện có.
    public float value = 2f; // Ví dụ Add +10 damage, Percent -20 damage, Add -10 điểm crit.
    public StatModifier Modifier => new StatModifier(stat, operation, value);
    public override SkillBehaviourType Type => SkillBehaviourType.StatBuff;

    #endregion

    #region Validation

    // Cùng một bộ kiểm tra với effect và các caller khác.
    public override void Validate() => Modifier.Validate();

    #endregion
}
