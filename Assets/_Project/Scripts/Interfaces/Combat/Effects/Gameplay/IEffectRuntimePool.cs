#region EffectRuntimeLease

/// <summary>Thuê và reset/trả runtime về đúng pool theo loại effect.</summary>
public interface IEffectRuntimeLease
{
    T Rent<T>()
        where T : EffectRuntime;
    void Return(EffectRuntime runtime);
}

#endregion

#region EffectRuntimeCapacity

/// <summary>Chuẩn bị dung lượng trước gameplay.</summary>
public interface IEffectRuntimeCapacity
{
    void Prewarm<T>(int count)
        where T : EffectRuntime;
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của EffectRuntimePool; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IEffectRuntimePool
{
    IEffectRuntimeLease GetEffectRuntimeLease();
    IEffectRuntimeCapacity GetEffectRuntimeCapacity();
}

#endregion
