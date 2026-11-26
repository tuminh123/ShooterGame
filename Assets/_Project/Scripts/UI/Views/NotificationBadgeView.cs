using System;
using TMPro;
using UnityEngine;
using Zenject;

/// <summary>
/// Một chấm báo trên UI: đọc <see cref="INotifications"/> theo khóa (và mục), tự bật/tắt và hiện số. Không chứa luật
/// "khi nào báo" — luật nằm ở các nguồn của miền Notification, nên mọi chấm cùng khóa luôn khớp nhau.
/// </summary>
/// <remarks>
/// Gắn lên object luôn bật (thường là nút), trỏ <see cref="badge"/> tới chấm cần bật/tắt: nếu gắn lên chính chấm thì chấm tắt
/// sẽ làm view ngừng nghe. View bị tắt (ví dụ nút bị ẩn) thì chấm cũng tắt theo.
/// </remarks>
public sealed class NotificationBadgeView : MonoBehaviour
{
    #region Settings

    [SerializeField] private NotificationKey key;
    [Tooltip("Các mục cộng lại (ID lịch, ID gói, ID nhóm mission, CharacterType, EquipmentSlot); để trống = tổng của khóa.")]
    [SerializeField] private string[] items = Array.Empty<string>();
    [SerializeField] private NotificationBadgeMode mode = NotificationBadgeMode.Dot;
    [Tooltip("Chấm cần bật/tắt (Alert_Circle…).")]
    [SerializeField] private GameObject badge;
    [Tooltip("Chữ số trong chấm; chỉ dùng khi Mode = Count.")]
    [SerializeField] private TMP_Text countText;
    [SerializeField, Min(1)] private int maxCount = 9;

    private INotifications notifications;
    private bool subscribed;

    #endregion

    #region Lifecycle

    /// <summary>SceneContext inject cả object đang tắt; đăng ký khi object được bật.</summary>
    [Inject]
    public void Construct(INotifications notificationService)
    {
        Unsubscribe();
        notifications = notificationService;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        Unsubscribe();
        if (badge != null) badge.SetActive(false);
    }

    private void Subscribe()
    {
        if (subscribed || notifications == null) return;
        subscribed = true;
        notifications.Changed += OnChanged;
        Draw();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        notifications.Changed -= OnChanged;
    }

    #endregion

    #region Draw

    private void OnChanged(NotificationKey changed)
    {
        if (changed == key) Draw();
    }

    private void Draw()
    {
        if (badge == null) return;
        int count = Count();
        badge.SetActive(count > 0);
        if (mode != NotificationBadgeMode.Count || countText == null || count <= 0) return;
        countText.text = count > maxCount ? maxCount + "+" : count.ToString();
    }

    private int Count()
    {
        if (items.Length == 0) return notifications.Get(key).Count;
        int total = 0;
        foreach (string item in items) total += notifications.Get(key, item).Count;
        return total;
    }

    #endregion
}
