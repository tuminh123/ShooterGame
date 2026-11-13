using System;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class EmergencyShieldData : SkillBehaviourData
{
    #region Configuration

    [Tooltip("Kích hoạt khi HP hiện tại thấp hơn hoặc bằng tỷ lệ này trên HP tối đa.")][Range(0f, 100f)] public float healthThreshold = 20f;
    [Tooltip("Lượng khiên được cấp mỗi lần kích hoạt.")][Min(0f)] public float shieldAmount = 30f;
    [Tooltip("Thời gian tối thiểu giữa hai lần cấp khiên.")][Min(0f)] public float cooldown = 5f;
    public override SkillBehaviourType Type => SkillBehaviourType.EmergencyShield;
    public override float Cooldown => cooldown;

    #endregion

    #region Validation

    // Ngưỡng và lượng shield phải hợp lệ trước khi runtime nhận.
    public override void Validate()
    {
        if (!NonNegative(healthThreshold) || healthThreshold > 100f || !NonNegative(shieldAmount) || !NonNegative(cooldown))
            throw new InvalidOperationException("Invalid emergency shield data.");
    }
    #endregion
}
