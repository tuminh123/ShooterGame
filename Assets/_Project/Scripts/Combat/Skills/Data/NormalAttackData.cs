using System;
using System.Collections.Generic;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class NormalAttackData : SkillBehaviourData
{
    #region Configuration

    [Tooltip("Thời gian tối thiểu giữa hai lần gây damage thành công.")][Min(0f)] public float cooldown = 0.8f;
    [Tooltip("Khoảng cách tối đa để nhận diện mục tiêu có thể đánh.")][Min(0f)] public float range = 0.2f;
    [Tooltip("Các hiệu ứng áp dụng khi đòn tiếp xúc đánh trúng.")][SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>();
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
