using System;
using UnityEngine;

// Bus chỉ thông báo cho UI. Trạng thái hiện tại và quyền chuyển trạng thái thuộc GameManager.
public static class GameUIEventBus
{
    #region UI Notifications

    public static event Action<GameState, GameState> StateChanged; // Trạng thái cũ/mới; UI tự chọn cách trình bày.

    // Chỉ GameManager công bố chuyển trạng thái; lỗi một listener không chặn các UI còn lại.
    internal static void Publish(GameState previous, GameState current)
    {
        Action<GameState, GameState> handlers = StateChanged; // Snapshot để listener tự gỡ trong callback vẫn duyệt an toàn.
        if (handlers == null) return;
        foreach (Action<GameState, GameState> handler in handlers.GetInvocationList())
        {
            try { handler(previous, current); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion

    #region Play Session Lifecycle

    // Domain Reload tắt vẫn phải xóa listener static từ phiên Play trước; bus không giữ state hoặc reference manager.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetListeners() => StateChanged = null;

    #endregion
}