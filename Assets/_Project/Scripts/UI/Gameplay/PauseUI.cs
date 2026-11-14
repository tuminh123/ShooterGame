using UnityEngine;
using UnityEngine.UI;
using Zenject;

// Nút pause tự quản lý click; không để UIManager xử lý hành vi của từng nút.
public sealed class PauseUI : MonoBehaviour
{
    #region References And Pause State

    [SerializeField] private Button button;
    [Tooltip("Popup Pause trong scene; mở bằng nút Pause và đóng bằng nút X.")]
    [SerializeField] private GameObject popup;
    [SerializeField] private Button closeButton;
    [Tooltip("Chơi lại màn hiện tại; xóa trạng thái lượt cũ và dùng lại pool đã chuẩn bị.")]
    [SerializeField] private Button restartButton;
    [Tooltip("Dọn lượt chơi và trở về Home trong cùng scene.")]
    [SerializeField] private Button homeButton;
    [Tooltip("Hiệu ứng popup dùng unscaled time, vẫn chạy khi gameplay đang pause.")]
    [SerializeField] private PopupTweenBase popupTween;
    private GameplayPauseService pauses;
    private GameManager game;
    private ScreenTransitionController transition;
    private System.IDisposable pauseLease;
    private bool closing;

    #endregion

    #region Injection And Lifecycle

    [Inject]
    public void Construct(GameplayPauseService pauses, GameManager game, ScreenTransitionController transition)
    {
        this.pauses = pauses;
        this.game = game;
        this.transition = transition;
        Resume();
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePopup);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (homeButton != null) homeButton.onClick.AddListener(ReturnHome);
        Resume();
    }
    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePopup);
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        if (homeButton != null) homeButton.onClick.RemoveListener(ReturnHome);
        Resume();
    }

    #endregion

    #region Pause Actions

    private void Toggle()
    {
        if (transition != null && transition.IsTransitioning) return;
        if (pauseLease != null) { ClosePopup(); return; }
        if (pauses == null || popup == null || game == null || game.State != GameState.Playing) return;
        pauseLease = pauses.Acquire();
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
        if (popupTween != null) popupTween.PlayOpen();
    }

    // Giữ gameplay dừng cho đến khi hiệu ứng đóng xong; click liên tiếp không chạy lại callback.
    private void ClosePopup()
    {
        if (closing || pauseLease == null || (transition != null && transition.IsTransitioning)) return;
        closing = true;
        if (popupTween != null && popup != null && popup.activeInHierarchy)
            popupTween.PlayClose(Resume);
        else Resume();
    }

    // Giữ popup tới khi lớp phủ kín; HUD đóng khi đổi state sẽ tự trả token qua Resume.
    private void Restart()
    {
        if (closing || pauseLease == null || transition == null) return;
        transition.RestartRun();
    }

    private void ReturnHome()
    {
        if (closing || pauseLease == null || transition == null) return;
        transition.ReturnHome();
    }

    // Cleanup tức thời cho đổi state/tắt HUD; hủy tween cũ và trả scale gốc trước khi ẩn popup.
    public void Resume()
    {
        closing = false;
        if (popupTween != null) popupTween.StopAndReset();
        System.IDisposable previous = pauseLease;
        pauseLease = null;
        previous?.Dispose();
        if (popup != null) popup.SetActive(false);
    }

    // GameplayController gọi trong LateTick: đóng popup từ ngoài cũng phải trả đúng token pause.
    internal void CheckPopupClosed()
    {
        if (pauseLease != null && (popup == null || !popup.activeInHierarchy)) Resume();
    }
    #endregion
}
