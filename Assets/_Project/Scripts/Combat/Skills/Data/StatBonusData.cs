using System;
using UnityEngine;

/// <summary>Thông số của <see cref="StatBonusSkill"/> tại một cấp: chỉ số nào, cộng thẳng hay phần trăm, giá trị bao nhiêu (âm là debuff).</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
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
