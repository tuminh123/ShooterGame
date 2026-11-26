using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Login Check-in: tính kỳ từng lịch, cho nhận một ô mỗi ngày, trao thưởng và lưu.
/// Đây là class <b>duy nhất</b> của Login Check-in được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// LoginCheckInManager
/// └── LoginCheckInCalendar[]   mỗi asset một lịch: kỳ hiện tại + số ô đã nhận (key login_check_in)
/// </code>
/// Chiều phụ thuộc: Login Check-in → Progression (trao thưởng) / Clock (giờ theo LoginCheckInCalendarSO.Clock) / Save.
/// Chỉ ghi file khi nhận quà, cùng một lần ghi với phần thưởng: đổi kỳ chỉ tính lại trong RAM và tính ra y hệt ở lần mở sau.
/// </remarks>
public sealed class LoginCheckInManager : DomainManager, ILoginCheckIn, IInitializable, IDisposable
{
    #region Settings

    /// <summary>Các lịch đăng nhập, installer gán từ Inspector.</summary>
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Thứ tự này là thứ tự popup tự mở khi vào Home (lịch đầu tiên còn quà hiện trước).")]
        [SerializeField] private List<LoginCheckInCalendarSO> calendars = new List<LoginCheckInCalendarSO>();

        public Settings() { }

        public Settings(List<LoginCheckInCalendarSO> calendars) => this.calendars = calendars;

        /// <summary>Các lịch theo thứ tự.</summary>
        public IReadOnlyList<LoginCheckInCalendarSO> Calendars => calendars;

        /// <summary>Mọi lịch hợp lệ và không trùng ID.</summary>
        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (LoginCheckInCalendarSO calendar in calendars)
            {
                if (calendar == null) throw new InvalidOperationException("Login check-in settings has an empty calendar.");
                calendar.Validate();
                if (!ids.Add(calendar.Id)) throw new InvalidOperationException("Duplicate login check-in calendar ID: " + calendar.Id);
            }
        }
    }

    #endregion

    #region Fields

    private readonly ISaveStore saves;
    private readonly IGameClock clock;
    private readonly IRewardGrant rewards;
    private readonly List<LoginCheckInCalendar> calendars = new List<LoginCheckInCalendar>();
    private bool loaded;
    private bool claiming; // Chặn nhận lồng nhau từ callback của chính lần nhận.
    private bool disposed;

    #endregion

    #region Constructor

    /// <summary>Dựng các lịch và nghe đồng hồ; chưa đọc save.</summary>
    public LoginCheckInManager(Settings settings, ISaveStore saves, IGameClock gameClock, IRewardGrant rewardGrant)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        rewards = rewardGrant ?? throw new ArgumentNullException(nameof(rewardGrant));
        foreach (LoginCheckInCalendarSO calendar in settings.Calendars)
            calendars.Add(new LoginCheckInCalendar(calendar));
        clock.Changed += Refresh; // Server vừa trả giờ hoặc vừa mất giờ: lịch dùng giờ server mở/đóng ngay.
    }

    #endregion

    #region Lifecycle

    /// <summary>Đọc tiến độ sau Save và Progression; thưởng cấu hình sai chỉ báo lỗi, không chặn game.</summary>
    public override void Initialize()
    {
        if (disposed || loaded || !saves.IsLoaded) return;
        LoginCheckInSaveData data = saves.Load(GameConstants.SaveKeys.LoginCheckIn, new LoginCheckInSaveData(), LoginCheckInSaveData.Validate);
        foreach (LoginCheckInSaveData.Calendar saved in data.calendars)
            foreach (LoginCheckInCalendar calendar in calendars)
                if (string.Equals(saved.calendarId, calendar.Data.Id, StringComparison.Ordinal)) calendar.Restore(saved);
        loaded = true;
        RefreshCalendars();
        foreach (LoginCheckInCalendar calendar in calendars)
            foreach (LoginCheckInDay day in calendar.Data.Days)
            {
                try { rewards.ValidateRewards(day.Rewards); }
                catch (Exception exception) { Debug.LogException(exception, calendar.Data); }
            }
    }

    /// <summary>Gỡ listener. Không ghi ở đây: mọi thay đổi cần lưu đã ghi lúc nhận quà.</summary>
    public override void Dispose()
    {
        if (disposed) return;
        clock.Changed -= Refresh;
        disposed = true;
        Changed = null;
    }

    #endregion

    #region ILoginCheckIn

    /// <inheritdoc/>
    public IReadOnlyList<LoginCheckInCalendar> Calendars => calendars;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public bool HasClaimable
    {
        get
        {
            foreach (LoginCheckInCalendar calendar in calendars)
                if (calendar.CanClaim) return true;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Refresh()
    {
        if (loaded && !disposed && RefreshCalendars()) NotifyChanged();
    }

    /// <inheritdoc/>
    public bool TryClaim(LoginCheckInCalendar calendar, out RewardResult result, out bool saved)
    {
        result = null;
        saved = false;
        if (calendar == null) throw new ArgumentNullException(nameof(calendar));
        if (!loaded || disposed || claiming) return false;
        Refresh(); // Có thể vừa qua ngày mới trong lúc popup còn mở.
        if (!calendars.Contains(calendar) || !calendar.CanClaim) return false;

        claiming = true;
        try
        {
            IReadOnlyList<IReward> reward = calendar.NextDay.Rewards;
            // Kiểm tra gói TRƯỚC khi đánh dấu: cấu hình sai thì ném lỗi mà không mất lượt nhận.
            rewards.ValidateRewards(reward);
            calendar.MarkClaimed(); // Trước snapshot: cờ đã nhận nằm trong cùng lần ghi với phần thưởng.
            result = rewards.GrantRewards(reward, SaveEntry.Of(GameConstants.SaveKeys.LoginCheckIn, CreateSaveData()), out saved);
        }
        finally { claiming = false; }
        NotifyChanged();
        return true;
    }

    #endregion

    #region Private Helpers

    // Mỗi lịch đọc giờ từ nguồn asset của nó chọn; miền này không biết nguồn nào là máy hay server.
    private bool RefreshCalendars()
    {
        bool changed = false;
        foreach (LoginCheckInCalendar calendar in calendars)
        {
            bool hasTime = clock.TryGetUtcNow(calendar.Data.Clock, out DateTime now);
            changed |= calendar.Refresh(now, hasTime);
        }
        return changed;
    }

    private LoginCheckInSaveData CreateSaveData()
    {
        var data = new LoginCheckInSaveData();
        foreach (LoginCheckInCalendar calendar in calendars) data.calendars.Add(calendar.ToSaveData());
        LoginCheckInSaveData.Validate(data);
        return data;
    }

    // Lỗi trình bày UI không được làm tưởng chưa nhận thưởng.
    private void NotifyChanged()
    {
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion
}
