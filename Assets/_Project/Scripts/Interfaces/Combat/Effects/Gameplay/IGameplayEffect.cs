#region GameplayEffectSettings

/// <summary>Đọc thời hạn, nhịp và xác suất áp dụng effect.</summary>
public interface IGameplayEffectSettings
{
    float Duration { get; }
    float ApplicationChance { get; }
    bool UseEffectStats { get; }
    float TickInterval { get; }
}

#endregion

#region GameplayEffectValidation

/// <summary>Kiểm tra thông số gameplay effect trước khi áp dụng.</summary>
public interface IGameplayEffectValidation
{
    void Validate();
}

#endregion

#region GameplayEffectFactory

/// <summary>Thuê đúng runtime và sao chép thông số effect vào runtime.</summary>
public interface IGameplayEffectFactory
{
    EffectRuntime RentRuntime(IEffectRuntimeLease pool);
}

#endregion

#region Aggregate Contract

/// <summary>Gom các vai trò của GameplayEffect; không thêm hành vi.</summary>
public interface IGameplayEffect
{
    IGameplayEffectSettings GetSettings();
    IGameplayEffectValidation GetValidation();
    IGameplayEffectFactory GetRuntimeFactory();
}

#endregion
