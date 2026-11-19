using System;
using UnityEngine;

/// <summary>Thông số của <see cref="RegenerationSkill"/> tại một cấp.</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class RegenerationData : SkillBehaviourData
{
    #region Configuration

    [SerializeField][Tooltip("Lượng HP hồi sau mỗi lần kích hoạt.")][Min(0f)] private float healAmount = 1f;
    [SerializeField][Tooltip("Thời gian tối thiểu giữa hai lần hồi HP.")][Min(0f)] private float cooldown = 2f;

    /// <summary>Lượng HP hồi sau mỗi lần kích hoạt.</summary>
    public float HealAmount => healAmount;

    public override SkillBehaviourType Type => SkillBehaviourType.Regeneration;
    public override float Cooldown => cooldown;

    #endregion

    #region Validation

    // Hồi máu và thời gian phải hữu hạn, không âm.
    public override void Validate()
    {
        if (!NonNegative(healAmount) || !NonNegative(cooldown)) throw new InvalidOperationException("Invalid regeneration data.");
    }
    #endregion
}
