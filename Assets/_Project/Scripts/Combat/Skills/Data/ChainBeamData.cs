using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thông số của <see cref="ChainSkill"/>: đánh quái gần nhất trong tầm chiến đấu rồi nảy sang quái kế tiếp.</summary>
/// <remarks>Chỉ đọc. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class ChainData : SkillBehaviourData
{
    [SerializeField, Min(0f)] private float cooldown = 1.6f;
    [Tooltip("% Damage của cú đánh đầu.")][SerializeField, Min(0f)] private float damagePercent = 100f;
    [Tooltip("Số lần nảy sau cú đầu.")][SerializeField, Range(0, 32)] private int jumps = 2;
    [Tooltip("Tầm tìm mục tiêu kế tiếp từ mục tiêu vừa trúng (world unit).")][SerializeField, Min(0.05f)] private float jumpRange = 0.5f;
    [Tooltip("% sát thương giảm sau mỗi lần nảy (15 = mỗi lần còn 85%). 0 = không giảm.")][SerializeField, Range(0f, 100f)] private float falloffPercent = 15f;
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [SerializeField] private EffectType hitVfx = EffectType.LightningImpact;
    [SerializeField] private Color lineColor = new Color(0.55f, 0.85f, 1f, 1f);
    [SerializeField, Min(0.005f)] private float lineWidth = 0.035f;

    public override SkillBehaviourType Type => SkillBehaviourType.Chain;
    public override float Cooldown => cooldown;
    public float DamageMultiplier => damagePercent / 100f;
    public int Jumps => jumps;
    public float JumpRange => jumpRange;
    public float Falloff => falloffPercent / 100f;
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    public EffectType HitVfx => hitVfx;
    public Color LineColor => lineColor;
    public float LineWidth => lineWidth;

    public override void Validate()
    {
        if (!NonNegative(cooldown) || !NonNegative(damagePercent) || !Positive(jumpRange) || !Positive(lineWidth))
            throw new InvalidOperationException("Invalid chain data.");
        ValidateEffects(effects);
    }
}

/// <summary>Thông số của <see cref="BeamSkill"/>: tia dài theo hướng nhìn (hoặc xoay quanh người), gây sát thương theo nhịp.</summary>
/// <remarks>Chỉ đọc. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class BeamData : SkillBehaviourData
{
    [SerializeField, Min(0f)] private float cooldown = 3f;
    [Tooltip("Thời gian tia tồn tại mỗi lần dùng (giây).")][SerializeField, Min(0.05f)] private float duration = 1f;
    [Tooltip("Giây giữa hai lần gây đòn.")][SerializeField, Min(0.02f)] private float tickInterval = 0.2f;
    [Tooltip("% Damage mỗi nhịp.")][SerializeField, Min(0f)] private float damagePercent = 50f;
    [Tooltip("Độ dài tia = tầm chiến đấu × hệ số này.")][SerializeField, Min(0.05f)] private float lengthScale = 1f;
    [Tooltip("Nửa bề rộng tia (world unit).")][SerializeField, Min(0.005f)] private float width = 0.05f;
    [Tooltip("Tốc độ xoay của tia (độ/giây). 0 = luôn theo hướng nhìn của chủ.")][SerializeField] private float rotateDegreesPerSecond;
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [SerializeField] private Color color = new Color(1f, 0.92f, 0.45f, 0.9f);

    public override SkillBehaviourType Type => SkillBehaviourType.Beam;
    public override float Cooldown => cooldown;
    public float Duration => duration;
    public float TickInterval => tickInterval;
    public float DamageMultiplier => damagePercent / 100f;
    public float LengthScale => lengthScale;
    public float Width => width;
    public float RotateDegreesPerSecond => rotateDegreesPerSecond;
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    public Color Color => color;

    public override void Validate()
    {
        if (!NonNegative(cooldown) || !Positive(duration) || !Positive(tickInterval) || !NonNegative(damagePercent) ||
            !Positive(lengthScale) || !Positive(width) || float.IsNaN(rotateDegreesPerSecond) || float.IsInfinity(rotateDegreesPerSecond))
            throw new InvalidOperationException("Invalid beam data.");
        ValidateEffects(effects);
    }
}
