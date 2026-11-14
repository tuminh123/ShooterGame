using System;
using UnityEngine;
using UnityEngine.Serialization;

// Dữ liệu SO cũ được giữ để không mất asset. Skill mới chọn IGameplayEffect qua SerializeReference.
public abstract class StatusEffectDataSO : ScriptableObject
{
    #region Legacy Asset Configuration

    [Tooltip("Thời hạn hiệu ứng trong asset SO cũ, tính bằng giây gameplay; phải lớn hơn 0. Pipeline skill mới dùng effect nhúng trong Behaviour Data.")]
    [SerializeField, Min(0.01f)] private float duration = 3f; // Thời hạn lưu trong asset cũ, tính bằng giây.
    [Tooltip("Xác suất gốc theo thang 0..100: 25 = 25%, 100 = luôn thành công trước điều chỉnh chỉ số.")]
    [SerializeField, Range(0f, 100f)] private float applicationChance = 100f; // Thang phần trăm 0..100.
    // Giữ dữ liệu đã serialize dưới tên cũ khi đổi tên field.
    [FormerlySerializedAs("useAccuracyAndDodge")]
    [Tooltip("Cờ của dữ liệu cũ cho phép tính thêm Effect Hit/Effect Resistance. Asset này không tự tạo effect runtime trong pipeline skill mới.")]
    [SerializeField] private bool useEffectStats = true;

    #endregion

    #region Configuration Access

    public float Duration => duration;
    public float ApplicationChance => applicationChance;
    public bool UseEffectStats => useEffectStats;

    #endregion

    #region Validation

    // Chỉ kiểm tra asset cũ; lớp này không triển khai IGameplayEffect và không tạo runtime cho pipeline mới.
    public virtual void Validate()
    {
        if (!(duration > 0f) || float.IsInfinity(duration) || float.IsNaN(applicationChance) ||
            applicationChance < 0f || applicationChance > 100f)
            throw new InvalidOperationException($"Invalid status effect: {name}.");
    }

    #endregion
}
