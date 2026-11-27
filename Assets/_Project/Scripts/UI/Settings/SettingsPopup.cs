using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup Settings mở từ nút menu (ba gạch) trên thanh trên. Class này chỉ lo mở/đóng; ba công tắc Sound/Music/Vibration
/// do <see cref="AudioSettingsPanel"/> trên Group_List điều khiển.
/// </summary>
/// <remarks>
/// Là overlay do UIManager quản lý (<see cref="IsOverlay"/>): đè lên màn đang hiện (Home, Heroes, Gear, Shop) mà không ẩn nó,
/// UIManager tắt lúc khởi động và tự bật object khi mở (scene lưu bật hay tắt đều như nhau), Back đóng popup trước,
/// đổi màn hoặc vào trận thì popup đóng theo. Nút mở nằm ở HomeNavigationBar (object luôn bật).
/// </remarks>
public sealed class SettingsPopup : UIPanelController
{
    #region References

    [SerializeField] private Button closeButton;
    [Tooltip("Bấm nền tối cũng đóng; để trống thì bỏ qua.")]
    [SerializeField] private Button dimButton;
    [SerializeField] private PopupTweenBase popupTween;

    /// <inheritdoc/>
    public override bool IsOverlay => true;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (dimButton != null) dimButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (dimButton != null) dimButton.onClick.RemoveListener(Close);
    }

    #endregion

    #region Open And Close

    /// <summary>Xin UIManager mở popup (cần một màn đang hiện bên dưới).</summary>
    public void Open() => RequestVisibility(true);

    /// <summary>Thu nhỏ rồi xin UIManager đóng.</summary>
    public void Close()
    {
        if (!WantsVisible) return;
        if (popupTween != null && gameObject.activeInHierarchy) popupTween.PlayClose(() => RequestVisibility(false));
        else RequestVisibility(false);
    }

    /// <inheritdoc/>
    // UIManager gọi: object đã được bật trước khi hiện. Đóng thì tắt object để nền tối không chặn bấm màn dưới.
    public override void SetVisible(bool visible)
    {
        if (popupTween != null) popupTween.StopAndReset();
        if (!visible)
        {
            gameObject.SetActive(false);
            return;
        }
        if (popupTween != null) popupTween.PlayOpen();
    }

    #endregion
}
