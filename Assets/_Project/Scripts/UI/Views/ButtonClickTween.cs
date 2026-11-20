using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng nảy khi bấm nút, cùng cảm giác với nút Play: thu nhỏ → nảy vượt → về kích thước gốc.
/// Chỉ chạy khi nút thật sự được bấm (onClick), nên nút đang khóa không nảy.
/// </summary>
/// <remarks>
/// Hiệu ứng chạy song song, không trì hoãn hành động của nút (khác nút Play: Home chờ tween xong mới vào trận).
/// Gắn hàng loạt bằng menu Tools/Shooter Game/UI/Add Click Tween To All Buttons.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ButtonClickTween : MonoBehaviour
{
    #region Settings (mặc định giống SinglePanelPopupTween của nút Play)

    [SerializeField, Range(0.5f, 1f)] private float pressScale = 0.9f;
    [SerializeField, Min(1f)] private float peakScale = 1.08f;
    [SerializeField, Min(0.01f)] private float growDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float settleDuration = 0.12f;

    #endregion

    #region State

    private Button button;
    private Vector3 originalScale;
    private bool scaleCached;
    private Sequence sequence;

    #endregion

    #region Lifecycle

    private void Awake() => button = GetComponent<Button>();

    private void OnEnable() => button.onClick.AddListener(Play);

    // Tắt giữa lúc nảy (đổi màn ngay khi bấm) vẫn trả scale gốc, không mang trạng thái méo sang lần hiện sau.
    private void OnDisable()
    {
        button.onClick.RemoveListener(Play);
        sequence.Stop();
        if (scaleCached) transform.localScale = originalScale;
    }

    #endregion

    #region Playback

    private void Play()
    {
        // Listener khác của nút có thể đã tắt chính nút này (ví dụ tab thanh bar đổi sang bản nổi): không nảy nữa.
        if (!isActiveAndEnabled) return;
        // Chụp scale gốc ở lần bấm đầu, không ở Awake: nút nằm trong popup có thể đang bị popup thu về 0 lúc Awake.
        if (!scaleCached)
        {
            originalScale = transform.localScale;
            scaleCached = true;
        }
        sequence.Stop();
        Vector3 pressed = originalScale * pressScale;
        Vector3 peak = originalScale * peakScale;
        transform.localScale = pressed;
        sequence = Sequence.Create(useUnscaledTime: true) // Nút ở Home/Pause vẫn nảy khi game đang pause.
            .Chain(Tween.Scale(transform, pressed, peak, growDuration, Ease.OutCubic))
            .Chain(Tween.Scale(transform, peak, originalScale, settleDuration, Ease.OutQuad));
    }

    #endregion
}
