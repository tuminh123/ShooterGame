using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thông số của <see cref="NormalAttackSkill"/> tại một cấp.</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class NormalAttackData : SkillBehaviourData
{
    #region Configuration

    [SerializeField][Tooltip("Thời gian tối thiểu giữa hai lần gây damage thành công.")][Min(0f)] private float cooldown = 0.8f;
    [SerializeField][Tooltip("Khoảng cách tối đa để nhận diện mục tiêu có thể đánh.")][Min(0f)] private float range = 0.2f;
    [Tooltip("Các hiệu ứng áp dụng khi đòn tiếp xúc đánh trúng.")][SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();

    /// <summary>Khoảng cách tối đa để nhận diện mục tiêu có thể đánh.</summary>
    public float Range => range;
    /// <summary>Các hiệu ứng áp dụng khi đòn tiếp xúc đánh trúng.</summary>
    public IReadOnlyList<IGameplayEffect> Effects => effects;

    public override SkillBehaviourType Type => SkillBehaviourType.NormalAttack;
    public override float Cooldown => cooldown;

    #endregion

    #region Validation

    // Kiểm tra toàn bộ trước khi cho hành vi dùng cấu hình.
    public override void Validate()
    {
        if (!NonNegative(cooldown) || !NonNegative(range)) throw new InvalidOperationException("Invalid contact attack data.");
        ValidateEffects(effects);
    }
    #endregion
}
