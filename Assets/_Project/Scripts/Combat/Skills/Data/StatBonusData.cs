using System;
using UnityEngine;

/// <summary>Thông số của <see cref="StatBonusSkill"/> tại một cấp: chỉ số nào, cộng thẳng hay phần trăm, giá trị bao nhiêu (âm là debuff).</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class StatBonusData : SkillBehaviourData
{
    #region Configuration

    [SerializeField][Tooltip("Chỉ số nhân vật sẽ bị thay đổi.")] private StatType stat = StatType.Defense;
    [SerializeField][Tooltip("Add cộng trực tiếp; Percent thay đổi theo phần trăm.")] private StatOperation operation;
    [SerializeField][Tooltip("Giá trị cộng hoặc tỷ lệ phần trăm; số âm tạo debuff.")] private float value = 2f;

    /// <summary>Chỉ số nhân vật sẽ bị thay đổi.</summary>
    public StatType Stat => stat;
    /// <summary>Add cộng trực tiếp; Percent thay đổi theo phần trăm.</summary>
    public StatOperation Operation => operation;
    /// <summary>Giá trị cộng hoặc tỷ lệ phần trăm; số âm tạo debuff.</summary>
    public float Value => value;

    public StatModifier Modifier => new StatModifier(stat, operation, value);
    public override SkillBehaviourType Type => SkillBehaviourType.StatBuff;

    #endregion

    #region Validation

    // Cùng một bộ kiểm tra với effect và các caller khác.
    public override void Validate() => Modifier.Validate();

    #endregion
}
