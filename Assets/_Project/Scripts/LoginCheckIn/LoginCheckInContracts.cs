using System;
using System.Collections.Generic;

// Ranh giới của miền Login Check-in, do LoginCheckInManager implement. Chỉ UI dùng (popup đăng nhập, nút ở Home).
// LoginCheckInCalendar ra ngoài chỉ đọc.

#region Login Check-in (popup đăng nhập, nút ở Home)

/// <summary>Đọc các lịch đăng nhập và nhận quà của ngày hôm nay.</summary>
public interface ILoginCheckIn
{
    /// <summary>Mọi lịch theo thứ tự cấu hình, gồm cả lịch đang đóng (<see cref="LoginCheckInCalendar.IsOpen"/>).</summary>
    IReadOnlyList<LoginCheckInCalendar> Calendars { get; }

    /// <summary>Có lịch nào nhận được quà hôm nay (để hiện chấm than đỏ và tự mở popup).</summary>
    bool HasClaimable { get; }

    /// <summary>Kiểm tra sang ngày/kỳ mới; có đổi thì phát <see cref="Changed"/>. UI gọi khi mở và định kỳ khi đang ở Home.</summary>
    void Refresh();

    /// <summary>
    /// Nhận ô kế tiếp của <paramref name="calendar"/> nếu hôm nay chưa nhận. <paramref name="result"/> là thưởng thực tế (null nếu không nhận được);
    /// <paramref name="saved"/> = false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    bool TryClaim(LoginCheckInCalendar calendar, out RewardResult result, out bool saved);

    /// <summary>Sang ngày/kỳ mới, lịch mở/đóng hoặc vừa nhận quà.</summary>
    event Action Changed;
}

#endregion
