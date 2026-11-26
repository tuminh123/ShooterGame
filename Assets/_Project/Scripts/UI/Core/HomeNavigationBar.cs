using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hai thanh dùng chung của các màn chính (Home, Heroes, Equipment, sau này Shop, Map...): thanh dưới (nút Battle về Home,
/// nút Heroes mở màn Character, nút Bag mở màn Equipment) và thanh trên (tiền tệ, nút menu). Nút của màn đang mở "nổi lên" bằng cách bật bản Focus.
/// </summary>
/// <remarks>
/// Là panel nền (chrome) của UIManager: UIManager bật/tắt cả khối theo state game (chỉ hiện ở Home), còn việc trượt hai thanh
/// và tab focus do class này lo. Cả hai thanh nằm ngoài MainUI và CharacterScreen vì UIManager tắt MainUI khi mở màn Character,
/// mà hai thanh phải đứng yên khi đổi giữa các màn chính. Màn con như Detail thì có header/footer riêng,
/// nên cả hai thanh cùng ẩn: thanh dưới trượt xuống đáy, thanh trên trượt lên đỉnh; quay lại thì trượt về chỗ cũ.
/// Component đặt trên object luôn bật (HomeNavigation); hai thanh được bật/tắt bằng SlideTransition.
/// </remarks>
public sealed class HomeNavigationBar : UIPanelController
{
    #region Types

    /// <summary>Một nút tab: bản thường và bản nổi (Focus); chỉ một trong hai được bật.</summary>
    [Serializable]
    private sealed class NavTab
    {
        public Button normal;
        public Button focused;

        public void SetFocused(bool value)
        {
            normal.gameObject.SetActive(!value);
            focused.gameObject.SetActive(value);
        }

        public void SetInteractable()
        {
            normal.interactable = true;
            focused.interactable = true;
        }
    }

    #endregion

    #region References

    [Tooltip("Thanh bar (BottomBar_Menu); cần SlideTransition để trượt xuống/lên.")]
    [SerializeField] private SlideTransition bar;
    [Tooltip("Thanh trên (Topbar: tiền tệ, nút menu); cần SlideTransition hướng Up để trượt lên ẩn / trượt xuống hiện.")]
    [SerializeField] private SlideTransition topBar;
    [Tooltip("Nền dùng chung của Home và màn Character; hiện suốt trạng thái Home để lúc trượt không lộ bản đồ.")]
    [SerializeField] private GameObject sharedBackground;
    [SerializeField] private NavTab battleTab;
    [SerializeField] private NavTab heroesTab;
    [SerializeField] private NavTab equipmentTab;
    [Tooltip("Các nút chưa có chức năng (Shop, Bag, Map...); bị khóa.")]
    [SerializeField] private Button[] lockedButtons = Array.Empty<Button>();
    [SerializeField] private CharacterScreenController characterScreen;
    [SerializeField] private EquipmentScreenController equipmentScreen;
    [Tooltip("Màn Mission có header riêng nên hai thanh ẩn khi nó mở; để trống thì bỏ qua.")]
    [SerializeField] private MissionScreenController missionScreen;
    [Tooltip("Tab Shop trên thanh bar (bản thường Button_01 + bản nổi Button_01_Focus); mở màn Shop, hai thanh vẫn hiện như Heroes/Gear.")]
    [SerializeField] private NavTab shopTab;
    [SerializeField] private ShopScreenController shopScreen;
    [Tooltip("Nút menu (ba gạch) trên thanh trên; mở popup Settings. Để trống thì bỏ qua.")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private SettingsPopup settingsPopup;

    #endregion

    #region State

    private bool shown; // UIManager đang cho phép hiện khối thanh bar (state Home); chưa được bật thì mọi thứ ẩn.
    private bool barShown;
    private bool barInitialized; // Lần áp dụng đầu bật/tắt ngay, không trượt.

    #endregion

    #region Panel Contract (UIManager)

    /// <inheritdoc/>
    public override bool IsChrome => true;

    /// <inheritdoc/>
    public override bool ShowsInState(GameState state) => state == GameState.Home;

    /// <inheritdoc/>
    // Đổi state (vào trận / về Home) thì bật/tắt ngay lúc màn đã tối, không trượt để khỏi hở đáy màn.
    public override void SetVisible(bool visible)
    {
        shown = visible;
        Refresh(!PlayTransition);
    }

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        battleTab.normal.onClick.AddListener(OnBattle);
        heroesTab.normal.onClick.AddListener(OnHeroes);
        equipmentTab.normal.onClick.AddListener(OnEquipment);
        shopTab.normal.onClick.AddListener(OnShop);
        // Scene lưu các nút mockup ở trạng thái khóa; trước đây HomeController mở khóa khi chúng còn nằm trong MainUI.
        // Thanh bar giờ tự mở khóa nút của nó (cả bản nổi, để không bị tô màu xám "disabled").
        battleTab.SetInteractable();
        heroesTab.SetInteractable();
        equipmentTab.SetInteractable();
        shopTab.SetInteractable();
        foreach (Button button in lockedButtons)
            if (button != null) button.interactable = false;
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
        Subscribe();
    }

    private void OnDisable()
    {
        battleTab.normal.onClick.RemoveListener(OnBattle);
        heroesTab.normal.onClick.RemoveListener(OnHeroes);
        equipmentTab.normal.onClick.RemoveListener(OnEquipment);
        shopTab.normal.onClick.RemoveListener(OnShop);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
        Unsubscribe();
    }

    private void Subscribe()
    {
        Unsubscribe(); // Tránh đăng ký hai lần khi OnEnable chạy lại.
        characterScreen.NavigationChanged += OnNavigationChanged;
        equipmentScreen.NavigationChanged += OnNavigationChanged;
        if (missionScreen != null)
        {
            missionScreen.NavigationChanged += OnNavigationChanged;
            missionScreen.GoRequested += OnMissionGo;
        }
        if (shopScreen != null) shopScreen.NavigationChanged += OnNavigationChanged;
        Refresh(false);
    }

    private void Unsubscribe()
    {
        if (characterScreen != null) characterScreen.NavigationChanged -= OnNavigationChanged;
        if (equipmentScreen != null) equipmentScreen.NavigationChanged -= OnNavigationChanged;
        if (missionScreen != null)
        {
            missionScreen.NavigationChanged -= OnNavigationChanged;
            missionScreen.GoRequested -= OnMissionGo;
        }
        if (shopScreen != null) shopScreen.NavigationChanged -= OnNavigationChanged;
    }

    #endregion

    #region Navigation

    // Bản Focus không cần listener: bấm vào nút của màn đang mở thì không làm gì.
    // Về Battle thì đóng cả hai tab; còn đổi giữa hai tab chỉ cần mở tab mới, UIManager tự thay tab cũ trong một lần cập nhật.
    private void OnBattle()
    {
        characterScreen.Close();
        equipmentScreen.Close();
        if (shopScreen != null) shopScreen.Close();
    }

    private void OnHeroes() => characterScreen.Open();

    private void OnEquipment() => equipmentScreen.Open();

    private void OnShop()
    {
        if (shopScreen != null) shopScreen.Open();
    }

    // Popup đè lên màn đang hiện (Home, Heroes, Gear, Shop); UIManager đóng nó khi đổi màn.
    private void OnSettings()
    {
        if (settingsPopup != null) settingsPopup.Open();
    }

    // Nút Go của mission: Mission đã tự đóng nên chỉ cần mở màn đích. Battle = Home, người chơi tự bấm chơi.
    private void OnMissionGo(MissionDestination destination)
    {
        switch (destination)
        {
            case MissionDestination.Heroes: characterScreen.Open(); break;
            case MissionDestination.Equipment: equipmentScreen.Open(); break;
        }
    }

    // Vào/ra Detail hoặc đổi tab trong menu: vẫn ở Home nên thanh bar trượt chứ không bật/tắt ngay.
    private void OnNavigationChanged() => Refresh(false);

    // Một chỗ quyết định mọi thứ của thanh bar từ việc UIManager cho phép hiện và màn Character/Equipment đang mở.
    private void Refresh(bool instant)
    {
        bool heroesOpen = characterScreen.IsShown;
        bool equipmentOpen = equipmentScreen.IsShown;
        bool shopOpen = shopScreen != null && shopScreen.IsShown; // Shop là tab menu: hai thanh vẫn hiện, nút Shop nổi lên.
        if (sharedBackground != null) sharedBackground.SetActive(shown);
        battleTab.SetFocused(!heroesOpen && !equipmentOpen && !shopOpen);
        heroesTab.SetFocused(heroesOpen);
        equipmentTab.SetFocused(equipmentOpen);
        shopTab.SetFocused(shopOpen);
        bool missionOpen = missionScreen != null && missionScreen.IsShown;
        SetBarsShown(shown && !missionOpen && !(heroesOpen && characterScreen.IsOnDetail), instant);
    }

    // Hai thanh luôn hiện/ẩn cùng nhau; hướng trượt do SlideTransition của từng thanh quyết định.
    private void SetBarsShown(bool show, bool instant)
    {
        if (barInitialized && show == barShown) return;
        bool snap = !barInitialized || instant;
        Apply(bar, show, snap);
        Apply(topBar, show, snap);
        barInitialized = true;
        barShown = show;
    }

    private static void Apply(SlideTransition slide, bool show, bool snap)
    {
        if (snap) slide.SetVisibleInstant(show);
        else if (show) slide.PlayIn();
        else slide.PlayOut();
    }

    #endregion
}
