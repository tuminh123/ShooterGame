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

    /// <summary>Prefab hiển thị vùng aura quanh chủ nhân.</summary>
    public SkillAreaVisual VisualPrefab => visualPrefab;
    /// <summary>Bán kính tác động của aura ở cấp này.</summary>
    public float Radius => radius;
    /// <summary>Tỷ lệ giảm tốc độ mục tiêu trong vùng, từ 0 đến 1.</summary>
    public float SlowFraction => slowFraction;
    /// <summary>Các hiệu ứng áp dụng khi mục tiêu đi vào vùng aura.</summary>
    public IReadOnlyList<IGameplayEffect> Effects => effects;

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
