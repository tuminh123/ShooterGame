using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup xác nhận mua: tên gói, phần thưởng, câu hỏi kèm giá, nút Mua và nút đóng. Màn Shop gọi <see cref="Show"/> và truyền
/// việc cần làm khi xác nhận.
/// </summary>
/// <remarks>
/// Là overlay do UIManager quản lý (<see cref="IsOverlay"/>): đè lên màn Shop mà không ẩn nó, UIManager tắt lúc khởi động và tự bật
/// object khi mở (scene lưu bật hay tắt đều như nhau), Back đóng popup trước, rời màn Shop thì popup đóng theo.
/// </remarks>
public sealed class ShopConfirmPopup : UIPanelController
{
    #region References

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button closeButton;
    [Tooltip("Bấm nền tối cũng đóng; để trống thì bỏ qua.")]
    [SerializeField] private Button dimButton;
    [SerializeField] private SinglePanelPopupTween popupTween;
    [SerializeField] private string confirmLabel = "Buy";

    #endregion

    #region State

    // Nội dung chờ hiện: Show ghi lại rồi xin UIManager mở; SetVisible(true) mới vẽ.
    private string title;
    private Sprite rewardIcon;
    private string amount;
    private string message;
    private Action onConfirm;

    /// <inheritdoc/>
    public override bool IsOverlay => true;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        confirmButton.onClick.AddListener(Confirm);
        closeButton.onClick.AddListener(Hide);
        if (dimButton != null) dimButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        confirmButton.onClick.RemoveListener(Confirm);
        closeButton.onClick.RemoveListener(Hide);
        if (dimButton != null) dimButton.onClick.RemoveListener(Hide);
    }

    #endregion

    #region Show And Hide

    /// <summary>Xin UIManager mở popup; <paramref name="confirm"/> chạy khi bấm Mua (popup đóng trước).</summary>
    public void Show(string packTitle, Sprite packIcon, string packAmount, string question, Action confirm)
    {
        title = packTitle;
        rewardIcon = packIcon;
        amount = packAmount;
        message = question;
        onConfirm = confirm;
        if (WantsVisible) Draw(); // Đang mở (bấm thẻ khác ngay sau): chỉ vẽ lại.
        else RequestVisibility(true);
    }

    /// <summary>Xin UIManager đóng popup, không làm gì thêm.</summary>
    public void Hide()
    {
        onConfirm = null;
        RequestVisibility(false);
    }

    /// <inheritdoc/>
    // UIManager gọi: object đã được bật trước khi hiện. Đóng thì tắt object để nền tối không chặn bấm màn dưới.
    public override void SetVisible(bool visible)
    {
        if (popupTween != null) popupTween.StopAndReset();
        if (!visible)
        {
            onConfirm = null;
            gameObject.SetActive(false);
            return;
        }
        Draw();
        if (popupTween != null) popupTween.PlayOpen();
    }

    private void Draw()
    {
        titleText.text = title;
        if (icon != null)
        {
            icon.sprite = rewardIcon;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(rewardIcon != null);
        }
        if (amountText != null) amountText.text = amount;
        messageText.text = message;
        if (confirmText != null) confirmText.text = confirmLabel;
    }

    // Đóng trước rồi mới chạy: việc mua có thể mở màn nhận quà ngay sau đó.
    private void Confirm()
    {
        Action confirm = onConfirm;
        Hide();
        confirm?.Invoke();
    }

    #endregion
}
