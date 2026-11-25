using System;
using System.Collections.Generic;

// Ranh giới của miền Mission, do MissionManager implement. Mission nằm trên mọi miền gameplay và chỉ NGHE event của chúng;
// chỉ UI dùng Mission. MissionBoard/MissionRuntime ra ngoài chỉ đọc.

#region Missions (màn Mission, nút Quest ở Home)

/// <summary>Đọc các nhóm mission và nhận thưởng. Dùng bởi màn Mission và Home.</summary>
public interface IMissions
{
    /// <summary>Mọi nhóm theo thứ tự cấu hình, gồm cả nhóm đang đóng (<see cref="MissionBoard.IsOpen"/>).</summary>
    IReadOnlyList<MissionBoard> Boards { get; }

    /// <summary>Có mission nào đang mở và nhận được thưởng (để hiện chấm báo).</summary>
    bool HasClaimable { get; }

    /// <summary>Giờ UTC mà Mission đang dùng, để UI đếm ngược cùng nguồn giờ.</summary>
    DateTime UtcNow { get; }

    /// <summary>Kiểm tra sang kỳ mới hay chưa; có đổi thì phát <see cref="Changed"/>. UI gọi khi mở màn và khi đếm ngược về 0.</summary>
    void Refresh();

    /// <summary>
    /// Nhận cả gói thưởng nếu mission đã xong, chưa nhận và đang mở. <paramref name="result"/> là thưởng thực tế (null nếu không nhận được);
    /// <paramref name="saved"/> = false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    bool TryClaim(MissionRuntime mission, out RewardResult result, out bool saved);

    /// <summary>Tiến độ, trạng thái nhận hoặc kỳ vừa đổi.</summary>
    event Action Changed;
}

#endregion
