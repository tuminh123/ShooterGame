/// <summary>
/// Chỗ cắm kiểm tra giờ/quyền nhận thưởng. Hiện chưa có server nên chưa kiểm tra gì; khi có server thì viết thêm một class
/// implement interface này và đổi đúng một dòng <c>new</c> trong constructor của <see cref="MissionManager"/>.
/// </summary>
/// <remarks>
/// Ngoại lệ của quy tắc "interface khi có từ 2 hành vi": bản server đã được dự kiến trước.
/// Nếu server phải chờ mạng thì đổi kiểu trả về sang UniTask&lt;bool&gt;; chỉ MissionManager.TryClaim gọi hàm này.
/// </remarks>
public interface IMissionTimeCheck
{
    /// <summary>True nếu được phép nhận thưởng lúc này.</summary>
    bool Check();
}

/// <summary>Bản trống: luôn cho phép.</summary>
public sealed class EmptyMissionTimeCheck : IMissionTimeCheck
{
    /// <inheritdoc/>
    public bool Check() => true;
}
