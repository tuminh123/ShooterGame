using System;
using UnityEngine;

// Cấu hình chọn trong skill; không giữ mục tiêu hay trạng thái của effect đang chạy.
public interface IGameplayEffect
{
    #region Configuration Contract

    float Duration { get; } // Giây tồn tại cho Apply có thời hạn; ApplyMaintained không tự hết hạn theo giá trị này.
    float ApplicationChance { get; } // Xác suất cơ bản 0..100, vẫn dùng khi tắt UseEffectStats.
    bool UseEffectStats { get; } // Có điều chỉnh xác suất theo effectHit của nguồn và kháng của mục tiêu.
    float TickInterval { get; } // 0 nếu không tick; >0 là số giây giữa hai nhịp.
    void Validate(); // Kiểm tra dữ liệu trước khi manager thuê runtime và tạo timer.
    EffectRuntime RentRuntime(EffectRuntimePool pool); // Thuê đúng loại và chép thông số; chưa Apply vào mục tiêu.

    #endregion
}

[Serializable]
public abstract class GameplayEffectData : IGameplayEffect
{
    #region Shared Configuration

    [SerializeField, Min(0.01f)] private float duration = 3f; // Thời gian hiệu lực gameplay, không phải thời gian VFX.
    [SerializeField, Range(0f, 100f)] private float applicationChance = 100f; // Xác suất cơ bản, tính theo phần trăm.
    [SerializeField] private bool useEffectStats = true; // Có tính chính xác hiệu ứng của nguồn và kháng của mục tiêu.

    #endregion

    #region Construction

    // Constructor mặc định giữ cấu hình được khai báo trong skill qua SerializeReference.
    protected GameplayEffectData() { }
    // Cho cấu hình tạo bằng code chọn có tính effectHit/kháng hay không.
    protected GameplayEffectData(bool useEffectStats) { this.useEffectStats = useEffectStats; }

    #endregion

    #region Configuration Access

    public float Duration => duration;
    public float ApplicationChance => applicationChance;
    public bool UseEffectStats => useEffectStats;
    public virtual float TickInterval => 0f; // Không có nhịp định kỳ; thời điểm gỡ do manager/skill quyết định.

    #endregion

    #region Validation And Runtime Factory

    // Attribute Inspector chỉ hỗ trợ nhập liệu; vẫn kiểm tra NaN/infinity và miền giá trị lúc chạy.
    public virtual void Validate()
    {
        if (!(duration > 0f) || float.IsInfinity(duration) || float.IsNaN(applicationChance) ||
            applicationChance < 0f || applicationChance > 100f)
            throw new InvalidOperationException("Invalid gameplay effect duration or chance.");
    }

    public abstract EffectRuntime RentRuntime(EffectRuntimePool pool);

    #endregion
}

[Serializable]
public sealed class SlowEffect : GameplayEffectData
{
    #region Slow Configuration

    [SerializeField, Range(0f, 1f)] private float slowFraction = 0.3f; // 0.3 nghĩa là giảm 30% tốc độ.

    #endregion

    #region Construction

    public SlowEffect() { }
    // Slow duy trì của aura giữ hành vi chắc chắn áp dụng khi vào vùng.
    public SlowEffect(float slowFraction) : base(useEffectStats: false)
    { this.slowFraction = slowFraction; }

    #endregion

    #region Validation And Runtime Factory

    // Fraction dùng 0..1; việc đổi sang modifier phần trăm nằm trong SlowEffectRuntime.Apply.
    public override void Validate()
    {
        base.Validate();
        if (float.IsNaN(slowFraction) || slowFraction < 0f || slowFraction > 1f)
            throw new InvalidOperationException("Slow fraction must be between zero and one.");
    }

    // Cùng cấu hình có thể tạo nhiều runtime cho các mục tiêu/nguồn aura khác nhau.
    public override EffectRuntime RentRuntime(EffectRuntimePool pool)
    {
        SlowEffectRuntime runtime = pool.Rent<SlowEffectRuntime>();
        runtime.SlowFraction = slowFraction;
        return runtime;
    }

    #endregion
}

// Cháy và độc dùng cùng hành vi gây sát thương định kỳ, nhưng là hai loại effect độc lập.
[Serializable]
public abstract class DamageOverTimeEffect : GameplayEffectData
{
    #region Periodic Damage Configuration

    [SerializeField, Min(0.01f)] private float tickInterval = 1f; // Số giây giữa hai lần gây sát thương.
    [SerializeField, Min(0f)] private float damagePerTick = 2f; // Sát thương cố định mỗi nhịp, cấu hình riêng cho skill.
    public override float TickInterval => tickInterval;

    #endregion

    #region Validation And Runtime Factory

    // Interval phải dương; damage bằng 0 hợp lệ nhưng số âm/NaN/infinity bị loại.
    public override void Validate()
    {
        base.Validate();
        if (!(tickInterval > 0f) || float.IsInfinity(tickInterval) ||
            float.IsNaN(damagePerTick) || float.IsInfinity(damagePerTick) || damagePerTick < 0f)
            throw new InvalidOperationException("Invalid periodic damage configuration.");
    }

    // Runtime chụp damage; manager đọc TickInterval từ cấu hình để đăng ký nhịp riêng.
    public override EffectRuntime RentRuntime(EffectRuntimePool pool)
    {
        DamageOverTimeEffectRuntime runtime = pool.Rent<DamageOverTimeEffectRuntime>();
        runtime.DamagePerTick = damagePerTick;
        return runtime;
    }

    #endregion
}

// Hai lựa chọn riêng trong Inspector; hiện dùng chung logic DoT, không có xử lý nguyên tố riêng.
[Serializable]
public sealed class BurnEffect : DamageOverTimeEffect { }

[Serializable]
public sealed class PoisonEffect : DamageOverTimeEffect { }
