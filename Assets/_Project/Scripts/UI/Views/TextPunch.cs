using PrimeTween;
using TMPro;
using UnityEngine;

/// <summary>
/// Hiệu ứng "số nhảy lên" cho một text: phóng to rồi nảy về, kèm nháy màu, khi giá trị tăng.
/// Lớp thuần do View tạo và giữ; không phải MonoBehaviour nên không cần gắn thêm gì trong scene.
/// </summary>
public sealed class TextPunch
{
    #region Settings

    private const float PeakScale = 1.3f;
    private const float GrowDuration = 0.1f;
    private const float SettleDuration = 0.2f;
    private const float FlashDuration = 0.35f;
    private static readonly Color FlashColor = new Color(0.45f, 1f, 0.45f, 1f); // Xanh lá: tín hiệu "tăng".

    #endregion

    #region State

    private readonly TMP_Text text;
    private readonly Vector3 originalScale;
    private readonly Color originalColor;
    private Sequence sequence;
    private float lastValue;
    private bool hasValue;

    #endregion

    /// <summary>Chụp scale và màu gốc của text lúc tạo.</summary>
    public TextPunch(TMP_Text text)
    {
        this.text = text;
        originalScale = text.transform.localScale;
        originalColor = text.color;
    }

    #region Public API

    /// <summary>
    /// Ghi chữ mới; nhảy khi <paramref name="animate"/> và <paramref name="value"/> lớn hơn lần trước.
    /// Truyền animate = false khi đổi sang nhân vật khác để số khác nhau giữa hai nhân vật không bị coi là "tăng".
    /// </summary>
    public void Set(string display, float value, bool animate)
    {
        text.text = display;
        bool increased = hasValue && value > lastValue + 0.0001f;
        lastValue = value;
        hasValue = true;
        if (animate && increased && text.isActiveAndEnabled) Play();
    }

    /// <summary>Dừng hiệu ứng và trả về dáng gốc (View gọi khi bị tắt).</summary>
    public void Reset()
    {
        sequence.Stop();
        text.transform.localScale = originalScale;
        text.color = originalColor;
    }

    #endregion

    #region Playback

    private void Play()
    {
        Reset();
        Vector3 peak = originalScale * PeakScale;
        // Đặt theo mốc thời gian: nháy màu chạy suốt từ đầu, song song với phóng to rồi nảy về.
        sequence = Sequence.Create(useUnscaledTime: true) // Màn Character chạy khi game đang pause.
            .Insert(0f, Tween.Color(text, FlashColor, originalColor, FlashDuration, Ease.InQuad))
            .Insert(0f, Tween.Scale(text.transform, originalScale, peak, GrowDuration, Ease.OutCubic))
            .Insert(GrowDuration, Tween.Scale(text.transform, peak, originalScale, SettleDuration, Ease.OutBack));
    }

    #endregion
}
