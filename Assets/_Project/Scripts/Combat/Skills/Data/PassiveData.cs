using System;
using UnityEngine;

/// <summary>Thông số của <see cref="KillStackSkill"/>: mỗi quái chủ giết được cộng một lớp buff có thời hạn (Soul Harvest).</summary>
/// <remarks>Chỉ đọc. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class KillStackData : SkillBehaviourData
{
    [Tooltip("Chỉ số của MỘT lớp (ví dụ Damage +1%).")]
    [SerializeField] private StatModifier perStack = new StatModifier(StatType.Damage, StatOperation.Percent, 1f);
    [SerializeField, Range(1, 100)] private int maxStacks = 20;
    [Tooltip("Giây giữ buff kể từ lần giết gần nhất; hết giờ thì mất mọi lớp.")][SerializeField, Min(0.1f)] private float duration = 5f;

    public override SkillBehaviourType Type => SkillBehaviourType.KillStack;
    public StatModifier PerStack => perStack;
    public int MaxStacks => maxStacks;
    public float Duration => duration;

    public override void Validate()
    {
        perStack.Validate();
        if (maxStacks < 1 || !Positive(duration)) throw new InvalidOperationException("Invalid kill stack data.");
    }
}

/// <summary>Thông số của <see cref="SkillScalingSkill"/>: chỉ số tăng theo số skill nhận trong trận đang có (Aura of Valor).</summary>
[Serializable]
public sealed class SkillScalingData : SkillBehaviourData
{
    [Tooltip("Chỉ số cho MỖI skill trong trận đang sở hữu (ví dụ Damage +3%).")]
    [SerializeField] private StatModifier perSkill = new StatModifier(StatType.Damage, StatOperation.Percent, 3f);

    public override SkillBehaviourType Type => SkillBehaviourType.SkillScaling;
    public StatModifier PerSkill => perSkill;

    public override void Validate() => perSkill.Validate();
}

/// <summary>Thông số của <see cref="ShieldRegenSkill"/>: không bị đánh một lúc thì hồi giáp (Toughness Lv5).</summary>
[Serializable]
public sealed class ShieldRegenData : SkillBehaviourData
{
    [Tooltip("Số giây không mất HP/giáp trước khi bắt đầu hồi.")][SerializeField, Min(0f)] private float delay = 3f;
    [Tooltip("Lượng giáp hồi mỗi giây (không vượt giáp tối đa).")][SerializeField, Min(0f)] private float perSecond = 1f;

    public override SkillBehaviourType Type => SkillBehaviourType.ShieldRegen;
    public float Delay => delay;
    public float PerSecond => perSecond;

    public override void Validate()
    {
        if (!NonNegative(delay) || !NonNegative(perSecond)) throw new InvalidOperationException("Invalid shield regen data.");
    }
}
