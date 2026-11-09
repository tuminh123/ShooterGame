using System;
using UnityEngine;

// Tween một khung popup bất kỳ: mở nhỏ → phóng vượt → về scale gốc; đóng thu nhỏ → callback UI.
// Không phụ thuộc nội dung Settings, cửa hàng, thông báo hay controller cụ thể.
public sealed class SinglePanelPopupTween : PopupTweenBase
{
    #region Panel Reference

    [SerializeField] private Transform panel; // Khung cần scale; để trống thì dùng Transform của component này.

    #endregion

    #region Initialization

    // UI có thể gán khung bằng code; gọi khi đổi reference, không gọi lại trước mỗi lần mở.
    public void Initialize(Transform target)
    {
        InitializeTargets(new[] { target });
        panel = target;
    }

    // Cho phép gán panel trong Inspector và gọi PlayOpen trực tiếp mà không cần bước khởi tạo riêng.
    private void EnsureInitialized()
    {
        if (!IsInitialized) Initialize(panel != null ? panel : transform);
    }

    #endregion

    #region Open And Close

    public override float PlayOpen(Action onComplete = null)
    {
        EnsureInitialized();
        return OpenTargets(1, 0f, onComplete);
    }

    // Ví dụ UI: popup.PlayClose(() => panelObject.SetActive(false)).
    public override float PlayClose(Action onComplete = null)
    {
        EnsureInitialized();
        return CloseTargets(1, 0f, onComplete);
    }

    #endregion
}
