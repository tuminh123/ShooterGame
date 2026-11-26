using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Panel Gacha trong tab "Summon" của Shop: mỗi banner (tướng, trang bị) một thẻ có tỉ lệ, số lượt còn tới pity và hai nút x1 / x10.
/// Quay → màn tối mở rương (GachaChestView) → bong bóng phần thưởng hiện lần lượt, bấm Claim thì bay vào Heroes / Bag (RewardRevealView).
/// </summary>
/// <remarks>
/// Không phải panel của UIManager: ShopScreenController bật/tắt panel này như mọi tab khác. Bật thì vẽ lại, tắt (đổi tab, đóng Shop)
/// thì dừng hiệu ứng đang diễn; phần thưởng đã lưu trước khi diễn nên không mất gì. Luật quay, tỉ lệ, pity nằm ở GachaManager / GachaBannerSO.
/// </remarks>
public sealed class GachaPanelView : MonoBehaviour
{
    #region References

    /// <summary>Thẻ của một banner, ghép với asset theo ID.</summary>
    [Serializable]
    private sealed class BannerCard
    {
        [Tooltip("ID của GachaBannerSO; banner không có trong cấu hình thì thẻ bị ẩn.")]
        public string bannerId;
        public GameObject root;
        public TMP_Text title;
        public Image icon;
        public TMP_Text rates;
        public TMP_Text pity;
        public Button singleButton;
        public TMP_Text singlePrice;
        public Button tenButton;
        public TMP_Text tenPrice;
        [NonSerialized] public GachaBannerSO banner;
        [NonSerialized] public UnityEngine.Events.UnityAction single;
        [NonSerialized] public UnityEngine.Events.UnityAction ten;
    }

    [SerializeField] private BannerCard[] cards = Array.Empty<BannerCard>();
    [SerializeField] private GachaChestView chest;
    [Tooltip("Màn nhận thưởng dùng chung với Mission và các gói Shop.")]
    [SerializeField] private RewardRevealView rewardReveal;
    [SerializeField] private NoticeToastView notice;
    [Tooltip("Icon tiền/đồ cho ô thưởng; dùng chung asset với màn Mission và Shop.")]
    [SerializeField] private EquipmentThemeSO theme;

    [Header("Chữ")]
    [SerializeField] private string amountFormat = "x{0}";
    [SerializeField] private string levelFormat = "Lv.{0}";
    [SerializeField] private string pityFormat = "{0} guaranteed in {1} pulls";
    [SerializeField] private string notEnoughFormat = "Not enough {0}";
    [SerializeField] private Color plainFrame = new Color(0.45f, 0.48f, 0.56f, 1f);

    #endregion

    #region Dependencies And State

    private IGacha gacha;
    private IWallet wallet;
    private RewardSlotFactory slots;
    private bool subscribed;
    private bool presenting; // Đang mở rương hoặc hiện quà: chặn quay chồng.

    private RewardSlotFactory Slots => slots ??= new RewardSlotFactory(theme, amountFormat, levelFormat, plainFrame);

    #endregion

    #region Lifecycle

    /// <summary>Zenject inject cả object đang tắt; đăng ký event khi panel được bật (chọn tab Summon).</summary>
    [Inject]
    public void Construct(IGacha gachaService, IWallet walletService)
    {
        Unsubscribe();
        gacha = gachaService;
        wallet = walletService;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();

    // Đổi tab hoặc đóng Shop giữa lúc diễn: tắt hiệu ứng ngay (quà đã lưu).
    private void OnDisable()
    {
        Unsubscribe();
        if (!presenting) return;
        presenting = false;
        if (chest != null) chest.Hide();
        if (rewardReveal != null) rewardReveal.Hide();
    }

    private void Subscribe()
    {
        if (subscribed || gacha == null) return;
        subscribed = true;
        gacha.Changed += Redraw;
        wallet.BalanceChanged += OnBalanceChanged;
        foreach (BannerCard card in cards)
        {
            BannerCard current = card;
            card.single ??= () => OnPull(current, 1);
            card.ten ??= () => OnPull(current, 10);
            card.singleButton.onClick.AddListener(card.single);
            card.tenButton.onClick.AddListener(card.ten);
        }
        Redraw();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        gacha.Changed -= Redraw;
        wallet.BalanceChanged -= OnBalanceChanged;
        foreach (BannerCard card in cards)
        {
            if (card.single != null) card.singleButton.onClick.RemoveListener(card.single);
            if (card.ten != null) card.tenButton.onClick.RemoveListener(card.ten);
        }
    }

    #endregion

    #region Pull

    private void OnPull(BannerCard card, int count)
    {
        if (presenting || gacha == null || card.banner == null) return;
        GachaPullResult result;
        try { result = gacha.Pull(card.banner, count); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return;
        }
        if (result.Block != PurchaseBlock.None)
        {
            if (result.Block == PurchaseBlock.NotEnoughCurrency && notice != null)
                notice.Show(string.Format(CultureInfo.InvariantCulture, notEnoughFormat, theme.StatTheme.GetCurrencyName(card.banner.Currency)));
            else Debug.LogWarning("Gacha pull blocked: " + result.Block, this);
            return;
        }
        if (!result.Saved) Debug.LogWarning("Gacha pull was granted but not saved yet; it will be retried.", this);
        presenting = true;
        chest.Play(card.banner.ChestClosed, card.banner.ChestOpen, result.HighestTier, () => Reveal(result));
    }

    // Gacha chỉ ra tướng/mảnh/trang bị nên thanh tiền của màn nhận quà giữ nguyên số dư hiện tại.
    private void Reveal(GachaPullResult result)
    {
        if (!isActiveAndEnabled)
        {
            presenting = false;
            return;
        }
        rewardReveal.Show(Slots.Result(result.Rewards), wallet.GetBalance(CurrencyType.Gold), wallet.GetBalance(CurrencyType.Diamond),
            OnRevealClosed);
    }

    private void OnRevealClosed()
    {
        presenting = false;
        Redraw();
    }

    #endregion

    #region Draw

    private void OnBalanceChanged(CurrencyType type, long balance)
    {
        if (!presenting) Redraw();
    }

    private void Redraw()
    {
        if (gacha == null || !isActiveAndEnabled) return;
        foreach (BannerCard card in cards)
        {
            card.banner = Find(card.bannerId);
            if (card.root != null) card.root.SetActive(card.banner != null);
            if (card.banner == null) continue;
            GachaBannerSO banner = card.banner;
            if (card.title != null) card.title.text = banner.DisplayName;
            if (card.icon != null && banner.Icon != null) card.icon.sprite = banner.Icon;
            if (card.rates != null) card.rates.text = RatesText(banner);
            int remaining = gacha.PullsUntilPity(banner);
            if (card.pity != null)
                card.pity.text = remaining > 0 ? string.Format(CultureInfo.InvariantCulture, pityFormat, banner.PityTier, remaining) : string.Empty;
            DrawPrice(card.singlePrice, banner.GetCost(1));
            DrawPrice(card.tenPrice, banner.GetCost(10));
        }
    }

    private GachaBannerSO Find(string id)
    {
        foreach (GachaBannerSO banner in gacha.Banners)
            if (string.Equals(banner.Id, id, StringComparison.Ordinal)) return banner;
        return null;
    }

    // Tỉ lệ gốc từ cùng bảng dùng để quay, bậc cao trước (công khai tỉ lệ như các store yêu cầu).
    private static string RatesText(GachaBannerSO banner)
    {
        var text = new StringBuilder();
        var tiers = (GachaTier[])Enum.GetValues(typeof(GachaTier));
        for (int i = tiers.Length - 1; i >= 0; i--)
        {
            float chance = GachaRoller.BaseChance(banner, tiers[i]);
            if (chance <= 0f) continue;
            if (text.Length > 0) text.Append("  ");
            text.Append(tiers[i]).Append(' ').Append((chance * 100f).ToString("0.##", CultureInfo.InvariantCulture)).Append('%');
        }
        return text.ToString();
    }

    // Giá đỏ khi không đủ tiền, nhưng nút vẫn bấm được để báo thiếu gì.
    private void DrawPrice(TMP_Text label, CurrencyCost cost)
    {
        if (label == null) return;
        label.text = cost.Amount.ToString("N0", CultureInfo.InvariantCulture);
        label.color = wallet.GetBalance(cost.Currency) >= cost.Amount ? Color.white : new Color(1f, 0.45f, 0.45f);
    }

    #endregion
}
