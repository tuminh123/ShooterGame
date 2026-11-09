using System;
using UnityEngine;

// Hợp đồng hiển thị chung: UIManager không cần biết dependency hay logic riêng của từng panel.
public abstract class UIPanelController : MonoBehaviour
{
    #region Flow Requests

    public event Action<UIPanelController, bool> VisibilityRequested; // Gửi ý định mở/đóng; UIManager quyết định thứ tự hiển thị.
    public bool WantsVisible { get; private set; } // Giữ yêu cầu ngay cả khi UI đang bị ẩn hoặc một panel khác đè lên.

    // Controller thông báo nhu cầu; không tham chiếu hoặc tự quyết định thay UIManager.
    protected void RequestVisibility(bool visible)
    {
        if (WantsVisible == visible) return;
        WantsVisible = visible;
        VisibilityRequested?.Invoke(this, visible);
    }

    // Manager đồng bộ yêu cầu khi điều hướng trực tiếp bằng Open/Close/Back, không phát lại event gây vòng lặp.
    internal void RecordVisibilityRequest(bool visible) => WantsVisible = visible;

    #endregion

    #region Visibility Contract

    // Controller tự quyết định cách ẩn/hiện và bảo toàn trạng thái riêng của UI mà nó quản lý.
    public abstract void SetVisible(bool visible);

    #endregion
}
