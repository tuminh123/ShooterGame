using System;
using PrimeTween;
using UnityEngine;

/// <summary>Hướng trượt của <see cref="SlideTransition"/>: phía màn hình mà panel trượt vào từ đó hoặc trượt ra về đó.</summary>
public enum SlideDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Trượt một panel vào/ra khỏi khung cha bằng anchoredPosition (cùng cách với banner cảnh báo enemy).
/// Dùng chung cho chuyển màn (Collection/Detail) và chuyển tab (Level/Stars).
/// </summary>
/// <remarks>
/// Chạy bằng unscaled time vì Home và màn Character hoạt động khi game đang pause.
/// Trong lúc trượt, panel không nhận click (CanvasGroup.blocksRaycasts) để tránh bấm Back/đổi tab giữa chừng.
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

    /// <summary>Đang trượt vào hoặc ra.</summary>
    public bool IsPlaying => tween.isAlive;

    #endregion

    #region Public API

    /// <summary>Bật panel và trượt vào theo hướng mặc định.</summary>
    public void PlayIn(Action onComplete = null) => PlayIn(enterFrom, onComplete);

    /// <summary>Bật panel và trượt từ phía <paramref name="from"/> vào vị trí nghỉ.</summary>
    public void PlayIn(SlideDirection from, Action onComplete = null)
    {
        CacheRest();
        Stop();
        gameObject.SetActive(true);
        Vector2 start = restPosition + Offset(from);
        rect.anchoredPosition = start;
        Begin(onComplete);
        tween = Tween.UIAnchoredPosition(rect, start, restPosition, enterDuration, Ease.OutCubic, useUnscaledTime: true)
            .OnComplete(this, slide => slide.Finish(deactivate: false));
    }

    /// <summary>Trượt ra theo hướng mặc định rồi tắt panel.</summary>
    public void PlayOut(Action onComplete = null) => PlayOut(exitTo, onComplete);

    /// <summary>Trượt về phía <paramref name="to"/>, tắt panel, rồi gọi <paramref name="onComplete"/>.</summary>
    public void PlayOut(SlideDirection to, Action onComplete = null)
    {
        CacheRest();
        Stop();
        if (!isActiveAndEnabled)
        {
            onComplete?.Invoke();
            return;
        }
        Begin(onComplete);
        tween = Tween.UIAnchoredPosition(rect, rect.anchoredPosition, restPosition + Offset(to), exitDuration, Ease.InCubic, useUnscaledTime: true)
            .OnComplete(this, slide => slide.Finish(deactivate: true));
    }

    /// <summary>Bật/tắt ngay, không trượt (lần hiện đầu tiên, vẽ lại cùng tab).</summary>
    public void SetVisibleInstant(bool visible)
    {
        CacheRest();
        Stop();
        ResetPose();
        gameObject.SetActive(visible);
    }

    #endregion

    #region Playback Helpers

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
    }

    #endregion
}
