using System;
using UnityEngine;

// Một tác động stat ở cấp hiện tại, dùng được cho cả buff dương và debuff âm.
[Serializable]
public sealed class StatBonusData : SkillBehaviourData
{
    #region Configuration

    [Tooltip("Chỉ số nhân vật sẽ bị thay đổi.")] public StatType stat = StatType.Defense;
    [Tooltip("Add cộng trực tiếp; Percent thay đổi theo phần trăm.")] public StatOperation operation;
    [Tooltip("Giá trị cộng hoặc tỷ lệ phần trăm; số âm tạo debuff.")] public float value = 2f;
    public StatModifier Modifier => new StatModifier(stat, operation, value);
    public override SkillBehaviourType Type => SkillBehaviourType.StatBuff;

    #endregion

    #region Validation

    // Cùng một bộ kiểm tra với effect và các caller khác.
    public override void Validate() => Modifier.Validate();

    #endregion
}
