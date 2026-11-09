using System;
using System.Collections.Generic;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class NormalAttackData : SkillBehaviourData
{
    #region Configuration

    [Min(0f)] public float cooldown = 0.8f; // Nhịp gây damage khi chạm.
    [Min(0f)] public float range = 0.2f; // Tầm tìm mục tiêu.
    [SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>(); // Effect riêng của đòn tiếp xúc.
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
