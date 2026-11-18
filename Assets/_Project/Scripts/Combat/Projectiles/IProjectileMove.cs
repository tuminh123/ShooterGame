using UnityEngine;

/// <summary>
/// Strategy của một <b>quỹ đạo đạn</b>: mỗi frame quyết định đạn bay theo hướng nào.
/// </summary>
/// <remarks>
/// Được chọn trong <see cref="ShootingData"/> bằng <c>[SerializeReference]</c>. Các quỹ đạo hiện có
/// không có trạng thái, nên mọi viên đạn dùng chung instance trong asset.
/// Quỹ đạo chỉ trả về hướng; MovementSystem áp dụng tốc độ và đổi vị trí.
/// </remarks>
public interface IProjectileMove
{
    /// <summary>
    /// Quỹ đạo có cần mục tiêu để bay hay không (ví dụ đạn đuổi).
    /// Nếu true, skill sẽ tìm mục tiêu ngay cả khi data không bắt buộc phải có mục tiêu mới được bắn.
    /// </summary>
    bool TracksTarget { get; }

    /// <summary>Hướng bay mới, đã chuẩn hóa. Mục tiêu không còn hợp lệ thì giữ hướng hiện tại.</summary>
    Vector2 GetDirection(Vector2 currentPosition, Vector2 currentDirection, CharacterHandle homingTarget);
}
