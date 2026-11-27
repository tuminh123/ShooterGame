using System;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Trượt một panel vào/ra khỏi khung cha bằng anchoredPosition (cùng cách với banner cảnh báo enemy).
/// Dùng chung cho chuyển màn (Collection/Detail) và chuyển tab (Level/Stars).
/// </summary>
/// <remarks>
/// Chạy bằng unscaled time vì Home và màn Character hoạt động khi game đang pause.
/// Trong lúc trượt, panel không nhận click (CanvasGroup.blocksRaycasts) để tránh bấm Back/đổi tab giữa chừng.
/// Bị gọi lại giữa chừng (bấm đổi tab nhanh) thì trượt tiếp từ vị trí hiện tại, không đặt lại ra ngoài màn,
/// và thời gian tỉ lệ với quãng còn lại để tốc độ trượt không đổi.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class SlideTransition : MonoBehaviour
{
    #region Settings

    [Tooltip("Hướng mặc định khi trượt vào: panel xuất phát từ phía này của màn hình.")]
    [SerializeField] private SlideDirection enterFrom = SlideDirection.Right;
    [Tooltip("Hướng mặc định khi trượt ra: panel đi về phía này của màn hình.")]
    [SerializeField] private SlideDirection exitTo = SlideDirection.Right;
    [SerializeField, Min(0.01f)] private float enterDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float exitDuration = 0.25f;

    #endregion

    #region State

    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 restPosition; // Vị trí nghỉ chụp một lần trước tween đầu tiên, không chụp lại giữa lúc trượt.
    private bool restCached;
    private Tween tween;
    private Action completion; // Xóa khi bị ngắt để callback cũ không chạy sau một lần trượt mới.
    // Panel đã được hiện (PlayIn/SetVisibleInstant(true)) và chưa bị ẩn. Không dùng activeSelf: UIPanelController.Present
    // bật object trước khi gọi PlayIn, nên activeSelf không phân biệt được "vừa bật để trượt vào" với "đang hiện sẵn".
    private bool shown;

    /// <summary>Đang trượt vào hoặc ra.</summary>
    public bool IsPlaying => tween.isAlive;

    #endregion

    #region Public API

    /// <summary>Bật panel và trượt vào theo hướng mặc định.</summary>
    public void PlayIn(Action onComplete = null) => PlayIn(enterFrom, onComplete);

    /// <summary>
    /// Bật panel và trượt từ phía <paramref name="from"/> vào vị trí nghỉ.
    /// Panel đang hiện (kể cả đang trượt ra dở) thì trượt tiếp từ chỗ hiện tại; đã đứng ở vị trí nghỉ thì xong ngay.
    /// </summary>
    public void PlayIn(SlideDirection from, Action onComplete = null)
    {
        CacheRest();
        // Đọc trước Stop: đang trượt dở (vào hoặc ra) hoặc đang hiện sẵn thì giữ vị trí hiện tại.
        bool keepPosition = tween.isAlive || (shown && gameObject.activeSelf);
        Stop();
        gameObject.SetActive(true);
        shown = true;
        Vector2 offset = Offset(from);
        // Không đặt lại ra ngoài màn khi panel đang trên màn hình: đó là cú nhảy khi bấm đổi tab nhanh.
        Vector2 start = keepPosition ? rect.anchoredPosition : restPosition + offset;
        rect.anchoredPosition = start;
        Begin(onComplete);
        float duration = enterDuration * RemainingFraction(start, restPosition, offset);
        if (duration < MinDuration)
        {
            rect.anchoredPosition = restPosition;
            Finish(deactivate: false);
            return;
        }
        tween = Tween.UIAnchoredPosition(rect, start, restPosition, duration, Ease.OutCubic, useUnscaledTime: true)
            .OnComplete(this, slide => slide.Finish(deactivate: false));
    }

    /// <summary>Trượt ra theo hướng mặc định rồi tắt panel.</summary>
    public void PlayOut(Action onComplete = null) => PlayOut(exitTo, onComplete);

    /// <summary>Trượt về phía <paramref name="to"/>, tắt panel, rồi gọi <paramref name="onComplete"/>.</summary>
    public void PlayOut(SlideDirection to, Action onComplete = null)
    {
        CacheRest();
        Stop();
        shown = false;
        if (!isActiveAndEnabled)
        {
            onComplete?.Invoke();
            return;
        }
        Begin(onComplete);
        Vector2 offset = Offset(to);
        Vector2 start = rect.anchoredPosition;
        Vector2 end = restPosition + offset;
        // Đang trượt vào dở mà bị gọi ra: chỉ đi nốt quãng còn lại, cùng tốc độ với một lần trượt ra đủ.
        float duration = exitDuration * RemainingFraction(start, end, offset);
        if (duration < MinDuration)
        {
            Finish(deactivate: true);
            return;
        }
        tween = Tween.UIAnchoredPosition(rect, start, end, duration, Ease.InCubic, useUnscaledTime: true)
            .OnComplete(this, slide => slide.Finish(deactivate: true));
    }

    /// <summary>Bật/tắt ngay, không trượt (lần hiện đầu tiên, vẽ lại cùng tab).</summary>
    public void SetVisibleInstant(bool visible)
    {
        CacheRest();
        Stop();
        ResetPose();
        shown = visible;
        gameObject.SetActive(visible);
    }

    #endregion

    #region Playback Helpers

    // Dưới ngưỡng này coi như đã tới nơi: không tạo tween chỉ để chạy một frame.
    private const float MinDuration = 0.01f;

    // Tỉ lệ quãng còn lại so với một lần trượt đủ (0..1). Dùng chung cho vào/ra để mọi lần trượt cùng một tốc độ.
    private static float RemainingFraction(Vector2 from, Vector2 to, Vector2 fullOffset)
    {
        float full = fullOffset.magnitude;
        return full > 0f ? Mathf.Clamp01((to - from).magnitude / full) : 0f;
    }

    private void Begin(Action onComplete)
    {
        completion = onComplete;
        group.blocksRaycasts = false;
    }

    private void Finish(bool deactivate)
    {
        tween = default;
        group.blocksRaycasts = true;
        // Lấy callback TRƯỚC khi tắt panel: SetActive(false) gọi OnDisable → Stop() sẽ xóa completion,
        // làm controller không bao giờ biết đã trượt xong (kẹt Busy, bấm thẻ không được).
        Action callback = completion;
        completion = null;
        if (deactivate) gameObject.SetActive(false); // OnDisable trả panel về vị trí nghỉ cho lần mở sau.
        callback?.Invoke(); // Callback được phép đổi trang hoặc bắt đầu lần trượt mới.
    }

    // PrimeTween.Stop không gọi OnComplete; bỏ luôn callback đang chờ của lần trước.
    private void Stop()
    {
        completion = null;
        tween.Stop();
        tween = default;
    }

    private void ResetPose()
    {
        if (!restCached) return;
        rect.anchoredPosition = restPosition;
        group.blocksRaycasts = true;
    }

    private void CacheRest()
    {
        if (restCached) return;
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        restPosition = rect.anchoredPosition;
        restCached = true;
    }

    // Đẩy hẳn ra ngoài khung cha: nửa khung cha + nửa panel tính từ tâm, cộng dư bằng kích thước panel cho chắc chắn.
    private Vector2 Offset(SlideDirection direction)
    {
        Rect parent = rect.parent is RectTransform parentRect ? parentRect.rect : new Rect(0f, 0f, Screen.width, Screen.height);
        Rect own = rect.rect;
        float horizontal = parent.width + own.width;
        float vertical = parent.height + own.height;
        switch (direction)
        {
            case SlideDirection.Left: return new Vector2(-horizontal, 0f);
            case SlideDirection.Right: return new Vector2(horizontal, 0f);
            case SlideDirection.Up: return new Vector2(0f, vertical);
            case SlideDirection.Down: return new Vector2(0f, -vertical);
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    /// <summary>Hướng ngược lại, dùng khi chuyển tab: tab cũ ra một phía thì tab mới vào từ phía đối diện.</summary>
    public static SlideDirection Opposite(SlideDirection direction)
    {
        switch (direction)
        {
            case SlideDirection.Left: return SlideDirection.Right;
            case SlideDirection.Right: return SlideDirection.Left;
            case SlideDirection.Up: return SlideDirection.Down;
            case SlideDirection.Down: return SlideDirection.Up;
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    #endregion

    #region Unity Cleanup

    // Bị tắt từ ngoài (UIManager ẩn màn) giữa lúc trượt: dừng tween, trả vị trí nghỉ, không chạy callback cũ.
    private void OnDisable()
    {
        Stop();
        ResetPose();
        shown = false; // Lần bật sau là một lần mở mới: trượt từ ngoài màn vào.
    }

    #endregion
}
