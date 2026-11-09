using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class SkillBehaviourData
{
    public abstract SkillBehaviourType Type { get; } // Do class quyết định, không nhập enum thủ công.
    public virtual float Cooldown => 0f; // Hành vi liên tục/buff không cần cấu hình cooldown.

    // Kiểm tra thông số riêng của từng loại trước khi tạo/cập nhật hành vi.
    public abstract void Validate();

    // Thời gian, khoảng cách phải hữu hạn và không âm.
    protected static bool NonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    // Kích thước/tốc độ bắt buộc lớn hơn không.
    protected static bool Positive(float value) => NonNegative(value) && value > 0f;

    // Effect là config chỉ đọc; mỗi hành vi chỉ áp dụng danh sách của nó.
    protected static void ValidateEffects(IReadOnlyList<IGameplayEffect> effects)
    {
        if (effects == null) throw new InvalidOperationException("Missing effect list.");
        foreach (IGameplayEffect effect in effects)
        {
            if (effect == null) throw new InvalidOperationException("Missing effect.");
            effect.Validate();
        }
    }
}
