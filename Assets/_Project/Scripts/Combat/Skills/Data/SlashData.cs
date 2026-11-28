using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thông số của <see cref="SlashSkill"/>: chém quanh chủ (hình quạt hoặc 360°), có thể là vòng sóng lan dần,
/// nhiều đợt liên tiếp, kéo quái vào trước khi chém, hút máu riêng.
/// </summary>
/// <remarks>Chỉ đọc. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class SlashData : SkillBehaviourData
{
    #region Configuration

    [SerializeField, Min(0f)] private float cooldown = 1.2f;
    [SerializeField, Min(0f)] private float damagePercent = 150f;
    [Tooltip("Luật chọn hướng chém (hướng nhìn, quái gần nhất…). Chém 360° vẫn dùng để biết có mục tiêu hay không.")]
    [SerializeReference] private ITargetSelector target = new FacingDirection();
    [Tooltip("Bật: không có mục tiêu trong tầm thì không chém (không tiêu cooldown).")]
    [SerializeField] private bool requiresTarget = true;
    [Tooltip("Tầm chém / bán kính cuối của vòng sóng (world unit).")][SerializeField, Min(0.05f)] private float radius = 0.4f;
    [Tooltip("Bật: bán kính = tầm chiến đấu × Radius Scale thay vì Radius (sóng lan hết tầm).")]
    [SerializeField] private bool radiusFromCombatRange;
    [SerializeField, Min(0.05f)] private float radiusScale = 1f;
    [Tooltip("Góc quạt (độ); 360 = quanh người.")][SerializeField, Range(10f, 360f)] private float arcDegrees = 120f;
    [Tooltip("Số hướng chém mỗi đợt, chia đều 360° (2 = trước + sau, 4 = bốn hướng).")][SerializeField, Range(1, 8)] private int directions = 1;
    [Tooltip("> 0: vòng sóng lan từ 0 tới bán kính trong số giây này; mỗi mục tiêu trúng một lần mỗi đợt.")]
    [SerializeField, Min(0f)] private float expandDuration;
    [Tooltip("Số đợt chém mỗi lần dùng.")][SerializeField, Range(1, 8)] private int waves = 1;
    [Tooltip("Giây giữa hai đợt.")][SerializeField, Min(0f)] private float waveInterval = 0.25f;
    [Tooltip("Trước đợt đầu: kéo đối thủ trong bán kính này về phía chủ. 0 = không kéo.")][SerializeField, Min(0f)] private float pullRadius;
    [Tooltip("Quãng kéo tối đa (world unit).")][SerializeField, Min(0f)] private float pullDistance = 0.3f;
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [Tooltip("Đẩy lùi (dương) / kéo (âm) mỗi mục tiêu trúng.")][SerializeField] private float knockback;
    [SerializeField] private SkillHitBonus hitBonus;
    [Tooltip("VFX tại điểm giữa mỗi hướng chém (chém quạt).")][SerializeField] private EffectType slashVfx = EffectType.WindImpact;
    [Tooltip("Màu vòng sóng / vòng chém 360° vẽ trên đất.")][SerializeField] private Color ringColor = new Color(1f, 0.9f, 0.5f, 0.35f);
    [Tooltip("Tác động tại chỗ chủ đứng khi đợt cuối xong (vùng thánh…).")]
    [SerializeReference] private List<ISkillImpact> onFinish = new List<ISkillImpact>();

    public override SkillBehaviourType Type => SkillBehaviourType.Slash;
    public override float Cooldown => cooldown;
    public float DamageMultiplier => damagePercent / 100f;
    public ITargetSelector Target => target;
    public bool RequiresTarget => requiresTarget;
    public float ArcDegrees => arcDegrees;
    public int Directions => directions;
    public float ExpandDuration => expandDuration;
    public int Waves => waves;
    public float WaveInterval => waveInterval;
    public float PullRadius => pullRadius;
    public float PullDistance => pullDistance;
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    public float Knockback => knockback;
    public SkillHitBonus HitBonus => hitBonus;
    public EffectType SlashVfx => slashVfx;
    public Color RingColor => ringColor;
    public IReadOnlyList<ISkillImpact> OnFinish => onFinish;

    /// <summary>Bán kính chém thật với tầm chiến đấu hiện tại của chủ.</summary>
    public float RadiusFor(float combatRange) => radiusFromCombatRange ? combatRange * radiusScale : radius;

    #endregion

    #region Validation

    public override void Validate()
    {
        if (!NonNegative(cooldown) || !NonNegative(damagePercent) || target == null || !Positive(radius) || !Positive(radiusScale) ||
            !NonNegative(expandDuration) || !NonNegative(waveInterval) || !NonNegative(pullRadius) || !NonNegative(pullDistance) ||
            float.IsNaN(knockback) || directions < 1 || waves < 1)
            throw new InvalidOperationException("Invalid slash data.");
        target.Validate();
        hitBonus.Validate();
        ValidateEffects(effects);
        SkillImpacts.Validate(onFinish);
    }

    #endregion
}
