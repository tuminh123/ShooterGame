using PrimeTween;
using UnityEngine;

/// <summary>
/// Hiệu ứng thu hút: phóng to rồi thu nhỏ lặp lại cho tới khi <see cref="Stop"/>. Dùng chung cho ô "nhận hôm nay" của popup
/// đăng nhập và các panel nhận quà sau này. Dùng unscaled time vì Home đang pause (timeScale = 0).
/// </summary>
/// <remarks>
/// Không gắn cùng object với <c>ButtonClickTween</c>: cả hai cùng đổi scale của một Transform sẽ giành nhau.
/// Stop và OnDisable luôn trả scale gốc, nên ẩn panel giữa chừng không để lại ô bị phóng to.
/// </remarks>
public sealed class AttentionPulse : MonoBehaviour
{
    #region Settings

    [Tooltip("Transform được phóng to/thu nhỏ; để trống thì dùng chính object này.")]
    [SerializeField] private Transform target;
    [Tooltip("Tỷ lệ lớn nhất so với scale gốc.")]
    [SerializeField, Min(1f)] private float peakScale = 1.08f;
    [Tooltip("Giây thực cho một lượt phóng to (lượt thu nhỏ bằng thời gian này).")]
    [SerializeField, Min(0.05f)] private float halfCycle = 0.55f;

    #endregion

    #region State

    private Tween tween;
    private Vector3 baseScale; // Scale gốc chụp ở lần chạy đầu, để Stop trả về đúng kích thước đã chỉnh trong Inspector.
    private bool hasBaseScale;

    /// <summary>Đang nhấp nháy.</summary>
    public bool IsPlaying => tween.isAlive;

    private Transform Target => target != null ? target : transform;

    #endregion

    #region Control

    /// <summary>Bắt đầu nhấp nháy; đang chạy thì giữ nguyên, không khởi động lại.</summary>
    public void Play()
    {
        if (tween.isAlive) return;
        Transform pulsing = Target;
        if (!hasBaseScale)
        {
            baseScale = pulsing.localScale;
            hasBaseScale = true;
        }
        pulsing.localScale = baseScale;
        tween = Tween.Scale(pulsing, baseScale, baseScale * peakScale, halfCycle, Ease.InOutSine,
            cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
    }

    /// <summary>Dừng và trả scale gốc.</summary>
    public void Stop()
    {
        if (tween.isAlive) tween.Stop();
        if (hasBaseScale) Target.localScale = baseScale;
    }

    private void OnDisable() => Stop();

    #endregion
}
