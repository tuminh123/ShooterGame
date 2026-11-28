using System;
using System.Collections.Generic;
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
    [SerializeField][Tooltip("% HP tối đa hồi ngay khi kích hoạt (hồi máu đã mất).")][Min(0f)] private float restorePercent;
    [SerializeField][Tooltip("Giây bất tử khi kích hoạt.")][Min(0f)] private float invulnerableSeconds;
    [SerializeField][Tooltip("Số lần kích hoạt tối đa mỗi lượt spawn; 0 = không giới hạn (chỉ theo hồi chiêu).")][Min(0)] private int maxTriggers;
    [SerializeField][Tooltip("Bán kính nổ quanh chủ khi kích hoạt; 0 = không nổ.")][Min(0f)] private float pulseRadius;
    [SerializeField][Min(0f)] private float pulseDamagePercent = 150f;
    [SerializeField][Tooltip("Đẩy lùi đối thủ khi nổ.")] private float pulseKnockback = 0.4f;
    [SerializeReference] private List<IGameplayEffect> pulseEffects = new List<IGameplayEffect>();
    [SerializeField] private EffectType pulseVfx = EffectType.HolyImpact;

    /// <summary>Kích hoạt khi HP hiện tại thấp hơn hoặc bằng tỷ lệ này trên HP tối đa.</summary>
    public float HealthThreshold => healthThreshold;
    /// <summary>Lượng khiên được cấp mỗi lần kích hoạt.</summary>
    public float ShieldAmount => shieldAmount;
    public float RestorePercent => restorePercent;
    public float InvulnerableSeconds => invulnerableSeconds;
    public int MaxTriggers => maxTriggers;
    public float PulseRadius => pulseRadius;
    public float PulseDamageMultiplier => pulseDamagePercent / 100f;
    public float PulseKnockback => pulseKnockback;
    public IReadOnlyList<IGameplayEffect> PulseEffects => pulseEffects ?? (IReadOnlyList<IGameplayEffect>)Array.Empty<IGameplayEffect>();
    public EffectType PulseVfx => pulseVfx;

    public override SkillBehaviourType Type => SkillBehaviourType.EmergencyShield;
    public override float Cooldown => cooldown;

    #endregion

    #region Validation

    // Ngưỡng và lượng shield phải hợp lệ trước khi runtime nhận.
    public override void Validate()
    {
        if (!NonNegative(healthThreshold) || healthThreshold > 100f || !NonNegative(shieldAmount) || !NonNegative(cooldown) ||
            !NonNegative(restorePercent) || !NonNegative(invulnerableSeconds) || maxTriggers < 0 || !NonNegative(pulseRadius) ||
            !NonNegative(pulseDamagePercent) || float.IsNaN(pulseKnockback))
            throw new InvalidOperationException("Invalid emergency shield data.");
        ValidateEffects(PulseEffects);
    }
    #endregion
}
