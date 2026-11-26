using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Class nền cho thông số của một hành vi tại một cấp. Mỗi class hành vi (<see cref="Skill"/>) có một class data tương ứng.
/// </summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class con: Unity lưu tên class trong asset skill.</remarks>
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
        // Duyệt bằng chỉ số: foreach trên IReadOnlyList cấp phát enumerator, mà hàm này chạy mỗi lần spawn.
        for (int i = 0; i < effects.Count; i++)
        {
            IGameplayEffect effect = effects[i];
            if (effect == null) throw new InvalidOperationException("Missing effect.");
            effect.Validate();
        }
    }
}
