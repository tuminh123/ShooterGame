using UnityEngine;

/// <summary>
/// Trạng thái bay của một viên đạn mà quỹ đạo được đọc và sửa mỗi frame (vị trí, hướng, mục tiêu, quãng đã bay…).
/// </summary>
/// <remarks>Nằm trong <see cref="ProjectileView"/>, truyền bằng <c>ref</c> nên không cấp phát.</remarks>
public struct ProjectileMoveState
{
    /// <summary>Vị trí hiện tại (world).</summary>
    public Vector2 Position;
    /// <summary>Hướng bay hiện tại, đã chuẩn hóa.</summary>
    public Vector2 Direction;
    /// <summary>Mục tiêu đang đuổi (đạn đuổi, khiên nảy); rỗng nếu không có.</summary>
    public CharacterHandle Target;
    /// <summary>Quãng đã bay (world unit) từ lúc bắn hoặc từ lần quay đầu gần nhất.</summary>
    public float Travelled;
    /// <summary>Quãng bay tối đa của lượt đi (world unit); vô cực nếu chỉ giới hạn bằng thời gian.</summary>
    public float MaxDistance;
    /// <summary>Vị trí hiện tại của người bắn (cho quỹ đạo bay về tay); chỉ hợp lệ khi <see cref="HasOwner"/>.</summary>
    public Vector2 OwnerPosition;
    public bool HasOwner;
    /// <summary>Đang bay về (boomerang / khiên): quỹ đạo bật lên, view xóa danh sách đã trúng để lượt về đánh lại được.</summary>
    public bool Returning;
    /// <summary>Quỹ đạo yêu cầu kết thúc lượt bay (đã về tới tay…).</summary>
    public bool Finished;
    /// <summary>Số lần đã nảy (nảy mép màn hình, nảy giữa quái).</summary>
    public int Bounces;
}

/// <summary>
/// Strategy của một <b>quỹ đạo đạn</b>: mỗi frame quyết định đạn bay theo hướng nào.
/// </summary>
/// <remarks>
/// Được chọn trong <see cref="ShootingData"/> bằng <c>[SerializeReference]</c>. Quỹ đạo KHÔNG giữ trạng thái (state nằm
/// trong <see cref="ProjectileMoveState"/> của từng viên), nên mọi viên đạn dùng chung instance trong asset.
/// Quỹ đạo chỉ đổi hướng/cờ; MovementSystem áp dụng tốc độ và đổi vị trí.
/// </remarks>
public interface IProjectileMove
{
    /// <summary>
    /// Quỹ đạo có cần mục tiêu để bay hay không (ví dụ đạn đuổi).
    /// Nếu true, skill sẽ tìm mục tiêu ngay cả khi data không bắt buộc phải có mục tiêu mới được bắn.
    /// </summary>
    bool TracksTarget { get; }

    /// <summary>Cập nhật hướng bay (và cờ quay về / kết thúc) cho frame này.</summary>
    void Advance(ref ProjectileMoveState state);

    /// <summary>
    /// Đạn vừa trúng <paramref name="hitTarget"/>. Trả true nếu quỹ đạo muốn bay tiếp (khiên nảy sang mục tiêu khác)
    /// dù đạn không còn lượt xuyên.
    /// </summary>
    bool OnHit(ref ProjectileMoveState state, CharacterHandle hitTarget, ICharacterSearch search, CharacterTeam opponents,
        System.Func<CharacterHandle, bool> alreadyHit);

    /// <summary>Kiểm tra thông số; ném exception nếu sai.</summary>
    void Validate();
}
