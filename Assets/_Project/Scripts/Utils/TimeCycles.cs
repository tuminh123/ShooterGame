using System;
using System.Globalization;

/// <summary>
/// Phép tính ngày/kỳ dùng chung (Mission, sau này Login Check-in). Chỉ là hàm thuần: nhận giờ vào, trả kết quả,
/// không tự đọc đồng hồ. Nguồn giờ do <see cref="IGameClock"/> hoặc bên gọi quyết định.
/// </summary>
/// <remarks>
/// Không có interface vì chỉ có một cách tính; khi có server chỉ đổi giờ đưa vào, phép tính giữ nguyên.
/// Mọi mốc là 0h UTC (7h sáng giờ Việt Nam).
/// </remarks>
public static class TimeCycles
{
    #region Days & Weeks

    /// <summary>Định dạng ngày trong data (eventStart, eventEnd…).</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>Số thứ tự của ngày UTC tính từ 0001-01-01; hai thời điểm cùng ngày luôn cho cùng một số.</summary>
    public static long DayOf(DateTime utc) => utc.Ticks / TimeSpan.TicksPerDay;

    /// <summary>0h UTC của ngày có số thứ tự <paramref name="day"/>.</summary>
    public static DateTime DayStart(long day) => new DateTime(day * TimeSpan.TicksPerDay, DateTimeKind.Utc);

    /// <summary>Số thứ tự tuần của một ngày. 0001-01-01 là thứ Hai nên mỗi tuần bắt đầu từ thứ Hai.</summary>
    public static long WeekOf(long day) => day / 7;

    /// <summary>0h UTC thứ Hai đầu tuần có số thứ tự <paramref name="week"/>.</summary>
    public static DateTime WeekStart(long week) => DayStart(week * 7);

    #endregion

    #region Months & Periods

    /// <summary>Số thứ tự tháng UTC (năm × 12 + tháng − 1); đổi lúc 0h UTC ngày 1.</summary>
    public static long MonthOf(DateTime utc) => utc.Year * 12L + utc.Month - 1;

    /// <summary>0h UTC ngày 1 của tháng có số thứ tự <paramref name="month"/>.</summary>
    public static DateTime MonthStart(long month) => new DateTime((int)(month / 12), (int)(month % 12) + 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Số ngày của tháng chứa <paramref name="utc"/>.</summary>
    public static int DaysInMonth(DateTime utc) => DateTime.DaysInMonth(utc.Year, utc.Month);

    /// <summary>Số thứ tự chu kỳ dài <paramref name="periodDays"/> ngày tính từ <paramref name="startDay"/>; chỉ gọi khi day ≥ startDay.</summary>
    public static long PeriodOf(long day, long startDay, int periodDays) => (day - startDay) / periodDays;

    #endregion

    #region Parsing

    /// <summary>Đọc ngày dạng <see cref="DateFormat"/> theo UTC. Sai định dạng thì ném FormatException/ArgumentNullException.</summary>
    public static DateTime ParseDate(string value)
        => DateTime.ParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    #endregion
}
