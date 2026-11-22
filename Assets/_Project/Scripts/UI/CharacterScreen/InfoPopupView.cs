using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup thông tin ở giữa màn Detail, dùng chung cho ô skill (tab Level) và dòng sao (tab Stars):
/// icon trong ô, tiêu đề, dòng trạng thái và mô tả đầy đủ. Chạm bất kỳ đâu thì đóng. Thiết kế cho cảm ứng, không dựa vào hover.
/// </summary>
/// <remarks>
/// Object đặt cuối cùng trong màn Detail để vẽ trên mọi thứ; nền tối phủ màn nên chặn mọi nút phía sau
/// khi popup đang mở. Chỉ hiển thị model; không đọc service.
/// </remarks>
public sealed class InfoPopupView : MonoBehaviour
{
    #region References

    [Tooltip("Nền tối phủ toàn màn; chạm vào thì đóng popup.")]
    [SerializeField] private Button dimmer;
    [Tooltip("Khung popup ở giữa; được phóng to nhẹ khi mở.")]
    [SerializeField] private RectTransform window;
    [SerializeField] private Image icon;
    [Tooltip("Tiêu đề: tên skill hoặc \"STAR n\".")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("Nút đóng trong khung; có thể để trống (chạm nền tối vẫn đóng được).")]
    [SerializeField] private Button closeButton;
    [Tooltip("Màu dòng trạng thái khi nội dung đã mở / chưa mở.")]
    [SerializeField] private Color unlockedStatusColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color lockedStatusColor = new Color(0.75f, 0.78f, 0.85f, 1f);
    [SerializeField, Min(0.01f)] private float openDuration = 0.18f;

    #endregion

    #region State

    private Sequence openSequence;

    #endregion

    #region Lifecycle

    private void Awake() => SetVisible(false);

    private void OnEnable()
    {
        dimmer.onClick.AddListener(Hide);
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        dimmer.onClick.RemoveListener(Hide);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
        openSequence.Stop();
        window.localScale = Vector3.one;
    }

    #endregion

    #region Presentation

    /// <summary>Mở popup với nội dung đã dựng sẵn (ô skill hoặc dòng sao).</summary>
    public void Show(InfoPopupModel model)
    {
        icon.sprite = model.Icon;
        icon.enabled = model.Icon != null;
        nameText.text = model.Title;
        statusText.text = model.Status;
        if (model.Unlocked)
            statusText.color = unlockedStatusColor;
        else
            statusText.color = lockedStatusColor;
        descriptionText.text = model.Description;
        SetVisible(true);
        // Ép layout tính lại ngay để khung khớp chữ mới trước khung hình đầu tiên.
        LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        PlayOpen();
    }

    /// <summary>Đóng popup (chạm nền tối, nút đóng, vẽ lại hoặc rời màn).</summary>
    public void Hide()
    {
        openSequence.Stop();
        window.localScale = Vector3.one;
        SetVisible(false);
    }

    // Phóng nhẹ từ 0.9 lên 1 cho cảm giác "bật lên"; dùng thời gian không scale vì màn Character chạy khi game pause.
    private void PlayOpen()
    {
        openSequence.Stop();
        window.localScale = Vector3.one * 0.9f;
        openSequence = Sequence.Create(useUnscaledTime: true)
            .Chain(Tween.Scale(window, 1f, openDuration, Ease.OutBack));
    }

    private void SetVisible(bool visible)
    {
        dimmer.gameObject.SetActive(visible);
        window.gameObject.SetActive(visible);
    }

    #endregion
}
