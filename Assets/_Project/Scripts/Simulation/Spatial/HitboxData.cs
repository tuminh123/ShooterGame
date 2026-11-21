using System;
using UnityEngine;

/// <summary>Hộp va chạm hình chữ nhật theo local của vỏ nhân vật; SpatialSystem đổi sang world khi chụp snapshot.</summary>
[Serializable]
public struct HitboxData
{
    #region Local Shape

    /// <summary>Kích thước local, độc lập với kích thước sprite.</summary>
    [Tooltip("Chiều rộng/cao hộp va chạm theo đơn vị local của vỏ; phải dương, độc lập với scale của visual prefab.")]
    public Vector2 Size;
    /// <summary>Tâm local; đổi sang world bằng Transform của vỏ, không theo flip visual.</summary>
    [Tooltip("Tâm hộp so với gốc local của vỏ nhân vật. Không tự đổi theo hướng lật sprite; có thể âm hoặc bằng 0.")]
    public Vector2 Offset;

    #endregion

    #region Validation

    /// <summary>Chặn hộp rỗng và giá trị không hợp lệ trước khi đưa hình học vào job.</summary>
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
