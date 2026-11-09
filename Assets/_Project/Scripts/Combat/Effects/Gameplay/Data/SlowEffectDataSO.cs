using System;
using UnityEngine;

// Dữ liệu slow SO cũ; skill hiện dùng SlowEffect nhúng qua SerializeReference.
[CreateAssetMenu(menuName = "Shooter Game/Effects/Slow")]
public sealed class SlowEffectDataSO : StatusEffectDataSO
{
    #region Legacy Slow Configuration

    [SerializeField, Range(0f, 1f)] private float slowFraction = 0.3f; // 0.3 nghĩa là giảm 30% tốc độ.
    public float SlowFraction => slowFraction;

    #endregion

    #region Validation

    // Kiểm tra cả cấu hình chung và tỷ lệ slow riêng của asset.
    public override void Validate()
    {
        base.Validate();
        if (float.IsNaN(slowFraction) || slowFraction < 0f || slowFraction > 1f)
            throw new InvalidOperationException($"Invalid slow fraction: {name}.");
    }

    #endregion
}
