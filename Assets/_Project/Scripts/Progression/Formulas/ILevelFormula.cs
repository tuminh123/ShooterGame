/// <summary>
/// Công thức "lượng cần cho một mốc cấp": nhận level hiện tại, trả lượng cần để lên level kế tiếp.
/// Dùng chung cho giá Gold nâng cấp trang bị, giá nâng level nhân vật và EXP lên cấp trong trận.
/// </summary>
/// <remarks>
/// Đây là họ strategy (nhiều cách tính thay được cho nhau). Asset chọn công thức bằng <c>[SerializeReference]</c>,
/// giống <see cref="IShooter"/>, vì mỗi công thức có bộ tham số riêng: Inspector chỉ hiện đúng tham số của công thức đang chọn.
/// Muốn đổi cách cân bằng thì viết thêm một class implement interface này; code ở nơi dùng không phải sửa.
/// Instance trong asset chỉ đọc (không giữ trạng thái), nên dùng chung được giữa mọi nhân vật và món đồ.
/// </remarks>
public interface ILevelFormula
{
    /// <summary>Kiểm tra tham số (không âm, hệ số hợp lệ...). Ném exception nếu cấu hình sai.</summary>
    void Validate();

    /// <summary>
    /// Lượng cần để lên từ <paramref name="level"/> lên <paramref name="level"/> + 1 (Lv1 → 2 là <c>Evaluate(1)</c>).
    /// Luôn ≥ 1. Trả <c>long</c> và báo lỗi khi tràn số, để giá lớn ở level cao không bao giờ thành số âm.
    /// </summary>
    long Evaluate(int level);
}
