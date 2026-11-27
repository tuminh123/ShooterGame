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
        [Tooltip("SlideTransition của màn mà tab này mở (tab Battle: màn Home). Để trống thì màn trượt theo hướng mặc định.")]
        public SlideTransition screen;

        /// <summary>
        /// Thứ tự trái → phải của nút trên thanh. Thanh dùng HorizontalLayoutGroup xếp theo thứ tự anh em, nên đọc sibling index
        /// thay cho vị trí: bản Focus vừa bật chưa được layout xếp chỗ (vị trí cũ) khi người chơi bấm nhanh.
        /// </summary>
        public int Order => normal.transform.GetSiblingIndex();

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
    [Tooltip("Nền dùng chung của Home và các màn menu; hiện suốt trạng thái Home để lúc trượt không lộ bản đồ. " +
        "Cần Canvas riêng: khi một màn menu đã trượt xong và che kín, chỉ tắt Canvas này để bớt vẽ thừa.")]
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
    private Canvas backgroundCanvas; // Canvas của nền chung; lấy lần đầu cần dùng.
    private SlideTransition missionSlide; // Màn Mission cũng che kín nền khi mở xong.

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
        missionSlide = missionScreen != null ? missionScreen.GetComponent<SlideTransition>() : null;
        // NavigationChanged phát lúc bắt đầu chuyển màn; nền chỉ được tắt khi màn menu trượt xong, nên nghe thêm Finished.
        foreach (SlideTransition slide in MenuSlides())
            if (slide != null) slide.Finished += RefreshBackground;
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
        foreach (SlideTransition slide in MenuSlides())
            if (slide != null) slide.Finished -= RefreshBackground;
    }

    // Các màn menu che kín nền chung khi mở xong (phần tử có thể null). Chỉ gọi lúc bật/tắt thanh bar nên mảng nhỏ không đáng kể.
    private SlideTransition[] MenuSlides() => new[] { heroesTab.screen, equipmentTab.screen, shopTab.screen, missionSlide };

    #endregion

    #region Navigation

    // Bản Focus không cần listener: bấm vào nút của màn đang mở thì không làm gì.
    // Về Battle thì đóng cả hai tab; còn đổi giữa hai tab chỉ cần mở tab mới, UIManager tự thay tab cũ trong một lần cập nhật.
    private void OnBattle()
    {
        PrepareSlide(battleTab);
        characterScreen.Close();
        equipmentScreen.Close();
        if (shopScreen != null) shopScreen.Close();
    }

    private void OnHeroes()
    {
        PrepareSlide(heroesTab);
        characterScreen.Open();
    }

    private void OnEquipment()
    {
        PrepareSlide(equipmentTab);
        equipmentScreen.Open();
    }

    private void OnShop()
    {
        if (shopScreen == null) return;
        PrepareSlide(shopTab);
        shopScreen.Open();
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
            case MissionDestination.Heroes: OnHeroes(); break;
            case MissionDestination.Equipment: OnEquipment(); break;
        }
    }

    // Hướng trượt theo vị trí nút: đích nằm bên trái tab đang mở thì cả hai màn dịch sang phải (màn cũ ra bên phải,
    // màn mới vào từ bên trái), và ngược lại. Mọi lần đổi tab đều đi qua thanh bar (kể cả nút Go của Mission),
    // nên chỉ chỗ này cần biết thứ tự tab; các màn không biết nhau. Đọc thứ tự nút trên scene nên đổi chỗ nút vẫn đúng.
    private void PrepareSlide(NavTab target)
    {
        NavTab current = CurrentTab();
        if (current == target || current.screen == null || target.screen == null) return;
        SlideDirection toward = target.Order < current.Order ? SlideDirection.Right : SlideDirection.Left;
        current.screen.SetDirection(toward);
        target.screen.SetDirection(toward);
    }

    // Tab đang mở suy ra từ màn đang hiện, cùng cách Refresh chọn nút nổi; không màn menu nào mở thì là Battle (Home).
    private NavTab CurrentTab()
    {
        if (characterScreen.IsShown) return heroesTab;
        if (equipmentScreen.IsShown) return equipmentTab;
        if (shopScreen != null && shopScreen.IsShown) return shopTab;
        return battleTab;
    }

    // Vào/ra Detail hoặc đổi tab trong menu: vẫn ở Home nên thanh bar trượt chứ không bật/tắt ngay.
    private void OnNavigationChanged() => Refresh(false);

    // Một chỗ quyết định mọi thứ của thanh bar từ việc UIManager cho phép hiện và màn Character/Equipment đang mở.
    private void Refresh(bool instant)
    {
        bool heroesOpen = characterScreen.IsShown;
        bool equipmentOpen = equipmentScreen.IsShown;
        bool shopOpen = shopScreen != null && shopScreen.IsShown; // Shop là tab menu: hai thanh vẫn hiện, nút Shop nổi lên.
        RefreshBackground();
        battleTab.SetFocused(!heroesOpen && !equipmentOpen && !shopOpen);
        heroesTab.SetFocused(heroesOpen);
        equipmentTab.SetFocused(equipmentOpen);
        shopTab.SetFocused(shopOpen);
        bool missionOpen = missionScreen != null && missionScreen.IsShown;
        SetBarsShown(shown && !missionOpen && !(heroesOpen && characterScreen.IsOnDetail), instant);
    }

    // Nền chung bật suốt state Home (tắt hẳn khi rời Home). Khi một màn menu đã trượt xong và đứng yên, màn đó che kín nền
    // (đã kiểm bằng ảnh chụp: Heroes, Gear, Shop, Mission đều có nền đục toàn màn), nên chỉ tắt Canvas của nền để GPU khỏi tô
    // thêm 2–3 lớp ảnh toàn màn không ai thấy (luật overdraw O1). Lúc đang trượt thì nền vẫn bật để khe giữa hai màn không lộ bản đồ.
    private void RefreshBackground()
    {
        if (sharedBackground == null) return;
        sharedBackground.SetActive(shown);
        if (backgroundCanvas == null) backgroundCanvas = sharedBackground.GetComponent<Canvas>();
        if (backgroundCanvas != null) backgroundCanvas.enabled = !MenuCoversBackground();
    }

    // Đọc cả ý định mở (IsShown) chứ không chỉ trạng thái trượt: lúc bắt đầu đóng menu, NavigationChanged phát trước khi
    // menu kịp trượt ra (vẫn đứng yên), mà nền phải bật lại ngay từ lúc đó.
    private bool MenuCoversBackground() =>
        Covers(characterScreen.IsShown, heroesTab.screen)
        || Covers(equipmentScreen.IsShown, equipmentTab.screen)
        || (shopScreen != null && Covers(shopScreen.IsShown, shopTab.screen))
        || (missionScreen != null && Covers(missionScreen.IsShown, missionSlide));

    private static bool Covers(bool open, SlideTransition slide) => open && slide != null && slide.IsFullyShown;

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
