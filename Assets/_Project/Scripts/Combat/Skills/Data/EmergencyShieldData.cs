using System;
using UnityEngine;

/// <summary>Thông số của <see cref="EmergencyShieldSkill"/> tại một cấp.</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class EmergencyShieldData : SkillBehaviourData
{
    #region Configuration

    [SerializeField][Tooltip("Kích hoạt khi HP hiện tại thấp hơn hoặc bằng tỷ lệ này trên HP tối đa.")][Range(0f, 100f)] private float healthThreshold = 20f;
    [SerializeField][Tooltip("Lượng khiên được cấp mỗi lần kích hoạt.")][Min(0f)] private float shieldAmount = 30f;
    [SerializeField][Tooltip("Thời gian tối thiểu giữa hai lần cấp khiên.")][Min(0f)] private float cooldown = 5f;

    /// <summary>Kích hoạt khi HP hiện tại thấp hơn hoặc bằng tỷ lệ này trên HP tối đa.</summary>
    public float HealthThreshold => healthThreshold;
    /// <summary>Lượng khiên được cấp mỗi lần kích hoạt.</summary>
    public float ShieldAmount => shieldAmount;

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
