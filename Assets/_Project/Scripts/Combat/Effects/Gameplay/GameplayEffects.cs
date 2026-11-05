using System;
using UnityEngine;

// Cấu hình chọn trong skill; không giữ mục tiêu hay trạng thái của effect đang chạy.
public interface IGameplayEffect
{
    float Duration { get; }
    float ApplicationChance { get; }
    bool UseEffectStats { get; }
    float TickInterval { get; }
    void Validate();
    EffectRuntime RentRuntime(EffectRuntimePool pool);
}

[Serializable]
public abstract class GameplayEffectData : IGameplayEffect
{
    [SerializeField, Min(0.01f)] private float duration = 3f; // Thời gian hiệu lực gameplay, không phải thời gian VFX.
    [SerializeField, Range(0f, 100f)] private float applicationChance = 100f; // Xác suất cơ bản, tính theo phần trăm.
    [SerializeField] private bool useEffectStats = true; // Có tính chính xác hiệu ứng của nguồn và kháng của mục tiêu.

    public float Duration => duration;
    public float ApplicationChance => applicationChance;
    public bool UseEffectStats => useEffectStats;
    public virtual float TickInterval => 0f; // 0: chỉ chờ hết hạn, không có nhịp định kỳ.

    public virtual void Validate()
    {
        if (!(duration > 0f) || float.IsInfinity(duration) || float.IsNaN(applicationChance) ||
            applicationChance < 0f || applicationChance > 100f)
            throw new InvalidOperationException("Invalid gameplay effect duration or chance.");
    }

    public abstract EffectRuntime RentRuntime(EffectRuntimePool pool);
}

[Serializable]
public sealed class SlowEffect : GameplayEffectData
{
    [SerializeField, Range(0f, 1f)] private float slowFraction = 0.3f; // 0.3 nghĩa là giảm 30% tốc độ.

    public override void Validate()
    {
        base.Validate();
        if (float.IsNaN(slowFraction) || slowFraction < 0f || slowFraction > 1f)
            throw new InvalidOperationException("Slow fraction must be between zero and one.");
    }

    public override EffectRuntime RentRuntime(EffectRuntimePool pool)
    {
        SlowEffectRuntime runtime = pool.Rent<SlowEffectRuntime>();
        runtime.SlowFraction = slowFraction;
        return runtime;
    }
}

// Cháy và độc dùng cùng hành vi gây sát thương định kỳ, nhưng là hai loại effect độc lập.
[Serializable]
public abstract class DamageOverTimeEffect : GameplayEffectData
{
    [SerializeField, Min(0.01f)] private float tickInterval = 1f; // Số giây giữa hai lần gây sát thương.
    [SerializeField, Min(0f)] private float damagePerTick = 2f; // Sát thương cố định mỗi nhịp, cấu hình riêng cho skill.
    public override float TickInterval => tickInterval;

    public override void Validate()
    {
        base.Validate();
        if (!(tickInterval > 0f) || float.IsInfinity(tickInterval) ||
            float.IsNaN(damagePerTick) || float.IsInfinity(damagePerTick) || damagePerTick < 0f)
            throw new InvalidOperationException("Invalid periodic damage configuration.");
    }

    public override EffectRuntime RentRuntime(EffectRuntimePool pool)
    {
        DamageOverTimeEffectRuntime runtime = pool.Rent<DamageOverTimeEffectRuntime>();
        runtime.DamagePerTick = damagePerTick;
        return runtime;
    }
}

[Serializable]
public sealed class BurnEffect : DamageOverTimeEffect { }

[Serializable]
public sealed class PoisonEffect : DamageOverTimeEffect { }
