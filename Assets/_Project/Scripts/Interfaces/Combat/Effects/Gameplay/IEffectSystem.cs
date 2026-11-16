#region EffectApplication

/// <summary>Áp dụng effect có thời hạn hoặc duy trì bằng handle.</summary>
public interface IEffectApplication
{
    bool Apply(IGameplayEffect configuration, CharacterTarget target, CharacterTeam sourceTeam, float effectHit = 0f);
    EffectHandle ApplyMaintained(IGameplayEffect configuration, CharacterTarget target, CharacterTeam sourceTeam, float effectHit = 0f);
}

#endregion

#region EffectHandleControl

/// <summary>Kiểm tra và gỡ đúng phiên effect được handle đại diện.</summary>
public interface IEffectHandleControl
{
    bool IsActive(EffectHandle handle);
    void Remove(EffectHandle handle);
}

#endregion

#region EffectOwnerLifecycle

/// <summary>Pause và dọn effect thuộc đúng lượt spawn của mục tiêu.</summary>
public interface IEffectOwnerLifecycle
{
    void ClearTarget(CharacterView owner, CharacterRuntimeData identity);
    void SetPaused(CharacterView owner, CharacterRuntimeData identity, bool paused);
}

#endregion

#region EffectCleanup

/// <summary>Kết thúc toàn bộ effect đang hoạt động.</summary>
public interface IEffectCleanup
{
    void ClearAll();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của EffectManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IEffectSystem
{
    IEffectApplication GetEffectApplication();
    IEffectHandleControl GetEffectHandleControl();
    IEffectOwnerLifecycle GetEffectOwnerLifecycle();
    IEffectCleanup GetEffectCleanup();
}

#endregion
