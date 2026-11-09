using System;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class RegenerationData : SkillBehaviourData
{
    #region Configuration

    [Min(0f)] public float healAmount = 1f; // HP hồi mỗi lần.
    [Min(0f)] public float cooldown = 2f; // Nhịp hồi khi cast thành công.
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
