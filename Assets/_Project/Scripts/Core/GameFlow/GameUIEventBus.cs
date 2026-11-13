using System;
using UnityEngine;

// Bus phát thay đổi state của GameManager cho gameplay và UI.
public static class GameUIEventBus
{
    #region State Notifications

    public static event Action<GameState, GameState> StateChanged; // State trước và sau khi chuyển.

    // GameManager là nơi duy nhất phát state; lỗi một listener không chặn listener khác.
    internal static void Publish(GameState previous, GameState current)
    {
        Action<GameState, GameState> handlers = StateChanged; // Chụp danh sách để listener tự gỡ an toàn khi đang phát.
        if (handlers == null) return;
        foreach (Action<GameState, GameState> handler in handlers.GetInvocationList())
        {
            try { handler(previous, current); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion

    #region Play Session Lifecycle

    // Xóa listener static giữa các phiên Play khi Domain Reload bị tắt.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetListeners() => StateChanged = null;

    #endregion
}
