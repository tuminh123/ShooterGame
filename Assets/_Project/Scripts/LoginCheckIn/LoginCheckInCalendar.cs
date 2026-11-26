using System;

/// <summary>
/// Một lịch đăng nhập đang chạy: kỳ hiện tại, số ô đã nhận và ngày nhận gần nhất. Ra ngoài chỉ đọc;
/// chỉ LoginCheckInManager đổi trạng thái.
/// </summary>
public sealed class LoginCheckInCalendar
{
    #region State

    private long cycle; // Mã kỳ của tiến độ đang giữ.
    private long lastClaimDay; // Ngày UTC của lần nhận gần nhất; 0 = chưa nhận lần nào.
    private long today; // Ngày UTC ở lần Refresh gần nhất khi lịch mở.

    /// <summary>Cấu hình của lịch.</summary>
    public LoginCheckInCalendarSO Data { get; }

    /// <summary>Đang mở (Event ngoài thời gian, Period chưa tới ngày bắt đầu, hoặc nguồn giờ chưa có giờ thì đóng).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Lúc kỳ hiện tại kết thúc (UTC); chỉ có nghĩa khi <see cref="IsOpen"/>.</summary>
    public DateTime EndsAt { get; private set; }

    /// <summary>Số ô nhận được trong kỳ này (Month: không quá số ngày của tháng).</summary>
    public int DayCount { get; private set; }

    /// <summary>Số ô đã nhận trong kỳ; ô kế tiếp có chỉ số này.</summary>
    public int ClaimedDays { get; private set; }

    /// <summary>Nhận được ô kế tiếp ngay lúc này: lịch mở, còn ô và hôm nay chưa nhận.</summary>
    public bool CanClaim => IsOpen && ClaimedDays < DayCount && lastClaimDay != today;

    #endregion

    #region Constructor

    internal LoginCheckInCalendar(LoginCheckInCalendarSO data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    #endregion

    #region Read

    /// <summary>Trạng thái ô <paramref name="index"/> để UI vẽ.</summary>
    public LoginCheckInDayState GetDayState(int index)
    {
        if (index < ClaimedDays) return LoginCheckInDayState.Claimed;
        return index == ClaimedDays && CanClaim ? LoginCheckInDayState.Claimable : LoginCheckInDayState.Locked;
    }

    #endregion

    #region Update (LoginCheckInManager)

    /// <summary>
    /// Cập nhật kỳ theo giờ. Sang kỳ mới thì bắt đầu lại từ ô 1; SevenDay nhận đủ thì sang ngày sau bắt đầu vòng mới.
    /// <paramref name="hasTime"/> = false (nguồn giờ chưa có giờ): đóng lịch nhưng giữ nguyên tiến độ. Trả về UI có cần vẽ lại không.
    /// </summary>
    /// <remarks>Không đánh dấu cần ghi: lần nạp sau tính lại đúng kết quả này từ mã kỳ đã lưu, nên chỉ ghi khi nhận quà.</remarks>
    internal bool Refresh(DateTime utcNow, bool hasTime)
    {
        // Gọi TryGetPeriod trước để biến out luôn được gán; giờ mặc định (chưa có giờ) vẫn tính được, chỉ bị bỏ qua.
        bool open = Data.TryGetPeriod(utcNow, out LoginCheckInPeriod period) && hasTime;
        bool changed = open != IsOpen;
        IsOpen = open;
        if (!open) return changed;

        long day = TimeCycles.DayOf(utcNow);
        changed |= day != today || period.DayCount != DayCount;
        today = day;
        EndsAt = period.EndsAt;
        DayCount = period.DayCount;
        if (period.Id != cycle)
        {
            cycle = period.Id;
            ClaimedDays = 0;
            return true;
        }
        if (period.RestartsWhenComplete && ClaimedDays >= DayCount && lastClaimDay < today)
        {
            ClaimedDays = 0;
            return true;
        }
        return changed;
    }

    /// <summary>Đánh dấu đã nhận ô kế tiếp hôm nay. Bên gọi đã kiểm tra <see cref="CanClaim"/>.</summary>
    internal void MarkClaimed()
    {
        ClaimedDays++;
        lastClaimDay = today;
    }

    /// <summary>Ô kế tiếp sẽ nhận.</summary>
    internal LoginCheckInDay NextDay => Data.Days[ClaimedDays];

    #endregion

    #region Save

    /// <summary>Nạp tiến độ đã lưu; số ô bị kẹp theo cấu hình hiện tại (asset có thể bớt ô).</summary>
    internal void Restore(LoginCheckInSaveData.Calendar saved)
    {
        cycle = saved.cycle;
        ClaimedDays = Math.Min(saved.claimedDays, Data.Days.Count);
        lastClaimDay = saved.lastClaimDay;
    }

    /// <summary>Snapshot để ghi.</summary>
    internal LoginCheckInSaveData.Calendar ToSaveData() => new LoginCheckInSaveData.Calendar
    {
        calendarId = Data.Id,
        cycle = cycle,
        claimedDays = ClaimedDays,
        lastClaimDay = lastClaimDay
    };

    #endregion
}
