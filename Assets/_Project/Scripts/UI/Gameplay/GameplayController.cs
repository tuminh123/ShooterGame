using UnityEngine;

// Gắn trên GameplayUI luôn hoạt động; chỉ bật/tắt panel con để controller giữ đúng vòng đời.
public sealed class GameplayController : UIPanelController
{
    #region Scene References

    [SerializeField] private GameObject panel; // Root HUD con chứa RunUI, nút pause và các thành phần gameplay.
    [SerializeField] private PauseUI pauseUI; // Pause thủ công thuộc gameplay; trả token trước khi HUD bị ẩn.

    #endregion

    #region Visibility And Lifecycle

    // Nhận điều phối từ UIManager; dữ liệu HUD vẫn được RunUI và các view con cập nhật độc lập.
    public override void SetVisible(bool visible)
    {
        if (!visible && pauseUI != null) pauseUI.Resume();
        if (panel != null) panel.SetActive(visible);
    }

    // Dọn pause và ẩn HUD khi root controller hoặc cả Canvas bị tắt.
    private void OnDisable() => SetVisible(false);

    #endregion
}
