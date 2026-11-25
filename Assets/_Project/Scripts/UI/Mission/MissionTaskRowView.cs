using System.Globalization;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Một dòng mission con: icon, mô tả, thanh tiến độ và "x/y". Thanh chạy từ mức đã xem tới mức hiện tại.</summary>
public sealed class MissionTaskRowView : MonoBehaviour
{
    #region References

    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text progressText;
    [Tooltip("Icon đầu thanh, nhún khi thanh chạy.")]
    [SerializeField] private RectTransform icon;
    [Tooltip("Thanh đầy theo chiều rộng bằng anchor, không cần sprite Filled.")]
    [SerializeField] private Image fill;
    [SerializeField] private Sprite runningFill;
    [SerializeField] private Sprite doneFill;
    [SerializeField, Min(0.01f)] private float fillDuration = 0.6f;

    #endregion

    #region State

    private MissionTaskModel model;
    private TextPunch punch;
    private Sequence sequence;
    private int shown = -1; // Số đang hiện; chỉ gán chữ khi số đổi vì mỗi lần gán là một lần dựng lại lưới chữ.

    #endregion

    #region Presentation

    /// <summary>Hiện một mission con; vẽ lại cùng dữ liệu không ngắt thanh đang chạy.</summary>
    public void Bind(MissionTaskModel next)
    {
        if (sequence.isAlive && next.Key == model.Key && next.Progress == model.Progress) return;
        StopMotion();
        model = next;
        shown = -1;
        descriptionText.text = model.Description;
        Show(model.From);
        if (model.From == model.Progress) return;
        // Home pause (timeScale = 0) nên dùng unscaled time.
        sequence = Sequence.Create(useUnscaledTime: true)
            .ChainDelay(model.Delay)
            .Chain(Tween.Custom((float)model.From, (float)model.Progress, fillDuration, Show, Ease.OutCubic))
            .Group(Tween.PunchScale(icon, Vector3.one * 0.35f, fillDuration, 6f))
            .ChainCallback(Finish);
    }

    private void Finish()
    {
        Show(model.Progress);
        // Đặt mốc cũ rồi ghi số mới để TextPunch nhận ra là "tăng".
        punch = punch ?? new TextPunch(progressText);
        punch.Set(progressText.text, model.From, false);
        punch.Set(progressText.text, model.Progress, true);
    }

    private void Show(float value)
    {
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value / model.Target), 1f);
        fill.sprite = value >= model.Target ? doneFill : runningFill;
        int number = Mathf.FloorToInt(value);
        if (number == shown) return;
        shown = number;
        progressText.text = number.ToString("N0", CultureInfo.InvariantCulture) + "/" +
                            model.Target.ToString("N0", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Lifecycle

    // Tắt giữa lúc chạy: dừng và hiện đúng mức cuối.
    private void OnDisable()
    {
        bool running = sequence.isAlive;
        StopMotion();
        if (running) Show(model.Progress);
    }

    private void StopMotion()
    {
        sequence.Stop();
        icon.localScale = Vector3.one;
        punch?.Reset();
    }

    #endregion
}
