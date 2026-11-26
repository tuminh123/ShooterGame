using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm tra miền Login Check-in trong Edit Mode bằng Save/Progression thật trên một FILE SAVE TẠM riêng (xóa khi xong),
/// nên không chạm hồ sơ người chơi. Giờ được giả bằng <see cref="FakeClock"/> để tua ngày/tháng.
/// </summary>
public static class LoginCheckInVerification
{
    #region Entry

    private const string SaveFile = "LoginCheckInVerification.es3";
    private static readonly List<UnityEngine.Object> Temporary = new List<UnityEngine.Object>();

    [MenuItem("Shooter Game/Verification/Check Login Check-in")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run Check Login Check-in in Edit Mode.");
        Require(!ES3.FileExists(SaveFile), "Temporary save already exists: " + SaveFile);
        try
        {
            CheckConfiguredCalendars();
            CheckPeriods();
            CheckSevenDay();
            CheckMonthReset();
            CheckEventWindow();
            CheckServerWithoutTime();
            Debug.Log("[LoginCheckIn] PASS: configured calendars, periods (month/period/event), one box per day, skipped day keeps next box, " +
                      "reload, seven-day restart, month reset, event window + extension, server clock without time.");
        }
        finally
        {
            ES3.DeleteFile(SaveFile);
            foreach (UnityEngine.Object item in Temporary) UnityEngine.Object.DestroyImmediate(item);
            Temporary.Clear();
        }
    }

    #endregion

    #region Checks

    // Mọi lịch trong project hợp lệ và mọi phần thưởng trao được với database nhân vật/trang bị thật.
    private static void CheckConfiguredCalendars()
    {
        using (var world = new World(Array.Empty<LoginCheckInCalendarSO>(), new FakeClock()))
            foreach (LoginCheckInCalendarSO calendar in Assets<LoginCheckInCalendarSO>())
            {
                calendar.Validate();
                foreach (LoginCheckInDay day in calendar.Days) world.Progression.ValidateRewards(day.Rewards);
            }
        ES3.DeleteFile(SaveFile);
    }

    // Hàm thuần: ranh giới tháng, chu kỳ N ngày và cửa sổ event.
    private static void CheckPeriods()
    {
        LoginCheckInCalendarSO month = Calendar("month", LoginCheckInCycle.Month, 31);
        Require(month.TryGetPeriod(Utc(2026, 11, 30, 23, 59), out LoginCheckInPeriod november), "Month should always be open.");
        month.TryGetPeriod(Utc(2026, 12, 1, 0, 0), out LoginCheckInPeriod december);
        Require(november.Id != december.Id && november.EndsAt == Utc(2026, 12, 1, 0, 0), "Month did not roll on the 1st (UTC).");
        Require(november.DayCount == 30 && december.DayCount == 31, "Month day count must follow the calendar month.");

        LoginCheckInCalendarSO period = Calendar("period", LoginCheckInCycle.Period, 30, periodStart: "2026-11-01");
        Require(!period.TryGetPeriod(Utc(2026, 10, 31, 23, 59), out _), "Period opened before its start date.");
        period.TryGetPeriod(Utc(2026, 11, 30, 23, 59), out LoginCheckInPeriod first);
        period.TryGetPeriod(Utc(2026, 12, 1, 0, 0), out LoginCheckInPeriod second);
        Require(first.Id == 0 && second.Id == 1 && first.EndsAt == Utc(2026, 12, 1, 0, 0), "Period did not roll every Period Days.");

        LoginCheckInCalendarSO window = Calendar("event", LoginCheckInCycle.Event, 3, eventStart: "2026-11-20", eventEnd: "2026-11-23");
        Require(!window.TryGetPeriod(Utc(2026, 11, 19, 23, 59), out _), "Event opened early.");
        Require(window.TryGetPeriod(Utc(2026, 11, 22, 23, 59), out _), "Event closed early.");
        Require(!window.TryGetPeriod(Utc(2026, 11, 23, 0, 0), out _), "Event End day must not be included.");
    }

    private static void CheckSevenDay()
    {
        var clock = new FakeClock { Now = Utc(2026, 11, 2, 8, 0) };
        LoginCheckInCalendarSO data = Calendar("seven", LoginCheckInCycle.SevenDay, 7);
        using (var world = new World(new[] { data }, clock))
        {
            LoginCheckInCalendar calendar = world.Manager.Calendars[0];
            long gold = world.Progression.GetBalance(CurrencyType.Gold);
            Require(calendar.CanClaim && world.Manager.HasClaimable, "Day 1 should be claimable.");
            Require(world.Manager.TryClaim(calendar, out RewardResult result, out _) && result != null, "Could not claim day 1.");
            Require(world.Progression.GetBalance(CurrencyType.Gold) == gold + 100, "Day 1 reward was not granted.");
            Require(!world.Manager.TryClaim(calendar, out _, out _) && calendar.ClaimedDays == 1, "Claimed twice in one day.");
            Require(calendar.GetDayState(0) == LoginCheckInDayState.Claimed && calendar.GetDayState(1) == LoginCheckInDayState.Locked,
                "Day states are wrong after claiming.");

            clock.Now = clock.Now.AddDays(2); // Bỏ một ngày.
            world.Manager.Refresh();
            Require(calendar.GetDayState(1) == LoginCheckInDayState.Claimable, "Skipping a day must keep the next box claimable.");
            Require(world.Manager.TryClaim(calendar, out _, out _) && calendar.ClaimedDays == 2, "Could not claim after a skipped day.");
        }
        using (var world = new World(new[] { data }, clock))
        {
            LoginCheckInCalendar calendar = world.Manager.Calendars[0];
            Require(calendar.ClaimedDays == 2 && !calendar.CanClaim, "Progress or today's claim was not saved.");
            for (int day = 3; day <= 7; day++)
            {
                clock.Now = clock.Now.AddDays(1);
                world.Manager.Refresh();
                Require(world.Manager.TryClaim(calendar, out _, out _), "Day " + day + " was not claimable.");
            }
            Require(calendar.ClaimedDays == 7 && !calendar.CanClaim, "A completed calendar must wait for the next day.");
            clock.Now = clock.Now.AddDays(1);
            world.Manager.Refresh();
            Require(calendar.ClaimedDays == 0 && calendar.CanClaim, "Seven-day calendar did not restart the day after completion.");
        }
        ES3.DeleteFile(SaveFile);
    }

    private static void CheckMonthReset()
    {
        var clock = new FakeClock { Now = Utc(2026, 11, 30, 10, 0) };
        using (var world = new World(new[] { Calendar("month", LoginCheckInCycle.Month, 31) }, clock))
        {
            LoginCheckInCalendar calendar = world.Manager.Calendars[0];
            Require(calendar.DayCount == 30 && world.Manager.TryClaim(calendar, out _, out _), "Could not claim in November.");
            clock.Now = Utc(2026, 12, 1, 1, 0);
            world.Manager.Refresh();
            Require(calendar.ClaimedDays == 0 && calendar.CanClaim && calendar.DayCount == 31, "A new month did not restart the calendar.");
        }
        ES3.DeleteFile(SaveFile);
    }

    private static void CheckEventWindow()
    {
        var clock = new FakeClock { Now = Utc(2026, 11, 19, 12, 0) };
        LoginCheckInCalendarSO data = Calendar("event", LoginCheckInCycle.Event, 3, eventStart: "2026-11-20", eventEnd: "2026-11-23");
        using (var world = new World(new[] { data }, clock))
        {
            LoginCheckInCalendar calendar = world.Manager.Calendars[0];
            Require(!calendar.IsOpen && !world.Manager.TryClaim(calendar, out _, out _), "Claimed before the event opened.");
            clock.Now = Utc(2026, 11, 20, 12, 0);
            world.Manager.Refresh();
            Require(world.Manager.TryClaim(calendar, out _, out _), "Could not claim on the first event day.");
            clock.Now = Utc(2026, 11, 23, 12, 0);
            world.Manager.Refresh();
            Require(!calendar.IsOpen && !world.Manager.HasClaimable, "Event stayed open after Event End.");

            // Gia hạn: chỉ sửa Event End, tiến độ phải giữ nguyên.
            var settings = new SerializedObject(data);
            settings.FindProperty("eventEnd").stringValue = "2026-11-25";
            settings.ApplyModifiedPropertiesWithoutUndo();
            world.Manager.Refresh();
            Require(calendar.IsOpen && calendar.ClaimedDays == 1 && calendar.CanClaim, "Extending Event End lost progress.");
        }
        ES3.DeleteFile(SaveFile);
    }

    // Nguồn Server chưa có giờ: lịch đóng; có giờ (Clock.Changed) thì mở ngay mà không cần ai gọi Refresh.
    private static void CheckServerWithoutTime()
    {
        var clock = new FakeClock { Now = Utc(2026, 11, 2, 8, 0), HasServerTime = false };
        LoginCheckInCalendarSO data = Calendar("server", LoginCheckInCycle.SevenDay, 7, clock: ClockSource.Server);
        using (var world = new World(new[] { data }, clock))
        {
            LoginCheckInCalendar calendar = world.Manager.Calendars[0];
            Require(!calendar.IsOpen && !world.Manager.HasClaimable && !world.Manager.TryClaim(calendar, out _, out _),
                "A Server calendar opened without server time.");
            clock.HasServerTime = true;
            clock.RaiseChanged();
            Require(calendar.IsOpen && calendar.CanClaim, "A Server calendar did not open when server time arrived.");
        }
        ES3.DeleteFile(SaveFile);
    }

    #endregion

    #region World

    // Save + Progression thật, đồng hồ giả. Dùng lại cùng một FakeClock giữa hai World để thử nạp lại save.
    private sealed class World : IDisposable
    {
        private readonly SaveManager save;
        public readonly ProgressionManager Progression;
        public readonly LoginCheckInManager Manager;

        public World(IEnumerable<LoginCheckInCalendarSO> calendars, FakeClock clock)
        {
            save = new SaveManager(new SaveManager.Settings(SaveFile));
            save.Initialize();
            Progression = new ProgressionManager(save, Assets<CharacterDatabaseSO>().Single(), Assets<LevelDatabaseSO>().Single(),
                Assets<EquipmentDatabaseSO>().Single());
            Progression.Initialize();
            Manager = new LoginCheckInManager(new LoginCheckInManager.Settings(calendars.ToList()), save, clock, Progression);
            Manager.Initialize();
        }

        public void Dispose()
        {
            Manager.Dispose();
            Progression.Dispose();
            save.Dispose();
        }
    }

    /// <summary>Đồng hồ giả: giờ máy luôn có; giờ server chỉ có khi <see cref="HasServerTime"/>.</summary>
    private sealed class FakeClock : IGameClock
    {
        public DateTime Now;
        public bool HasServerTime = true;

        public event Action Changed;

        public bool TryGetUtcNow(ClockSource source, out DateTime utcNow)
        {
            utcNow = Now;
            return source == ClockSource.Device || HasServerTime;
        }

        public void RaiseChanged() => Changed?.Invoke();
    }

    #endregion

    #region Helpers

    // Ô ngày i thưởng 100 × (i + 1) vàng, để kiểm tra đúng ô được trao.
    private static LoginCheckInCalendarSO Calendar(string id, LoginCheckInCycle cycle, int dayCount, string periodStart = null,
        string eventStart = null, string eventEnd = null, ClockSource clock = ClockSource.Device)
    {
        var calendar = ScriptableObject.CreateInstance<LoginCheckInCalendarSO>();
        Temporary.Add(calendar);
        var data = new SerializedObject(calendar);
        data.FindProperty("id").stringValue = id;
        data.FindProperty("cycle").intValue = (int)cycle;
        data.FindProperty("clock").intValue = (int)clock;
        data.FindProperty("periodDays").intValue = 30;
        data.FindProperty("periodStart").stringValue = periodStart ?? string.Empty;
        data.FindProperty("eventStart").stringValue = eventStart ?? string.Empty;
        data.FindProperty("eventEnd").stringValue = eventEnd ?? string.Empty;
        SerializedProperty days = data.FindProperty("days");
        days.arraySize = dayCount;
        for (int i = 0; i < dayCount; i++)
            days.GetArrayElementAtIndex(i).FindPropertyRelative("reward").managedReferenceValue = new CurrencyReward(CurrencyType.Gold, 100 * (i + 1));
        data.ApplyModifiedPropertiesWithoutUndo();
        calendar.Validate();
        return calendar;
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
        => new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object
        => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[LoginCheckIn] " + message);
    }

    #endregion
}
