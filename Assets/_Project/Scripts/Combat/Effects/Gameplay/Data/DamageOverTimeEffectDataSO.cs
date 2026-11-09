using System;
using UnityEngine;

// Dữ liệu DoT SO cũ cho Burn/Poison; pipeline hiện dùng DamageOverTimeEffect nhúng trong skill.
[CreateAssetMenu(menuName = "Shooter Game/Effects/Damage Over Time")]
public sealed class DamageOverTimeEffectDataSO : StatusEffectDataSO
{
    #region Legacy Periodic Damage Configuration

    [SerializeField, Min(0.01f)] private float tickInterval = 1f; // Số giây giữa hai nhịp của cấu hình cũ.
    [SerializeField, Min(0f)] private float damageMultiplierPerTick = 0.2f; // Hệ số damage cũ; khác damagePerTick cố định của cấu hình mới.
    public float TickInterval => tickInterval;
    public float DamageMultiplierPerTick => damageMultiplierPerTick;

    #endregion

    #region Validation

    // Giữ asset hợp lệ; không chuyển hệ số cũ thành sát thương runtime trong lớp này.
    public override void Validate()
    {
        base.Validate();
        if (!(tickInterval > 0f) || float.IsInfinity(tickInterval) ||
            float.IsNaN(damageMultiplierPerTick) || float.IsInfinity(damageMultiplierPerTick) ||
            damageMultiplierPerTick < 0f)
            throw new InvalidOperationException($"Invalid damage over time: {name}.");
    }

    #endregion
}
