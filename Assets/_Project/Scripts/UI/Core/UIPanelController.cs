using System;
using UnityEngine;

/// <summary>Hợp đồng hiển thị chung: UIManager không cần biết dependency hay logic riêng của từng panel.</summary>
public abstract class UIPanelController : MonoBehaviour
{
    #region Flow Requests

    /// <summary>Gửi ý định mở/đóng; UIManager quyết định thứ tự hiển thị.</summary>
    public event Action<UIPanelController, bool> VisibilityRequested;
    /// <summary>Giữ yêu cầu ngay cả khi UI đang bị ẩn hoặc một panel khác đè lên.</summary>
    public bool WantsVisible { get; private set; }

    // Controller thông báo nhu cầu; không tham chiếu hoặc tự quyết định thay UIManager.
    protected void RequestVisibility(bool visible)
    {
        if (WantsVisible == visible) return;
        WantsVisible = visible;
        VisibilityRequested?.Invoke(this, visible);
    }

    /// <summary>Manager đồng bộ yêu cầu khi điều hướng trực tiếp bằng Open/Close/Back, không phát lại event gây vòng lặp.</summary>
    internal void RecordVisibilityRequest(bool visible) => WantsVisible = visible;

    #endregion

    #region Placement (khai báo cho UIManager)

    /// <summary>
    /// True với panel nền dùng chung (thanh bar, nền): hiện song song với màn đang mở, không nằm trong stack điều hướng.
    /// UIManager bật/tắt nó theo <see cref="ShowsInState"/>, nên scene không cần tắt tay.
    /// </summary>
    public virtual bool IsChrome => false;

    /// <summary>Chỉ có nghĩa khi <see cref="IsChrome"/>: panel nền hiện ở những trạng thái game nào.</summary>
    public virtual bool ShowsInState(GameState state) => false;

    /// <summary>
    /// True với màn là một tab của menu (Character, Equipment): mở tab này khi tab khác đang đứng trên cùng thì thay thế nó
    /// trong một lần cập nhật, để màn dưới (Home) không bật lại giữa hai tab.
    /// </summary>
    public virtual bool IsMenuTab => false;

    /// <summary>
    /// True với popup đè lên màn đang hiện (quà đăng nhập, các panel nhận quà sau này): không làm ẩn màn dưới và không vào
    /// stack điều hướng. UIManager đóng nó khi màn dưới đổi (mở màn khác, đổi state, Back). Mỗi lúc chỉ một overlay.
    /// </summary>
    public virtual bool IsOverlay => false;

    /// <summary>
    /// UIManager đặt trước mỗi lần gọi <see cref="SetVisible"/>: true khi đổi do điều hướng trong menu (nên trượt),
    /// false khi đổi do state game hoặc lúc khởi động (bật/tắt ngay, màn đang tối hoặc chưa hiện).
    /// </summary>
    protected bool PlayTransition { get; private set; }

    /// <summary>Đường duy nhất UIManager dùng để đổi hiển thị: ghi cờ trình bày rồi gọi <see cref="SetVisible"/>.</summary>
    internal void Present(bool visible, bool animate)
    {
        PlayTransition = animate;
        // Object chứa controller lỡ tắt trong scene thì bật lại trước khi hiện: nút, event và Redraw của controller chỉ chạy khi nó bật.
        // Nhờ vậy scene không cần bật sẵn thứ gì; panel hiện cho đúng cách riêng của nó ngay sau đó trong SetVisible.
        if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
        SetVisible(visible);
    }

    /// <summary>
    /// Hiện/ẩn cả màn theo cờ <see cref="PlayTransition"/>: có SlideTransition thì trượt khi điều hướng menu, bật/tắt ngay khi
    /// đổi state hoặc khởi động; không có SlideTransition thì SetActive. Bật/tắt ngay giúp màn lỡ để bật trong scene
    /// tắt hẳn lúc khởi động chứ không phụ thuộc một tween có thể bị ngắt.
    /// </summary>
    protected void ApplyScreenVisibility(bool visible)
    {
        if (!TryGetComponent(out SlideTransition slide)) gameObject.SetActive(visible);
        else if (!PlayTransition) slide.SetVisibleInstant(visible);
        else if (visible) slide.PlayIn();
        else slide.PlayOut();
    }

    #endregion

    #region Visibility Contract

    /// <summary>Controller tự quyết định cách ẩn/hiện và bảo toàn trạng thái riêng của UI mà nó quản lý.</summary>
    public abstract void SetVisible(bool visible);

    #endregion
}
