using System;
using UnityEngine;

/// <summary>
/// Class nền cho cấu hình hiệu ứng gameplay, chứa các thông số chung: thời hạn, xác suất, có tính chỉ số hiệu ứng hay không.
/// </summary>
/// <remarks>
/// Cấu hình chỉ đọc, chọn trong data của skill bằng <c>[SerializeReference]</c>. Không giữ mục tiêu hay
/// trạng thái của effect đang chạy. Không đổi tên class con vì Unity lưu tên class trong asset.
/// </remarks>
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

    // Cấu hình tạo bằng code đặt thời hạn riêng.
    protected void SetDuration(float seconds) => duration = seconds;

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

/// <summary>Cấu hình làm chậm: giảm tốc độ di chuyển của mục tiêu theo tỉ lệ trong một khoảng thời gian.</summary>
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
        SlowEffectRuntime runtime = pool.RentSlow();
        runtime.SlowFraction = slowFraction;
        return runtime;
    }

    #endregion
}

/// <summary>Class nền cho cấu hình gây sát thương định kỳ (cháy, độc).</summary>
/// <remarks>
/// Cháy và độc dùng cùng runtime (<see cref="DamageOverTimeEffectRuntime"/>), nhưng là hai loại effect độc lập:
/// mục tiêu có thể vừa cháy vừa trúng độc, và mỗi loại hiện màu feedback riêng.
/// </remarks>
[Serializable]
public abstract class DamageOverTimeEffect : GameplayEffectData
{
    #region Periodic Damage Configuration

    [SerializeField, Min(0.01f)] private float tickInterval = 1f; // Số giây giữa hai lần gây sát thương.
    [SerializeField, Min(0f)] private float damagePerTick = 2f; // Sát thương cố định mỗi nhịp, cấu hình riêng cho skill.
    [Tooltip("Sát thương mỗi nhịp theo % Damage của bên gây effect lúc trúng (cộng với Damage Per Tick). 10 = 10% Damage mỗi nhịp.")]
    [SerializeField, Min(0f)] private float damagePercentPerTick;
    [Tooltip("Số lớp cộng dồn tối đa. Trúng lại khi đang có effect: gia hạn và thêm 1 lớp (mỗi lớp gây trọn sát thương một nhịp).")]
    [SerializeField, Min(1)] private int maxStacks = 1;
    public override float TickInterval => tickInterval;

    /// <summary>Số lớp cộng dồn tối đa (1 = không cộng dồn).</summary>
    public int MaxStacks => Mathf.Max(1, maxStacks);

    #endregion

    #region Validation And Runtime Factory

    // Interval phải dương; damage bằng 0 hợp lệ nhưng số âm/NaN/infinity bị loại.
    public override void Validate()
    {
        base.Validate();
        if (!(tickInterval > 0f) || float.IsInfinity(tickInterval) ||
            float.IsNaN(damagePerTick) || float.IsInfinity(damagePerTick) || damagePerTick < 0f ||
            float.IsNaN(damagePercentPerTick) || float.IsInfinity(damagePercentPerTick) || damagePercentPerTick < 0f)
            throw new InvalidOperationException("Invalid periodic damage configuration.");
    }

    // Runtime chụp damage; manager đọc TickInterval từ cấu hình để đăng ký nhịp riêng.
    public override EffectRuntime RentRuntime(EffectRuntimePool pool)
    {
        DamageOverTimeEffectRuntime runtime = pool.RentDamageOverTime();
        runtime.DamagePerTick = damagePerTick;
        runtime.DamagePercentPerTick = damagePercentPerTick;
        runtime.Stacks = 1;
        return runtime;
    }

    #endregion
}

/// <summary>Cháy. Dùng chung logic sát thương định kỳ; class riêng để chọn trong Inspector và hiện feedback riêng.</summary>
[Serializable]
public sealed class BurnEffect : DamageOverTimeEffect { }

/// <summary>Độc. Dùng chung logic sát thương định kỳ; class riêng để chọn trong Inspector và hiện feedback riêng.</summary>
[Serializable]
public sealed class PoisonEffect : DamageOverTimeEffect { }

/// <summary>Chảy máu (cắn của sói). Dùng chung logic sát thương định kỳ.</summary>
[Serializable]
public sealed class BleedEffect : DamageOverTimeEffect { }

/// <summary>
/// Class nền cho effect giữ một trạng thái trên mục tiêu trong suốt thời hạn: choáng, đóng băng, dấu săn.
/// Dùng chung <see cref="ControlEffectRuntime"/>: một modifier chỉ số và/hoặc một nguồn choáng.
/// </summary>
[Serializable]
public abstract class ControlEffectData : GameplayEffectData
{
    protected ControlEffectData() { }
    protected ControlEffectData(float duration) : base(useEffectStats: false) => SetDuration(duration);

    /// <summary>Có khóa đánh và đứng yên hay không.</summary>
    protected abstract bool Stuns { get; }

    /// <summary>Modifier giữ trên mục tiêu; value 0 = không có.</summary>
    protected abstract StatModifier Modifier { get; }

    public override EffectRuntime RentRuntime(EffectRuntimePool pool)
    {
        ControlEffectRuntime runtime = pool.RentControl();
        runtime.Stuns = Stuns;
        runtime.Modifier = Modifier;
        return runtime;
    }
}

/// <summary>Choáng: đứng yên và không đánh trong thời hạn. Boss miễn nhiễm.</summary>
[Serializable]
public sealed class StunEffect : ControlEffectData
{
    public StunEffect() { }
    /// <summary>Cấu hình tạo bằng code (chí mạng gây choáng…): luôn áp, không tính chỉ số hiệu ứng.</summary>
    public StunEffect(float duration) : base(duration) { }
    protected override bool Stuns => true;
    protected override StatModifier Modifier => default;
}

/// <summary>Đóng băng: như choáng, kèm màu băng. Boss miễn nhiễm.</summary>
[Serializable]
public sealed class FreezeEffect : ControlEffectData
{
    protected override bool Stuns => true;
    protected override StatModifier Modifier => default;
}

/// <summary>Dấu săn: mục tiêu nhận thêm % sát thương từ mọi nguồn trong thời hạn.</summary>
[Serializable]
public sealed class MarkEffect : ControlEffectData
{
    [Tooltip("% sát thương nhận thêm khi bị đánh dấu (30 = +30%).")]
    [SerializeField, Min(0f)] private float damageTakenPercent = 30f;
    protected override bool Stuns => false;
    protected override StatModifier Modifier => new StatModifier(StatType.DamageTaken, StatOperation.Add, damageTakenPercent);

    public override void Validate()
    {
        base.Validate();
        if (!(damageTakenPercent >= 0f) || float.IsInfinity(damageTakenPercent))
            throw new InvalidOperationException("Mark damage must be finite and not negative.");
    }
}
