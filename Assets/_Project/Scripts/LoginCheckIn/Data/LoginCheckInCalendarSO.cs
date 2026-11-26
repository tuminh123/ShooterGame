using System;
using System.Collections.Generic;
using UnityEngine;

#region Day & Period

/// <summary>Một ô ngày của lịch đăng nhập: đúng một phần thưởng (bản dựng UI chỉ có một icon và một số).</summary>
[Serializable]
public sealed class LoginCheckInDay
{
    [Tooltip("Phần thưởng của ô này (tiền, nhân vật, mảnh, trang bị).")]
    [SerializeReference] private IReward reward;

    /// <summary>Phần thưởng của ô.</summary>
    public IReward Reward => reward;

    /// <summary>Gói thưởng một phần tử, đúng kiểu mà IRewardGrant và RewardSlotFactory nhận.</summary>
    public IReadOnlyList<IReward> Rewards => new[] { reward };
}

/// <summary>Kết quả tính kỳ tại một thời điểm: mã kỳ, lúc kết thúc, số ô nhận được và có lặp lại khi nhận đủ không.</summary>
public readonly struct LoginCheckInPeriod
{
    /// <summary>Mã kỳ; đổi nghĩa là sang kỳ mới (reset tiến độ).</summary>
    public readonly long Id;
    /// <summary>Lúc kỳ kết thúc (UTC); SevenDay không có hạn nên là DateTime.MaxValue.</summary>
    public readonly DateTime EndsAt;
    /// <summary>Số ô nhận được trong kỳ này (Month: không quá số ngày của tháng).</summary>
    public readonly int DayCount;
    /// <summary>Nhận đủ mọi ô thì sang ngày sau bắt đầu vòng mới (chỉ SevenDay).</summary>
    public readonly bool RestartsWhenComplete;

    public LoginCheckInPeriod(long id, DateTime endsAt, int dayCount, bool restartsWhenComplete)
    {
        Id = id;
        EndsAt = endsAt;
        DayCount = dayCount;
        RestartsWhenComplete = restartsWhenComplete;
    }
}

#endregion

/// <summary>
/// Một lịch đăng nhập (7 ngày, theo tháng, theo chu kỳ N ngày hoặc event). Mỗi ngày nhận một ô kế tiếp; bỏ ngày không mất gì
/// và không quay về ô 1. Asset chỉ chứa cấu hình; tiến độ nằm ở <see cref="LoginCheckInCalendar"/>.
/// </summary>
[CreateAssetMenu(fileName = "LoginCheckIn", menuName = "Shooter Game/Login Check-in/Calendar")]
public sealed class LoginCheckInCalendarSO : ScriptableObject
{
    #region Configuration

    private const int SevenDayCount = 7;
    private const int MaxMonthDays = 31;

    [Tooltip("ID ổn định được lưu trong save; không trống, không trùng giữa các lịch.")]
    [SerializeField] private string id;
    [Tooltip("Chữ trên ruy băng tiêu đề của popup.")]
    [SerializeField] private string displayName;
    [Tooltip("SevenDay: 7 ô lặp lại. Month: theo tháng UTC. Period: chu kỳ N ngày. Event: mở trong khoảng ngày.")]
    [SerializeField] private LoginCheckInCycle cycle;
    [Tooltip("Nơi lấy giờ. Device: giờ máy, chỉnh được (đang test). Server: chống chỉnh giờ, mất mạng thì lịch bị ẩn.")]
    [SerializeField] private ClockSource clock = ClockSource.Device;
    [Tooltip("Chỉ dùng cho Period: độ dài một chu kỳ, theo ngày.")]
    [SerializeField, Min(1)] private int periodDays = 30;
    [Tooltip("Chỉ dùng cho Period: ngày bắt đầu chu kỳ đầu tiên (UTC), dạng yyyy-MM-dd; chung cho mọi người chơi.")]
    [SerializeField] private string periodStart;
    [Tooltip("Chỉ dùng cho Event: ngày mở (UTC), dạng yyyy-MM-dd. Không đổi khi event đang chạy (tiến độ về 0).")]
    [SerializeField] private string eventStart;
    [Tooltip("Chỉ dùng cho Event: ngày đóng (UTC, không gồm ngày này). Gia hạn event thì sửa ngày này, tiến độ giữ nguyên.")]
    [SerializeField] private string eventEnd;
    [SerializeField] private List<LoginCheckInDay> days = new List<LoginCheckInDay>();

    /// <summary>ID ổn định.</summary>
    public string Id => id;

    /// <summary>Chữ trên ruy băng tiêu đề.</summary>
    public string DisplayName => displayName;

    /// <summary>Cách tính kỳ.</summary>
    public LoginCheckInCycle Cycle => cycle;

    /// <summary>Nơi lấy giờ (đọc qua <see cref="IGameClock"/>).</summary>
    public ClockSource Clock => clock;

    /// <summary>Các ô theo thứ tự ngày.</summary>
    public IReadOnlyList<LoginCheckInDay> Days => days;

    #endregion

    #region Period

    /// <summary>
    /// Nơi DUY NHẤT tính kỳ của lịch tại <paramref name="utcNow"/>. False nếu lịch đang đóng (Period chưa tới ngày bắt đầu,
    /// Event ngoài khoảng ngày). Hàm thuần: giờ do bên gọi đưa vào theo <see cref="Clock"/>.
    /// </summary>
    public bool TryGetPeriod(DateTime utcNow, out LoginCheckInPeriod period)
    {
        long day = TimeCycles.DayOf(utcNow);
        switch (cycle)
        {
            case LoginCheckInCycle.SevenDay:
                period = new LoginCheckInPeriod(0, DateTime.MaxValue, days.Count, true);
                return true;
            case LoginCheckInCycle.Month:
                long month = TimeCycles.MonthOf(utcNow);
                period = new LoginCheckInPeriod(month, TimeCycles.MonthStart(month + 1),
                    Math.Min(days.Count, TimeCycles.DaysInMonth(utcNow)), false);
                return true;
            case LoginCheckInCycle.Period:
                long startDay = TimeCycles.DayOf(TimeCycles.ParseDate(periodStart));
                if (day < startDay)
                {
                    period = default;
                    return false;
                }
                long index = TimeCycles.PeriodOf(day, startDay, periodDays);
                period = new LoginCheckInPeriod(index, TimeCycles.DayStart(startDay + (index + 1) * periodDays), days.Count, false);
                return true;
            case LoginCheckInCycle.Event:
                // Mã kỳ theo ngày mở: gia hạn eventEnd không làm mất tiến độ.
                DateTime start = TimeCycles.ParseDate(eventStart);
                DateTime end = TimeCycles.ParseDate(eventEnd);
                period = new LoginCheckInPeriod(TimeCycles.DayOf(start), end, days.Count, false);
                return utcNow >= start && utcNow < end;
            default:
                throw new ArgumentOutOfRangeException(nameof(cycle), cycle, "Unknown login check-in cycle.");
        }
    }

    #endregion

    #region Validation

    /// <summary>ID, loại, ngày, số ô và phần thưởng hợp lệ. Việc thưởng khớp database nhân vật/trang bị do Progression kiểm tra.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException($"{name}: lịch chưa có ID.");
        if (!Enum.IsDefined(typeof(LoginCheckInCycle), cycle)) throw new InvalidOperationException($"{name}: loại lịch không hợp lệ.");
        if (!Enum.IsDefined(typeof(ClockSource), clock)) throw new InvalidOperationException($"{name}: nguồn giờ không hợp lệ.");
        if (days.Count == 0) throw new InvalidOperationException($"{name}: cần ít nhất một ô ngày.");
        int capacity = Capacity();
        if (days.Count > capacity)
            throw new InvalidOperationException($"{name}: có {days.Count} ô nhưng kỳ {cycle} chỉ chứa tối đa {capacity} ngày.");
        if (cycle == LoginCheckInCycle.SevenDay && days.Count != SevenDayCount)
            throw new InvalidOperationException($"{name}: lịch SevenDay cần đúng {SevenDayCount} ô.");
        for (int i = 0; i < days.Count; i++)
        {
            if (days[i] == null || days[i].Reward == null)
                throw new InvalidOperationException($"{name}: ô ngày {i + 1} chưa có phần thưởng.");
            RewardBatch.From(days[i].Rewards);
        }
    }

    // Số ngày tối đa của một kỳ; ngày sai định dạng thì báo rõ thay vì để FormatException lộ ra Inspector.
    private int Capacity()
    {
        try
        {
            switch (cycle)
            {
                case LoginCheckInCycle.SevenDay: return SevenDayCount;
                case LoginCheckInCycle.Month: return MaxMonthDays;
                case LoginCheckInCycle.Period:
                    TimeCycles.ParseDate(periodStart);
                    return periodDays;
                default:
                    DateTime start = TimeCycles.ParseDate(eventStart);
                    DateTime end = TimeCycles.ParseDate(eventEnd);
                    if (start >= end) throw new InvalidOperationException($"{name}: Event Start phải trước Event End.");
                    return (int)Math.Ceiling((end - start).TotalDays);
            }
        }
        catch (Exception exception) when (exception is FormatException || exception is ArgumentNullException)
        {
            throw new InvalidOperationException($"{name}: ngày phải có dạng {TimeCycles.DateFormat}.");
        }
    }

    #endregion
}
