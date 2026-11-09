using System;
using System.Collections.Generic;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class FrostAuraData : SkillBehaviourData
{
    #region Configuration

    public SkillAreaVisual visualPrefab; // Visual của vùng tác động.
    [Min(0.01f)] public float radius = 1f; // Bán kính ở cấp này.
    [Range(0f, 1f)] public float slowFraction = 0.3f; // Tỷ lệ làm chậm duy trì khi còn trong vùng.
    [SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>(); // Effect bổ sung lúc vào vùng.
    public override SkillBehaviourType Type => SkillBehaviourType.SlowAura;

    #endregion

    #region Validation

    // Slow và bán kính phải hợp lệ trước khi thêm token vào mục tiêu.
    public override void Validate()
    {
        if (visualPrefab == null || !Positive(radius) || !NonNegative(slowFraction) || slowFraction > 1f)
            throw new InvalidOperationException("Invalid frost aura data.");
        ValidateEffects(effects);
    }
    #endregion
}
