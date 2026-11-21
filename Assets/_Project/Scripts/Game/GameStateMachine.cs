using System;
using UnityEngine;

/// <summary>
/// Giữ <see cref="GameState"/> hiện tại và phát thay đổi cho UI. Không quyết định luồng — GameManager quyết định.
/// </summary>
/// <remarks>Do <see cref="GameManager"/> tạo; tách ra để GameManager chỉ còn điều phối các miền khác.</remarks>
public sealed class GameStateMachine
{
    #region Properties / Events

    /// <summary>Nguồn state duy nhất; UI chỉ đọc.</summary>
    public GameState State { get; private set; } = GameState.Boot;

    /// <summary>(state trước, state sau), phát sau khi <see cref="State"/> đã được ghi.</summary>
    public event Action<GameState, GameState> StateChanged;

    #endregion

    #region Behaviour

    /// <summary>Ghi state mới rồi phát; trùng state cũ thì không làm gì.</summary>
    public void Change(GameState next)
    {
        if (State == next) return;
        GameState previous = State; // Chụp state cũ trước khi phát cho listener.
        State = next;
        Publish(previous, next);
    }

    /// <summary>Gỡ mọi listener khi scene kết thúc (UI bị hủy cùng scene).</summary>
    public void ClearListeners() => StateChanged = null;

    #endregion

    #region Private Helpers

    // Lỗi của một listener không được chặn các listener còn lại, nếu không một panel UI lỗi
    // có thể khiến các panel khác kẹt ở state cũ.
    private void Publish(GameState previous, GameState next)
    {
        Action<GameState, GameState> handlers = StateChanged;
        if (handlers == null) return;
        foreach (Action<GameState, GameState> handler in handlers.GetInvocationList())
        {
            try { ((Action<GameState, GameState>)handler)(previous, next); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion
}
