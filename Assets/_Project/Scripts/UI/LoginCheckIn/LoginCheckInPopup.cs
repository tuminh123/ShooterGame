using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Popup quà đăng nhập đè lên Home: mỗi lịch một panel dựng sẵn trong scene (7 ngày, 30 ô). Bấm ô "hôm nay" để nhận,
/// rồi hiện <see cref="RewardRevealView"/> dùng chung.
/// </summary>
/// <remarks>
/// Là overlay do UIManager quản lý (<see cref="IsOverlay"/>): không làm ẩn Home, UIManager tắt nó lúc khởi động, tự bật object
/// khi mở (scene lưu Login bật hay tắt đều như nhau) và đóng nó khi màn dưới đổi. Nút ở Home gọi <see cref="Open"/>;
/// HomeController gọi <see cref="TryAutoOpen"/> để tự mở một lần mỗi phiên — mở tự động thì Close chuyển sang lịch kế tiếp còn quà.
/// </remarks>
public sealed class LoginCheckInPopup : UIPanelController
{
    #region Panels

    /// <summary>Một panel dựng sẵn và lịch mà nó hiển thị.</summary>
    [Serializable]
    private sealed class CalendarPanel
    {
        [Tooltip("ID của LoginCheckInCalendarSO hiện ở panel này.")]
        public string calendarId;
        public GameObject root;
        [Tooltip("Hiệu ứng mở/đóng; Items là các phần trong panel (không gồm nền tối).")]
        public StaggeredPopupTween popupTween;
        public TMP_Text title;
        public Button closeButton;
        [Tooltip("{0} = số thứ tự ngày, ví dụ \"DAY {0}\" hoặc \"{0}\".")]
        public string dayFormat = "DAY {0}";
        [Tooltip("Các thẻ theo thứ tự ngày.")]
        public LoginCheckInDayView[] days = Array.Empty<LoginCheckInDayView>();
    }

    [SerializeField] private CalendarPanel[] panels = Array.Empty<CalendarPanel>();
    [SerializeField] private RewardRevealView rewardReveal;
    [Tooltip("Icon tiền/đồ cho ô thưởng; dùng chung asset với màn Mission.")]
    [SerializeField] private EquipmentThemeSO theme;
    [SerializeField] private string amountFormat = "{0}";
    [SerializeField] private string levelFormat = "Lv.{0}";
    [SerializeField] private Color plainFrame = new Color(0.45f, 0.48f, 0.56f, 1f);

    #endregion

    #region Dependencies And State

    private ILoginCheckIn checkIn;
    private IGameState game;
    private IWallet wallet;
    private RewardSlotFactory slots;
    private bool subscribed;
    private bool themeValid;
    private bool autoOpened; // Đã tự mở trong phiên này.
    private bool presenting; // Màn nhận quà đang diễn: chặn bấm tiếp và hoãn vẽ lại.
    private bool chaining; // Mở tự động: đóng một lịch thì chuyển sang lịch kế tiếp còn quà. Mở từ nút thì chỉ lịch đó.
    private int pending = -1; // Panel sẽ hiện khi UIManager cho mở.
    private int current = -1; // Panel đang hiện; -1 = popup đóng.

    /// <summary>Popup đang hiện.</summary>
    public bool IsOpen => current >= 0;

    /// <inheritdoc/>
    public override bool IsOverlay => true;

    private RewardSlotFactory Slots => slots ??= new RewardSlotFactory(theme, amountFormat, levelFormat, plainFrame);

    #endregion

    #region Lifecycle

    /// <summary>SceneContext inject cả object đang tắt; đăng ký event khi object được bật (UIManager bật lúc mở).</summary>
    [Inject]
    public void Construct(ILoginCheckIn loginCheckIn, IGameState gameState, IWallet walletService)
    {
        Unsubscribe();
        checkIn = loginCheckIn;
        game = gameState;
        wallet = walletService;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (subscribed || checkIn == null) return;
        subscribed = true;
        checkIn.Changed += OnCheckInChanged;
        foreach (CalendarPanel panel in panels)
        {
            if (panel.closeButton != null) panel.closeButton.onClick.AddListener(OnClose);
            foreach (LoginCheckInDayView day in panel.days) day.Clicked += OnDayClicked;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        checkIn.Changed -= OnCheckInChanged;
        foreach (CalendarPanel panel in panels)
        {
            if (panel.closeButton != null) panel.closeButton.onClick.RemoveListener(OnClose);
            foreach (LoginCheckInDayView day in panel.days) day.Clicked -= OnDayClicked;
        }
    }

    #endregion

    #region Open And Close (HomeController, UIManager)

    /// <summary>Mở đúng lịch <paramref name="calendarId"/> (nút riêng của lịch đó ở Home); lịch đang đóng thì bỏ qua.</summary>
    public void Open(string calendarId)
    {
        if (!CanShow()) return;
        for (int i = 0; i < panels.Length; i++)
        {
            if (!string.Equals(panels[i].calendarId, calendarId, StringComparison.Ordinal)) continue;
            LoginCheckInCalendar calendar = FindCalendar(calendarId);
            if (calendar == null || !calendar.IsOpen) return;
            chaining = false;
            RequestOpen(i);
            return;
        }
    }

    /// <summary>
    /// Tự mở lịch đầu tiên còn quà, một lần mỗi phiên. HomeController gọi khi Home đã hiện xong (hết chuyển cảnh).
    /// Trả về đã mở hay chưa; chưa có quà thì lần sau gọi lại vẫn mở được (ví dụ vừa qua ngày mới).
    /// </summary>
    public bool TryAutoOpen()
    {
        if (autoOpened || WantsVisible || checkIn == null || !checkIn.HasClaimable || !CanShow()) return false;
        int index = FindPanel(-1, true);
        if (index < 0) return false; // Lịch còn quà không có panel: không có gì để tự mở.
        autoOpened = true;
        chaining = true;
        RequestOpen(index);
        return true;
    }

    /// <inheritdoc/>
    // UIManager gọi: hiện panel đã chọn, hoặc ẩn hết. Object chứa popup do UIManager bật trước khi gọi hàm này.
    public override void SetVisible(bool visible)
    {
        if (!visible)
        {
            CloseImmediate();
            return;
        }
        int index = pending >= 0 ? pending : FindPanel(-1, false);
        pending = -1;
        if (index >= 0) Show(index);
        else RequestVisibility(false); // Không còn lịch nào mở để hiện: trả lại ngay, để lần mở sau không bị coi là đang mở.
    }

    // Đang hiện thì đổi panel ngay; chưa hiện thì xin UIManager mở (UIManager quyết định có cho đè lên màn hiện tại không).
    private void RequestOpen(int index)
    {
        if (IsOpen)
        {
            Show(index);
            return;
        }
        pending = index;
        RequestVisibility(true);
    }

    // Mở tự động: còn lịch khác có quà thì chuyển sang nó, hết thì đóng popup. Mở từ nút: đóng luôn.
    private void OnClose()
    {
        if (presenting || !IsOpen) return;
        int next = chaining ? FindPanel(current, true) : -1;
        if (next >= 0)
        {
            Show(next);
            return;
        }
        StaggeredPopupTween tween = panels[current].popupTween;
        if (tween != null) tween.PlayClose(() => RequestVisibility(false));
        else RequestVisibility(false);
    }

    private bool CanShow() => game != null && game.State == GameState.Home && EnsureThemeValid();

    private void Show(int index)
    {
        HideAll();
        current = index;
        CalendarPanel panel = panels[index];
        panel.root.SetActive(true);
        Draw(panel);
        if (panel.popupTween != null) panel.popupTween.PlayOpen();
    }

    private void CloseImmediate()
    {
        if (presenting && rewardReveal != null) rewardReveal.Hide();
        presenting = false;
        HideAll();
        current = -1;
    }

    private void HideAll()
    {
        foreach (CalendarPanel panel in panels)
        {
            if (panel.root == null) continue;
            if (panel.popupTween != null) panel.popupTween.StopAndReset();
            panel.root.SetActive(false);
        }
    }

    #endregion

    #region Claim

    private void OnDayClicked(LoginCheckInDayView view)
    {
        if (presenting || !IsOpen) return;
        CalendarPanel panel = panels[current];
        LoginCheckInCalendar calendar = FindCalendar(panel.calendarId);
        int index = Array.IndexOf(panel.days, view);
        if (calendar == null || index < 0 || calendar.GetDayState(index) != LoginCheckInDayState.Claimable) return;

        presenting = true; // Đặt trước khi nhận: Changed phát ngay trong TryClaim và không được vẽ lại lúc này.
        RewardResult result = null;
        bool saved = false, claimed = false;
        try { claimed = checkIn.TryClaim(calendar, out result, out saved); }
        finally
        {
            if (!claimed) presenting = false;
        }
        if (!claimed)
        {
            Draw(panel);
            return;
        }
        if (!saved) Debug.LogWarning("Login reward was granted but not saved yet; it will be retried.", this);
        // Vẽ lại ngay khi đã nhận: ô vừa nhận thành "đã nhận" và ngừng nhấp nháy trước khi nền tối + quà hiện lên.
        Draw(panel);
        rewardReveal.Show(Slots.Result(result), BalanceBefore(result, CurrencyType.Gold), BalanceBefore(result, CurrencyType.Diamond),
            OnRevealClosed);
    }

    private void OnRevealClosed()
    {
        presenting = false;
        if (IsOpen) Draw(panels[current]);
    }

    // Thanh tiền của màn nhận quà đếm từ số dư trước khi cộng.
    private long BalanceBefore(RewardResult result, CurrencyType type)
    {
        long balance = wallet.GetBalance(type);
        foreach (CurrencyCost currency in result.Currencies)
            if (currency.Currency == type) balance -= currency.Amount;
        return balance;
    }

    #endregion

    #region Draw

    // Sang ngày mới hoặc lịch đóng (mất giờ server) trong lúc popup mở.
    private void OnCheckInChanged()
    {
        if (!IsOpen || presenting) return;
        LoginCheckInCalendar calendar = FindCalendar(panels[current].calendarId);
        if (calendar == null || !calendar.IsOpen) RequestVisibility(false);
        else Draw(panels[current]);
    }

    private void Draw(CalendarPanel panel)
    {
        LoginCheckInCalendar calendar = FindCalendar(panel.calendarId);
        if (calendar == null) return;
        IReadOnlyList<LoginCheckInDay> data = calendar.Data.Days;
        if (panel.title != null) panel.title.text = calendar.Data.DisplayName;
        for (int i = 0; i < panel.days.Length; i++)
        {
            LoginCheckInDayView view = panel.days[i];
            bool used = i < data.Count;
            if (view.gameObject.activeSelf != used) view.gameObject.SetActive(used);
            if (!used) continue;
            string dayText = string.Format(CultureInfo.InvariantCulture, panel.dayFormat, i + 1);
            view.Draw(new LoginCheckInDayModel(dayText, Slots.Preview(data[i].Rewards)[0], calendar.GetDayState(i)));
        }
    }

    #endregion

    #region Lookup

    // Panel đầu tiên sau after có lịch đang mở (và còn quà nếu claimableOnly).
    private int FindPanel(int after, bool claimableOnly)
    {
        for (int i = after + 1; i < panels.Length; i++)
        {
            LoginCheckInCalendar calendar = FindCalendar(panels[i].calendarId);
            if (calendar != null && calendar.IsOpen && (!claimableOnly || calendar.CanClaim)) return i;
        }
        return -1;
    }

    private LoginCheckInCalendar FindCalendar(string calendarId)
    {
        if (checkIn == null) return null;
        foreach (LoginCheckInCalendar calendar in checkIn.Calendars)
            if (string.Equals(calendar.Data.Id, calendarId, StringComparison.Ordinal)) return calendar;
        return null;
    }

    private bool EnsureThemeValid()
    {
        if (themeValid) return true;
        try
        {
            if (theme == null) throw new InvalidOperationException("Assign Equipment Theme on LoginCheckInPopup.");
            if (rewardReveal == null) throw new InvalidOperationException("Assign Reward Reveal on LoginCheckInPopup.");
            theme.Validate();
            themeValid = true;
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
        return themeValid;
    }

    #endregion
}
