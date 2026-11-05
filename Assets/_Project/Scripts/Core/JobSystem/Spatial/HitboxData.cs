using System;
using UnityEngine;

[Serializable]
public struct HitboxData
{
    #region Local Shape

    public Vector2 Size; // Kích thước local, độc lập với kích thước sprite.
    public Vector2 Offset; // Tâm local; đổi sang world bằng Transform của vỏ, không theo flip visual.

    #endregion

    #region Validation

    // Chặn hộp rỗng và giá trị không hợp lệ trước khi đưa hình học vào job.
    public void Validate()
    {
        if (!IsFinite(Size.x) || !IsFinite(Size.y) || Size.x <= 0f || Size.y <= 0f ||
            !IsFinite(Offset.x) || !IsFinite(Offset.y))
            throw new InvalidOperationException("Hitbox size must be positive and all hitbox values finite.");
    }

    // Kích thước/offset phải là số thực hữu hạn; offset vẫn được âm hoặc bằng 0.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion
}
