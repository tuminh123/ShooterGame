using System;
using UnityEngine;

/// <summary>
/// Bonus chiến đấu RIÊNG của một skill (Holy Bolt +% lên boss, lưỡi hái hút máu), cộng vào bonus của nhân vật
/// ngay lúc tạo gói đòn. Nhờ vậy bonus của skill này không ăn sang skill khác.
/// </summary>
[Serializable]
public struct SkillHitBonus
{
    [Tooltip("% sát thương thêm lên boss/tinh anh của riêng skill này.")]
    [Min(0f)] public float bossDamage;
    [Tooltip("% lượng HP thật đã trừ hồi cho người đánh, riêng skill này.")]
    [Min(0f)] public float lifeSteal;

    /// <summary>Bonus của nhân vật cộng bonus riêng của skill.</summary>
    public CombatBonuses AddTo(CombatBonuses owner)
    {
        owner.bossDamage += bossDamage;
        owner.lifeSteal += lifeSteal;
        return owner;
    }

    /// <summary>Ném lỗi nếu giá trị âm hoặc không hữu hạn.</summary>
    public void Validate()
    {
        if (!(bossDamage >= 0f) || !(lifeSteal >= 0f) || float.IsInfinity(bossDamage) || float.IsInfinity(lifeSteal))
            throw new InvalidOperationException("Invalid skill hit bonus.");
    }
}
