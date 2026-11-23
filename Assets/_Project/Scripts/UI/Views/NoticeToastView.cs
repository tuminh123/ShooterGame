using PrimeTween;
using TMPro;
using UnityEngine;

/// <summary>
/// Dòng thông báo ngắn nổi giữa màn (ví dụ "Not enough Gold"): hiện dần, giữ một lúc rồi mờ đi.
/// Dùng chung cho mọi màn; Controller gọi <see cref="Show"/>, View không gọi service.
/// </summary>
/// <remarks>
/// Object luôn bật và chỉ đổi alpha, để gọi Show lúc nào cũng chạy được. Không chặn click (blocksRaycasts = false).
/// Thời gian không scale vì các màn ở Home có thể đang pause.
/// </remarks>
[RequireComponent(typeof(CanvasGroup))]
public sealed class NoticeToastView : MonoBehaviour
{
    #region References And Settings

    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.12f;
    [SerializeField, Min(0f)] private float holdDuration = 1.1f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.3f;
    [Tooltip("Thông báo trồi lên một đoạn (px) khi hiện.")]
    [SerializeField] private float riseDistance = 24f;

    #endregion

    #region State

    private CanvasGroup group;
    private RectTransform rect;
    private Vector2 restPosition;
    private Sequence sequence;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
    }

    // Tắt giữa lúc hiện thì ẩn hẳn và trả về chỗ cũ.
    private void OnDisable()
    {
        sequence.Stop();
        if (group == null) return;
        group.alpha = 0f;
        rect.anchoredPosition = restPosition;
    }

    #endregion

    #region Presentation

    /// <summary>Hiện <paramref name="message"/>; đang hiện thì chạy lại từ đầu với nội dung mới (bấm liên tục không chồng nhau).</summary>
    public void Show(string message)
    {
        if (group == null) Awake(); // Object có thể chưa từng bật trước lần gọi đầu.
        sequence.Stop();
        messageText.text = message;
        rect.anchoredPosition = restPosition - new Vector2(0f, riseDistance);
        group.alpha = 0f;
        sequence = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(group, 1f, fadeInDuration))
            .Group(Tween.UIAnchoredPosition(rect, restPosition, fadeInDuration, Ease.OutCubic))
            .ChainDelay(holdDuration)
            .Chain(Tween.Alpha(group, 0f, fadeOutDuration));
    }

    #endregion
}
