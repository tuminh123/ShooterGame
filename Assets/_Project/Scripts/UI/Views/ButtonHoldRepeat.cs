using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Giữ nút để bấm lặp: sau một lúc giữ, nút tự gọi onClick liên tục và nhanh dần (ví dụ nút UPGRADE nạp EXP).
/// Thả tay sau khi đã lặp thì không tính thêm một lần bấm nữa.
/// </summary>
/// <remarks>Dùng unscaled time vì màn Character chạy khi game đang pause. Nút bị khóa (MAX) thì tự dừng lặp.</remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ButtonHoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    #region Settings

    [Tooltip("Giữ bao lâu (giây) thì bắt đầu lặp.")]
    [SerializeField, Min(0.05f)] private float holdDelay = 0.4f;
    [Tooltip("Khoảng cách giữa hai lần lặp đầu tiên (giây).")]
    [SerializeField, Min(0.01f)] private float startInterval = 0.15f;
    [Tooltip("Khoảng cách nhỏ nhất khi đã nhanh dần (giây).")]
    [SerializeField, Min(0.01f)] private float minInterval = 0.04f;
    [Tooltip("Mỗi lần lặp nhân khoảng cách với hệ số này (< 1 là nhanh dần).")]
    [SerializeField, Range(0.5f, 1f)] private float acceleration = 0.85f;

    #endregion

    #region State

    private Button button;
    private bool holding;
    private bool repeated;
    private float nextTime;
    private float interval;

    #endregion

    #region Unity Events

    private void Awake() => button = GetComponent<Button>();

    private void OnDisable() => holding = false;

    /// <summary>Bắt đầu đếm giờ giữ nút.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.IsInteractable()) return;
        holding = true;
        repeated = false;
        interval = startInterval;
        nextTime = Time.unscaledTime + holdDelay;
    }

    /// <summary>Thả tay: nếu đã lặp thì bỏ lần bấm khi thả, để không bị tính dư một lần.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (repeated) eventData.eligibleForClick = false;
        holding = false;
    }

    /// <summary>Kéo tay ra khỏi nút thì dừng lặp.</summary>
    public void OnPointerExit(PointerEventData eventData) => holding = false;

    private void Update()
    {
        if (!holding) return;
        if (!button.IsInteractable()) { holding = false; return; } // Vừa chạm MAX hoặc bị khóa.
        if (Time.unscaledTime < nextTime) return;
        repeated = true;
        button.onClick.Invoke(); // Cùng đường với bấm tay: tween nút, nạp điểm, vẽ lại.
        interval = Mathf.Max(minInterval, interval * acceleration);
        nextTime = Time.unscaledTime + interval;
    }

    #endregion
}
