using System;

#region GameplayPause

/// <summary>Giữ token pause hoặc slow motion độc lập với các nguồn khác.</summary>
public interface IGameplayPause
{
    IDisposable Acquire();
    IDisposable AcquireSlowMotion(float multiplier);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của GameplayPauseService; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IGameplayPauseService
{
    IGameplayPause GetGameplayPause();
}

#endregion
