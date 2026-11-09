using System;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class EmergencyShieldData : SkillBehaviourData
{
    #region Configuration

    [Range(0f, 100f)] public float healthThreshold = 20f; // Ngưỡng HP phần trăm.
    [Min(0f)] public float shieldAmount = 30f; // Shield mỗi lần kích hoạt.
    [Min(0f)] public float cooldown = 5f; // Khoảng cách giữa hai lần cấp shield.
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
