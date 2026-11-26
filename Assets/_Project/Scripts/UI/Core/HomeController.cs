using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Home tạm thời: nút Play hoạt động, giữa màn hiện nhân vật đang chọn; giữ bố cục có sẵn cho các tính năng nối sau.
/// Thanh bar dưới (mở màn Character, về Home) do HomeNavigationBar quản lý, nằm ngoài MainUI để hiện cả ở màn Character.
/// Số tiền do CurrencyBarView trên Topbar dùng chung hiển thị (Topbar nằm ngoài MainUI, HomeNavigationBar bật/tắt).
/// </summary>
public sealed class HomeController : UIPanelController
{
    #region References

    [SerializeField] private Button playButton;
    [Tooltip("Hiệu ứng nhún của nút Play; chạy xong mới bắt đầu trận, dùng unscaled time khi Home đang pause.")]
    [SerializeField] private SinglePanelPopupTween playTween;

    [Header("Nhân vật đang chọn (giữa Home)")]
    [Tooltip("Khung đặt nhân vật UI (Middle/Character); ảnh của nó là ảnh dự phòng khi nhân vật chưa có prefab UI.")]
    [SerializeField] private Image characterFrame;
    [SerializeField] private TMP_Text characterName;
    [Tooltip("Thanh EXP nâng cấp của cấp hiện tại, cùng nghĩa với thanh ở màn Detail.")]
    [SerializeField] private Slider levelSlider;
    [SerializeField] private TMP_Text levelValueText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string maxLevelText = "MAX";

    [Header("Mission")]
    [Tooltip("Nút Quest mở màn Mission; chấm báo do NotificationBadgeView (khóa Quest) trên nút lo.")]
    [SerializeField] private Button questButton;
    [SerializeField] private MissionScreenController missionScreen;

    [Header("Login Check-in")]
    [Tooltip("Mỗi lịch đăng nhập một nút riêng ở Home (7 ngày, 30 ngày, event…).")]
    [SerializeField] private LoginEntry[] loginEntries = Array.Empty<LoginEntry>();
    [SerializeField] private LoginCheckInPopup loginPopup;
    [Tooltip("{0} = ngày, {1} = giờ.")]
    [SerializeField] private string timerDaysFormat = "{0}d {1}h";
    [Tooltip("{0} = giờ, {1} = phút.")]
    [SerializeField] private string timerHoursFormat = "{0}h {1}m";

    /// <summary>
    /// Nút ở Home của một lịch đăng nhập: bấm mở đúng lịch đó, đồng hồ khi lịch có hạn, ẩn khi lịch đóng.
    /// Chấm than do NotificationBadgeView (khóa Login + ID lịch) trên nút lo.
    /// </summary>
    [Serializable]
    private sealed class LoginEntry
    {
        [Tooltip("ID của LoginCheckInCalendarSO.")]
        public string calendarId;
        public Button button;
        [Tooltip("Thời gian còn lại của kỳ (Month/Period/Event); lịch 7 ngày không có hạn thì ẩn.")]
        public TMP_Text timer;
        [NonSerialized] public UnityEngine.Events.UnityAction listener; // Giữ để gỡ đúng listener đã gắn.
    }

    #endregion

    #region Dependencies And State

    private IGameState game;
    private IScreenTransition transition;
    private IPlayerSelection selection;
    private ICharacterUpgrades upgrades;
    private ILoginCheckIn checkIn;
    private IGameClock clock;
    private CharacterPreviewSlot preview;
    private float nextLoginCheck;
    private bool shown; // Home đang là màn hiện (không bị màn Mission/Character… che); khác WantsVisible vẫn true khi Home nằm dưới stack.
    private bool starting;
    private bool subscribed;

    #endregion

    #region Lifecycle

    /// <summary>SceneContext inject các service cần cho màn Home.</summary>
    [Inject]
    public void Construct(IGameState gameState, IScreenTransition screenTransition,
        IPlayerSelection playerSelection, ICharacterUpgrades characterUpgrades, ILoginCheckIn loginCheckIn, IGameClock gameClock)
    {
        Unsubscribe();
        game = gameState;
        transition = screenTransition;
        selection = playerSelection;
        upgrades = characterUpgrades;
        checkIn = loginCheckIn;
        clock = gameClock;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (questButton != null) questButton.onClick.AddListener(OpenMissions);
        foreach (LoginEntry entry in loginEntries)
        {
            if (entry.button == null) continue;
            string calendarId = entry.calendarId;
            entry.listener ??= () => OpenLogin(calendarId);
            entry.button.onClick.AddListener(entry.listener);
        }
        Subscribe();
    }

    private void OnDisable()
    {
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        if (questButton != null) questButton.onClick.RemoveListener(OpenMissions);
        foreach (LoginEntry entry in loginEntries)
            if (entry.button != null && entry.listener != null) entry.button.onClick.RemoveListener(entry.listener);
        Unsubscribe();
        starting = false;
        if (playTween != null) playTween.StopAndReset();
    }

    // Đổi nhân vật hoặc nâng cấp ở màn Character thì giữa Home cập nhật theo (Home có thể đang trượt về).
    private void Subscribe()
    {
        if (subscribed || selection == null) return;
        subscribed = true;
        selection.SelectionChanged += OnSelectionChanged;
        upgrades.Upgraded += OnUpgraded;
        checkIn.Changed += DrawLogin;
        DrawCharacter();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        selection.SelectionChanged -= OnSelectionChanged;
        upgrades.Upgraded -= OnUpgraded;
        checkIn.Changed -= DrawLogin;
    }

    // Home pause (timeScale = 0) nên dùng unscaled time: mỗi giây chạy đồng hồ của các lịch. Việc kiểm tra sang ngày mới
    // (chấm báo, mở/đóng lịch) do NotificationManager làm cho mọi miền; lịch đổi thì Changed → DrawLogin.
    // Popup đăng nhập tự mở ở đây (không phải trong popup) vì object popup có thể đang tắt; chờ hết chuyển cảnh để không bật lúc màn còn tối.
    private void Update()
    {
        if (!shown || checkIn == null || game.State != GameState.Home) return;
        if (loginPopup != null && !starting && !transition.IsTransitioning) loginPopup.TryAutoOpen();
        if (Time.unscaledTime < nextLoginCheck) return;
        nextLoginCheck = Time.unscaledTime + 1f;
        DrawLoginTimer();
    }

    private void OnSelectionChanged(CharacterType type) => DrawCharacter();
    private void OnUpgraded(CharacterType type, CharacterUpgradeKind kind) => DrawCharacter();

    #endregion

    #region Visibility

    // Chỉ trượt khi đổi qua lại với màn menu (UIManager gọi hai bên cùng lúc nên hai màn đẩy nhau): PlayTransition = true.
    // Vào trận / từ trận về Home thì bật/tắt ngay: ScreenTransition đã làm màn tối dần, UIManager truyền PlayTransition = false.
    public override void SetVisible(bool visible)
    {
        shown = visible; // Popup đăng nhập đè lên Home do UIManager tự đóng khi Home không còn là màn hiện.
        bool hasSlide = TryGetComponent(out SlideTransition slide);
        if (!visible)
        {
            if (hasSlide && PlayTransition) slide.PlayOut();
            else if (hasSlide) slide.SetVisibleInstant(false);
            else gameObject.SetActive(false);
            return;
        }
        if (hasSlide && PlayTransition) slide.PlayIn();
        else if (hasSlide) slide.SetVisibleInstant(true);
        else gameObject.SetActive(true);
        // Chỉ các nút đã có chức năng được bấm; phần còn lại của bố cục giữ nguyên nhưng tắt.
        foreach (Selectable control in GetComponentsInChildren<Selectable>(true))
            control.interactable = control == playButton || control == questButton || IsLoginButton(control);
        DrawCharacter();
        DrawLogin();
    }

    #endregion

    #region Selected Character

    // Scene inject trước ProgressionManager.Initialize; chỉ đọc tiến trình khi đã tới Home.
    private void DrawCharacter()
    {
        if (selection == null || game == null || game.State != GameState.Home || !isActiveAndEnabled) return;
        CharacterDataSO character = FindSelected();
        if (character == null) return;

        if (characterFrame != null)
        {
            preview ??= new CharacterPreviewSlot(characterFrame);
            preview.Show(character.UIVisualPrefab, character.Portrait);
        }
        if (characterName != null) characterName.text = character.DisplayName;

        UpgradeQuote level = upgrades.GetQuote(character.CharacterType, CharacterUpgradeKind.Level);
        bool maxed = level.ProgressRequired <= 0;
        if (levelText != null) levelText.text = level.Current.ToString(CultureInfo.InvariantCulture);
        if (levelSlider != null)
        {
            levelSlider.minValue = 0f;
            levelSlider.maxValue = maxed ? 1f : level.ProgressRequired;
            levelSlider.value = maxed ? 1f : level.Progress;
        }
        if (levelValueText != null)
            levelValueText.text = maxed
                ? maxLevelText
                : level.Progress.ToString(CultureInfo.InvariantCulture) + "/" + level.ProgressRequired.ToString(CultureInfo.InvariantCulture);
    }

    private CharacterDataSO FindSelected()
    {
        foreach (CharacterDataSO character in selection.PlayableCharacters)
            if (character.CharacterType == selection.SelectedCharacterType) return character;
        return null;
    }

    #endregion

    #region Home Actions

    private void OpenMissions()
    {
        if (!starting && missionScreen != null) missionScreen.Open();
    }

    private void OpenLogin(string calendarId)
    {
        if (!starting && loginPopup != null) loginPopup.Open(calendarId);
    }

    private bool IsLoginButton(Selectable control)
    {
        foreach (LoginEntry entry in loginEntries)
            if (entry.button == control) return true;
        return false;
    }

    // Chỉ đọc tiến độ khi đã tới Home. Lịch đang đóng (event hết hạn, chưa có giờ server) thì ẩn nút; ẩn nút thì
    // NotificationBadgeView trên nút cũng tắt chấm theo.
    private void DrawLogin()
    {
        if (checkIn == null || game == null || game.State != GameState.Home) return;
        foreach (LoginEntry entry in loginEntries)
        {
            LoginCheckInCalendar calendar = FindCalendar(entry.calendarId);
            bool open = calendar != null && calendar.IsOpen;
            if (entry.button != null && entry.button.gameObject.activeSelf != open) entry.button.gameObject.SetActive(open);
        }
        DrawLoginTimer();
    }

    // Đồng hồ trên từng nút: thời gian còn lại của kỳ; lịch không có hạn (7 ngày) thì ẩn chữ.
    private void DrawLoginTimer()
    {
        foreach (LoginEntry entry in loginEntries)
        {
            if (entry.timer == null) continue;
            string text = null;
            LoginCheckInCalendar calendar = FindCalendar(entry.calendarId);
            if (calendar != null && calendar.IsOpen && calendar.EndsAt != DateTime.MaxValue &&
                clock.TryGetUtcNow(calendar.Data.Clock, out DateTime now))
                text = FormatTimeLeft(calendar.EndsAt - now);
            bool show = text != null;
            if (entry.timer.gameObject.activeSelf != show) entry.timer.gameObject.SetActive(show);
            if (show) entry.timer.text = text;
        }
    }

    private LoginCheckInCalendar FindCalendar(string calendarId)
    {
        foreach (LoginCheckInCalendar calendar in checkIn.Calendars)
            if (string.Equals(calendar.Data.Id, calendarId, StringComparison.Ordinal)) return calendar;
        return null;
    }

    private string FormatTimeLeft(TimeSpan left)
    {
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        return left.Days > 0
            ? string.Format(CultureInfo.InvariantCulture, timerDaysFormat, left.Days, left.Hours)
            : string.Format(CultureInfo.InvariantCulture, timerHoursFormat, left.Hours, left.Minutes);
    }

    private void Play()
    {
        if (starting || game == null || game.State != GameState.Home || transition == null || transition.IsTransitioning) return;
        starting = true; // Chặn click lặp trong lúc nút đang nhún.
        if (playTween != null) playTween.PlayOpen(CompletePlay);
        else CompletePlay();
    }

    // Callback bị hủy khi Home đóng; không để tween cũ tự khởi động một lượt khác.
    // Thanh bar không trượt lúc vào trận: nó tắt ngay cùng Home khi màn đã tối (xem HomeNavigationBar.Refresh).
    private void CompletePlay()
    {
        if (!starting) return;
        starting = false;
        if (isActiveAndEnabled && game != null && game.State == GameState.Home) transition.StartBattle();
    }

    #endregion
}
