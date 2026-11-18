using UnityEngine;

/// <summary>
/// Strategy của một <b>kiểu bắn</b>: chia hướng bay cho từng viên đạn trong một lượt bắn.
/// </summary>
/// <remarks>
/// Kiểu bắn được chọn trực tiếp trong <see cref="ShootingData"/> bằng <c>[SerializeReference]</c>.
/// Instance trong asset là <b>template chỉ đọc</b>; mỗi skill đang chạy dùng bản sao riêng từ
/// <see cref="CreateRuntime"/>, để trạng thái như góc xoay của Spiral không dùng chung giữa các nhân vật.
/// </remarks>
public interface IShooter
{
    /// <summary>Kiểm tra thông số (góc phải hữu hạn…). Ném exception nếu cấu hình sai.</summary>
    void Validate();

    /// <summary>Tạo bản sao riêng cho một skill đang chạy, với trạng thái runtime đã được reset.</summary>
    IShooter CreateRuntime();

    /// <summary>Hướng bay của viên thứ <paramref name="projectileIndex"/> trong tổng số <paramref name="projectileCount"/> viên.</summary>
    Vector2 GetDirection(Vector2 aimDirection, int projectileIndex, int projectileCount);

    /// <summary>Gọi sau một lượt bắn có ít nhất một viên được tạo, để kiểu bắn cập nhật trạng thái của nó.</summary>
    void OnShotCompleted();
}
