using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thông số của <see cref="FrostAuraSkill"/> tại một cấp: bán kính, mức làm chậm, effect khi vào vùng.</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class FrostAuraData : SkillBehaviourData
{
    #region Configuration

    [SerializeField][Tooltip("Prefab hiển thị vùng aura quanh chủ nhân.")] private SkillAreaVisual visualPrefab;
    [SerializeField][Tooltip("Bán kính tác động của aura ở cấp này.")][Min(0.01f)] private float radius = 1f;
    [SerializeField][Tooltip("Tỷ lệ giảm tốc độ mục tiêu trong vùng, từ 0 đến 1.")][Range(0f, 1f)] private float slowFraction = 0.3f;
    [Tooltip("Các hiệu ứng áp dụng khi mục tiêu đi vào vùng aura.")][SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [Header("Sát thương theo nhịp")]
    [Tooltip("% Damage mỗi nhịp lên đối thủ trong vùng. 0 = không gây sát thương.")][SerializeField, Min(0f)] private float damagePercent;
    [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;
    [Tooltip("Số đối thủ bị đánh mỗi nhịp, chọn ngẫu nhiên trong vùng (giật sét). 0 = mọi đối thủ trong vùng.")]
    [SerializeField, Min(0)] private int targetsPerTick;
    [Tooltip("Effect mỗi nhịp đánh (choáng của vòm sét…).")][SerializeReference] private List<IGameplayEffect> tickEffects = new List<IGameplayEffect>();
    [Tooltip("VFX tại mục tiêu mỗi nhịp đánh; có Zap Line thì kèm đường sét từ chủ.")][SerializeField] private EffectType tickVfx;
    [SerializeField] private bool zapLine;
    [Header("Hồi máu")]
    [Tooltip("% HP tối đa hồi mỗi giây cho chủ.")][SerializeField, Min(0f)] private float healPercentPerSecond;
    [Tooltip("HP đầy thì lượng hồi thừa thành giáp, tối đa % HP tối đa này. 0 = bỏ phần thừa.")][SerializeField, Min(0f)] private float overhealShieldPercent;
    [Header("Nổ theo nhịp")]
    [Tooltip("Giây giữa hai lần nổ quanh chủ; 0 = không nổ.")][SerializeField, Min(0f)] private float pulseInterval;
    [SerializeField, Min(0f)] private float pulseDamagePercent = 150f;
    [Tooltip("Đẩy lùi (dương) mọi đối thủ trong vùng khi nổ.")][SerializeField] private float pulseKnockback;
    [SerializeReference] private List<IGameplayEffect> pulseEffects = new List<IGameplayEffect>();
    [SerializeField] private EffectType pulseVfx = EffectType.IceImpact;

    /// <summary>Prefab hiển thị vùng aura quanh chủ nhân.</summary>
    public SkillAreaVisual VisualPrefab => visualPrefab;
    /// <summary>Bán kính tác động của aura ở cấp này.</summary>
    public float Radius => radius;
    /// <summary>Tỷ lệ giảm tốc độ mục tiêu trong vùng, từ 0 đến 1.</summary>
    public float SlowFraction => slowFraction;
    /// <summary>Các hiệu ứng áp dụng khi mục tiêu đi vào vùng aura.</summary>
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    public float DamageMultiplier => damagePercent / 100f;
    public float TickInterval => tickInterval;
    public int TargetsPerTick => targetsPerTick;
    public IReadOnlyList<IGameplayEffect> TickEffects => tickEffects;
    public EffectType TickVfx => tickVfx;
    public bool ZapLine => zapLine;
    public float HealPercentPerSecond => healPercentPerSecond;
    public float OverhealShieldPercent => overhealShieldPercent;
    public float PulseInterval => pulseInterval;
    public float PulseDamageMultiplier => pulseDamagePercent / 100f;
    public float PulseKnockback => pulseKnockback;
    public IReadOnlyList<IGameplayEffect> PulseEffects => pulseEffects;
    public EffectType PulseVfx => pulseVfx;

    public override SkillBehaviourType Type => SkillBehaviourType.SlowAura;

    #endregion

    #region Validation

    // Slow và bán kính phải hợp lệ trước khi thêm token vào mục tiêu.
    public override void Validate()
    {
        if (visualPrefab == null || !Positive(radius) || !NonNegative(slowFraction) || slowFraction > 1f ||
            !NonNegative(damagePercent) || !Positive(tickInterval) || targetsPerTick < 0 || !NonNegative(healPercentPerSecond) ||
            !NonNegative(overhealShieldPercent) || !NonNegative(pulseInterval) || !NonNegative(pulseDamagePercent) || float.IsNaN(pulseKnockback))
            throw new InvalidOperationException("Invalid frost aura data.");
        ValidateEffects(effects);
        ValidateEffects(tickEffects ?? (tickEffects = new List<IGameplayEffect>()));
        ValidateEffects(pulseEffects ?? (pulseEffects = new List<IGameplayEffect>()));
    }
    #endregion
}
