using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Màn Shop, một tab menu như Heroes/Gear: mỗi tab nội dung một panel riêng (Offers, Daily, Resources), thẻ gói, popup xác nhận
/// và màn nhận quà. Chỉ trình bày; điều kiện mua, trả tiền và trao quà nằm ở <see cref="IShop"/>.
/// </summary>
/// <remarks>
/// Thanh tiền (trên) và thanh điều hướng (dưới) của Home vẫn hiện; về Home bằng nút Battle. Đổi tab thì panel trượt bằng SlideTransition.
/// Gói miễn phí nhận ngay; gói trả tiền (tài nguyên hoặc tiền thật) hỏi xác nhận trước. Không mua được thì báo bằng toast.
/// </remarks>
public sealed class ShopScreenController : UIPanelController
{
    #region References

    /// <summary>
    /// Một tab: nút, phần sáng khi đang chọn, panel nội dung và các thẻ trong panel.
    /// Chấm than do NotificationBadgeView (khóa Shop + ID các gói của tab) trên nút lo.
    /// </summary>
    [Serializable]
    private sealed class Tab
    {
        public Button button;
        [Tooltip("Phần sáng của tab đang chọn (Light).")]
        public GameObject focus;
        [Tooltip("Panel nội dung của tab; có SlideTransition để trượt khi đổi tab.")]
        public SlideTransition panel;
        public ShopPackView[] packs = Array.Empty<ShopPackView>();
        [NonSerialized] public UnityEngine.Events.UnityAction listener;
    }

    [SerializeField] private Tab[] tabs = Array.Empty<Tab>();
    [Tooltip("Màu nền (Image của nút) của tab đang chọn.")]
    [SerializeField] private Color selectedTabColor = new Color32(0x31, 0x81, 0xFF, 0xFF);
    [Tooltip("Màu nền của tab không chọn.")]
    [SerializeField] private Color normalTabColor = new Color32(0x36, 0x36, 0x4E, 0xFF);
    [Tooltip("Đồng hồ reset của các gói Daily.")]
    [SerializeField] private TMP_Text dailyTimer;
    [SerializeField] private ShopConfirmPopup confirmPopup;
    [SerializeField] private RewardRevealView rewardReveal;
    [SerializeField] private NoticeToastView notice;
    [Tooltip("Icon tiền/đồ cho ô thưởng và giá; dùng chung asset với màn Mission.")]
    [SerializeField] private EquipmentThemeSO theme;

    [Header("Chữ")]
    [SerializeField] private string amountFormat = "x{0}";
    [SerializeField] private string levelFormat = "Lv.{0}";
    [SerializeField] private string confirmFormat = "Buy {0} for {1}?";
    [SerializeField] private string notEnoughFormat = "Not enough {0}";
    [SerializeField] private string soldOutNotice = "Sold out";
    [SerializeField] private string unavailableNotice = "Not available right now";
    [SerializeField] private string timerFormat = "{0:00}h {1:00}m {2:00}s";
    [SerializeField] private Color plainFrame = new Color(0.45f, 0.48f, 0.56f, 1f);

    #endregion

    #region Dependencies And State

    private IShop shop;
    private IWallet wallet;
    private IGameState game;
    private IGameClock clock;
    private RewardSlotFactory slots;
    private bool subscribed;
    private bool themeValid;
    private bool presenting; // Đang mua hoặc đang hiện quà: chặn bấm thẻ khác.
    private int currentTab;
    private float nextCheck;

    /// <inheritdoc/>
    // Tab menu như Heroes/Gear: mở từ Heroes thì thay thế màn đó, hai thanh của Home vẫn hiện.
    public override bool IsMenuTab => true;

    private RewardSlotFactory Slots => slots ??= new RewardSlotFactory(theme, amountFormat, levelFormat, plainFrame);

    /// <summary>Màn đang mở theo yêu cầu điều hướng.</summary>
    public bool IsShown => WantsVisible;

    /// <summary>Phát khi màn mở/đóng (HomeNavigationBar cập nhật nút đang chọn).</summary>
    public event Action NavigationChanged;

    #endregion

    #region Lifecycle

    /// <summary>Zenject inject cả object đang tắt; đăng ký event khi object được bật (UIManager bật lúc mở).</summary>
    [Inject]
    public void Construct(IShop shopService, IWallet walletService, IGameState gameState, IGameClock gameClock)
    {
        Unsubscribe();
        shop = shopService;
        wallet = walletService;
        game = gameState;
        clock = gameClock;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (subscribed || shop == null) return;
        subscribed = true;
        shop.Changed += Redraw;
        wallet.BalanceChanged += OnBalanceChanged;
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].listener ??= () => SelectTab(index, true);
            tabs[i].button.onClick.AddListener(tabs[i].listener);
            foreach (ShopPackView pack in tabs[i].packs) pack.Clicked += OnPackClicked;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        shop.Changed -= Redraw;
        wallet.BalanceChanged -= OnBalanceChanged;
        foreach (Tab tab in tabs)
        {
            if (tab.listener != null) tab.button.onClick.RemoveListener(tab.listener);
            foreach (ShopPackView pack in tab.packs) pack.Clicked -= OnPackClicked;
        }
    }

    // Home pause (timeScale = 0) nên dùng unscaled time; mỗi giây kiểm tra sang ngày mới và chạy đồng hồ Daily.
    private void Update()
    {
        if (!WantsVisible || shop == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        shop.Refresh(); // Sang kỳ mới thì Changed sẽ vẽ lại.
        DrawTimer();
    }

    #endregion

    #region Open And Visibility

    /// <summary>Mở màn Shop ở tab Offers. Chỉ mở được khi game đang ở Home.</summary>
    public void Open()
    {
        if (game == null || game.State != GameState.Home || !EnsureThemeValid()) return;
        currentTab = 0;
        RequestVisibility(true);
    }

    /// <summary>Đóng màn về Home (nút Battle của thanh điều hướng).</summary>
    public void Close() => RequestVisibility(false);

    /// <inheritdoc/>
    public override void SetVisible(bool visible)
    {
        NavigationChanged?.Invoke();
        // Popup xác nhận là overlay: UIManager tự đóng nó khi màn Shop không còn là màn hiện.
        if (!visible)
        {
            if (presenting && rewardReveal != null) rewardReveal.Hide();
            presenting = false;
        }
        ApplyScreenVisibility(visible);
        if (!visible) return;
        shop.Refresh();
        SelectTab(currentTab, false); // Mở màn: cả màn đã trượt vào, panel không trượt thêm.
        Redraw();
    }

    // Mỗi tab một panel, trượt bằng SlideTransition có sẵn (như tab Level/Stars của màn Character):
    // bấm tab bên phải thì panel cũ ra bên trái, panel mới vào từ bên phải; ngược lại với tab bên trái.
    private void SelectTab(int index, bool animate)
    {
        if (index < 0 || index >= tabs.Length) return;
        int previous = currentTab;
        currentTab = index;
        bool slide = animate && previous != index;
        SlideDirection exitTo = index > previous ? SlideDirection.Left : SlideDirection.Right;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].focus != null) tabs[i].focus.SetActive(i == index);
            // Tab đang chọn nền xanh, các tab khác nền xám (scene lưu màu cố định nên phải tô lại mỗi lần đổi).
            if (tabs[i].button.targetGraphic != null)
                tabs[i].button.targetGraphic.color = i == index ? selectedTabColor : normalTabColor;
            SlideTransition panel = tabs[i].panel;
            if (panel == null) continue;
            if (i == index && slide) panel.PlayIn(SlideTransition.Opposite(exitTo));
            else if (i == previous && slide) panel.PlayOut(exitTo);
            else panel.SetVisibleInstant(i == index);
        }
        DrawTimer();
    }

    private bool EnsureThemeValid()
    {
        if (themeValid) return true;
        try
        {
            if (theme == null) throw new InvalidOperationException("Assign Equipment Theme on ShopScreenController.");
            if (confirmPopup == null || rewardReveal == null) throw new InvalidOperationException("Assign Confirm Popup and Reward Reveal on ShopScreenController.");
            theme.Validate();
            themeValid = true;
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
        return themeValid;
    }

    #endregion

    #region Purchase

    private void OnPackClicked(ShopPackView view)
    {
        if (presenting || shop == null) return;
        ShopPackRuntime pack = shop.Find(view.PackId);
        if (pack == null) return;
        PurchaseBlock block = shop.GetBlock(pack);
        if (block != PurchaseBlock.None)
        {
            ShowBlocked(pack, block);
            return;
        }
        if (pack.Request.IsFree)
        {
            BuyAsync(pack).Forget();
            return;
        }
        RewardSlotModel reward = FirstReward(pack);
        string message = string.Format(CultureInfo.InvariantCulture, confirmFormat, pack.Data.DisplayName, PriceText(pack.Request));
        confirmPopup.Show(pack.Data.DisplayName, pack.Data.Icon != null ? pack.Data.Icon : reward.Icon, reward.Label, message,
            () => BuyAsync(pack).Forget());
    }

    private async UniTaskVoid BuyAsync(ShopPackRuntime pack)
    {
        presenting = true;
        ShopPurchaseResult result;
        try { result = await shop.BuyAsync(pack); }
        catch (Exception exception)
        {
            presenting = false;
            Debug.LogException(exception, this);
            return;
        }
        if (!result.Succeeded)
        {
            presenting = false;
            ShowBlocked(pack, result.Block);
            Redraw();
            return;
        }
        if (!result.Saved) Debug.LogWarning("Shop purchase was granted but not saved yet; it will be retried.", this);
        if (!WantsVisible)
        {
            presenting = false;
            return;
        }
        rewardReveal.Show(Slots.Result(result.Rewards), BalanceBefore(result.Rewards, CurrencyType.Gold),
            BalanceBefore(result.Rewards, CurrencyType.Diamond), OnRevealClosed);
    }

    private void OnRevealClosed()
    {
        presenting = false;
        Redraw();
    }

    private void ShowBlocked(ShopPackRuntime pack, PurchaseBlock block)
    {
        if (notice == null) return;
        switch (block)
        {
            case PurchaseBlock.NotEnoughCurrency:
                CurrencyType currency = pack.Request.Costs.Count > 0 ? pack.Request.Costs[0].Currency : CurrencyType.Gold;
                notice.Show(string.Format(CultureInfo.InvariantCulture, notEnoughFormat, theme.StatTheme.GetCurrencyName(currency)));
                break;
            case PurchaseBlock.LimitReached:
                notice.Show(soldOutNotice);
                break;
            case PurchaseBlock.Busy:
                break;
            default:
                notice.Show(unavailableNotice);
                break;
        }
    }

    // Thanh tiền của màn nhận quà đếm từ số dư trước khi cộng (giá đã trừ nằm sẵn trong số dư hiện tại).
    private long BalanceBefore(RewardResult result, CurrencyType type)
    {
        long balance = wallet.GetBalance(type);
        foreach (CurrencyCost currency in result.Currencies)
            if (currency.Currency == type) balance -= currency.Amount;
        return balance;
    }

    #endregion

    #region Draw

    private void OnBalanceChanged(CurrencyType type, long balance)
    {
        if (WantsVisible && !presenting) Redraw();
    }

    private void Redraw()
    {
        if (!WantsVisible || shop == null || !EnsureThemeValid()) return;
        foreach (Tab tab in tabs)
            foreach (ShopPackView view in tab.packs)
            {
                ShopPackRuntime pack = shop.Find(view.PackId);
                if (view.gameObject.activeSelf != (pack != null)) view.gameObject.SetActive(pack != null);
                if (pack == null) continue;
                view.Draw(CreateModel(pack, shop.GetBlock(pack)));
            }
        DrawTimer();
    }

    private ShopPackModel CreateModel(ShopPackRuntime pack, PurchaseBlock block)
    {
        PurchaseRequest request = pack.Request;
        RewardSlotModel reward = FirstReward(pack);
        Sprite currencyIcon = null;
        if (request.Kind == PaymentKind.Currency && request.Costs.Count > 0)
            currencyIcon = theme.StatTheme.GetCurrencyIcon(request.Costs[0].Currency);
        return new ShopPackModel(pack.Data.DisplayName, pack.Data.Icon != null ? pack.Data.Icon : reward.Icon, reward.Label,
            request.IsFree, currencyIcon, PriceText(request), block);
    }

    private RewardSlotModel FirstReward(ShopPackRuntime pack) => Slots.Preview(pack.Data.Rewards)[0];

    // Giá tiền thật hiện nhãn của store; giá tài nguyên hiện số (icon loại tiền nằm cạnh).
    private static string PriceText(PurchaseRequest request)
    {
        if (request.Kind == PaymentKind.Store) return request.StoreLabel;
        if (request.Costs.Count == 0) return string.Empty;
        return request.Costs[0].Amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    // Đồng hồ Daily: thời gian tới lần reset sớm nhất của các gói Daily đang hiện.
    private void DrawTimer()
    {
        if (dailyTimer == null || shop == null) return;
        ShopPackRuntime first = null;
        foreach (ShopPackRuntime pack in shop.Packs)
            if (pack.Data.Limit == PurchaseLimit.Daily && pack.IsOpen && (first == null || pack.ResetsAt < first.ResetsAt)) first = pack;
        DateTime now = default;
        bool show = first != null && clock.TryGetUtcNow(first.Data.Clock, out now);
        if (dailyTimer.gameObject.activeSelf != show) dailyTimer.gameObject.SetActive(show);
        if (!show) return;
        TimeSpan left = first.ResetsAt - now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        dailyTimer.text = string.Format(CultureInfo.InvariantCulture, timerFormat, (int)left.TotalHours, left.Minutes, left.Seconds);
    }

    #endregion
}
