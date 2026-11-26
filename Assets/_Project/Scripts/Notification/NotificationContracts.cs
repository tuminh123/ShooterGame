using System;

// Ranh giới của miền Notification, do NotificationManager implement. Chỉ UI dùng (NotificationBadgeView và các màn).
// Notification là tầng trên cùng: chỉ ĐỌC interface của các Manager tầng dưới, không miền nào bên dưới biết nó.

#region Notifications (chấm báo trên UI)

/// <summary>Trạng thái một thông báo: số việc đang chờ.</summary>
public readonly struct NotificationState
{
    /// <summary>Số việc đang chờ; 0 = không có.</summary>
    public readonly int Count;

    public NotificationState(int count) => Count = count;

    /// <summary>Có việc đang chờ (hiện chấm).</summary>
    public bool Active => Count > 0;
}

/// <summary>Đọc thông báo theo khóa và nghe khi đổi. Mọi luật "khi nào báo" nằm trong miền Notification, UI chỉ hiển thị.</summary>
public interface INotifications
{
    /// <summary>
    /// Thông báo của <paramref name="key"/>; <paramref name="item"/> = null là tổng, có giá trị là riêng một mục
    /// (ID lịch, ID gói, ID nhóm mission, tên CharacterType, tên EquipmentSlot).
    /// </summary>
    NotificationState Get(NotificationKey key, string item = null);

    /// <summary>Thông báo của một khóa (hoặc mục bên trong nó) có thể đã đổi; phát tối đa một lần mỗi frame cho mỗi khóa.</summary>
    event Action<NotificationKey> Changed;
}

#endregion
