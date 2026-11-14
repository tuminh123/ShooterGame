using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

// Home tạm thời: chỉ nút Play hoạt động; giữ bố cục có sẵn cho các tính năng nối sau.
public sealed class HomeController : UIPanelController
{
    #region References And Lifecycle

    [SerializeField] private Button playButton;
    [Tooltip("Hiệu ứng nhún của nút Play; chạy xong mới bắt đầu trận, dùng unscaled time khi Home đang pause.")]
    [SerializeField] private SinglePanelPopupTween playTween;
    [Tooltip("Số vàng trong ví người chơi; đọc CurrencyService, không dùng thống kê riêng của lượt chơi.")]
    [SerializeField] private TMP_Text goldText;
    private GameManager game;
    private CurrencyService currencies;
    private ScreenTransitionController transition;
    private bool starting;

    [Inject]
    public void Construct(GameManager game, ScreenTransitionController transition, CurrencyService currencies)
    {
        UnsubscribeWallet();
        this.game = game;
        this.transition = transition;
        this.currencies = currencies;
        if (isActiveAndEnabled) SubscribeWallet();
    }

    private void OnEnable()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        SubscribeWallet();
    }

    private void OnDisable()
    {
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        UnsubscribeWallet();
        starting = false;
        if (playTween != null) playTween.StopAndReset();
    }

    #endregion

    #region Wallet Presentation

    // Đọc lại số dư khi mở Home để nhận cả vàng kiếm được lúc HUD Main đang ẩn.
    private void SubscribeWallet()
    {
        if (currencies == null) return;
        currencies.BalanceChanged -= OnBalanceChanged;
        currencies.BalanceChanged += OnBalanceChanged;
        RefreshGold();
    }

    private void UnsubscribeWallet()
    {
        if (currencies != null) currencies.BalanceChanged -= OnBalanceChanged;
    }

    private void RefreshGold()
    {
        // Scene inject trước CurrencyService.Initialize; chỉ đọc ví sau khi tới Home.
        if (currencies != null && game != null && game.State == GameState.Home)
            OnBalanceChanged(CurrencyType.Gold, currencies.GetBalance(CurrencyType.Gold));
    }

    private void OnBalanceChanged(CurrencyType type, long balance)
    {
        if (type == CurrencyType.Gold && goldText != null)
            goldText.text = balance.ToString("N0", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Home Actions

    private void Play()
    {
        if (starting || game == null || game.State != GameState.Home || transition == null || transition.IsTransitioning) return;
        starting = true; // Chặn click lặp trong lúc nút đang nhún.
        if (playTween != null) playTween.PlayOpen(CompletePlay);
        else CompletePlay();
    }

    // Callback bị hủy khi Home đóng; không để tween cũ tự khởi động một lượt khác.
    private void CompletePlay()
    {
        if (!starting) return;
        starting = false;
        if (isActiveAndEnabled && game != null && game.State == GameState.Home) transition.StartRun();
    }

    public override void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible) return;
        RefreshGold();
        foreach (Selectable control in GetComponentsInChildren<Selectable>(true))
            control.interactable = control == playButton;
    }

    #endregion
}
