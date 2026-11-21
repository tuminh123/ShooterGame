using UnityEngine;

/// <summary>
/// Camera bám Player với độ trễ mượt. System thuần của miền World, do <see cref="WorldManager"/> tạo và gọi ở LateTick.
/// </summary>
/// <remarks>
/// Trước đây là MonoBehaviour <c>CameraFolow</c> tự đăng ký LateTick với số thứ tự riêng. Chuyển vào World để
/// thứ tự frame chỉ nằm ở installer: camera chạy sau Simulation (đã áp di chuyển) nên đọc đúng vị trí cuối của Player.
/// </remarks>
internal sealed class CameraFollow
{
    #region State

    private readonly Transform camera;
    private readonly Vector3 offset;
    private readonly float smoothTime;
    private Transform target;
    private Vector3 velocity; // Vận tốc nội bộ của SmoothDamp, giữ giữa các frame để chuyển động liên tục.

    #endregion

    #region Lifecycle

    /// <summary>Nhận Transform camera trong scene và cấu hình bám từ <see cref="WorldManager.Settings"/>.</summary>
    internal CameraFollow(Transform camera, Vector3 offset, float smoothTime)
    {
        this.camera = camera;
        this.offset = offset;
        this.smoothTime = smoothTime;
    }

    /// <summary>Đổi mục tiêu và reset quán tính, để camera không trượt theo đà của mục tiêu cũ.</summary>
    internal void Follow(Transform newTarget)
    {
        target = newTarget;
        velocity = Vector3.zero;
    }

    /// <summary>Làm mượt camera về vị trí mục tiêu + offset. Bỏ qua khi pause (deltaTime = 0) hoặc chưa có mục tiêu.</summary>
    internal void LateTick()
    {
        // So sánh kiểu Unity: camera/target đã bị Destroy (ví dụ lúc đóng scene) được xem là null.
        if (camera == null || target == null || Time.deltaTime <= 0f)
            return;
        Vector3 desired = target.position + offset;
        camera.position = Vector3.SmoothDamp(camera.position, desired, ref velocity, smoothTime, Mathf.Infinity, Time.deltaTime);
    }

    #endregion
}
