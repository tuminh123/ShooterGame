using System.Globalization;
using TMPro;
using UnityEngine;
using Zenject;

/// <summary>
/// Thanh tiền (vàng, kim cương) của prefab CurrencyBar. Tự inject <see cref="IWallet"/>, nghe BalanceChanged
/// và đọc lại số dư mỗi khi bật. Chỉ hiển thị, không cộng/trừ tiền; nút [+] chưa có chức năng.
/// </summary>
public sealed class CurrencyBarView : MonoBehaviour
{
    #region References

    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text diamondText;

    private IWallet wallet;
    private IGameState game;
    private bool subscribed;

    #endregion

    #region Lifecycle

    /// <summary>Zenject inject cả object đang tắt; nếu đã bật thì đăng ký ngay.</summary>
    [Inject]
    public void Construct(IWallet wallet, IGameState gameState)
    {
        Unsubscribe();
        this.wallet = wallet;
        this.game = gameState;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    #endregion

    #region Wallet Presentation

    private void Subscribe()
    {
        if (subscribed || wallet == null) return;
        subscribed = true;
        wallet.BalanceChanged += OnBalanceChanged;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        wallet.BalanceChanged -= OnBalanceChanged;
    }

    // Scene được inject trước ProgressionManager.Initialize; giống HomeController, chỉ đọc ví khi đã tới Home.
    private void Refresh()
    {
        if (game == null || game.State != GameState.Home) return;
        OnBalanceChanged(CurrencyType.Gold, wallet.GetBalance(CurrencyType.Gold));
        OnBalanceChanged(CurrencyType.Diamond, wallet.GetBalance(CurrencyType.Diamond));
    }

    private void OnBalanceChanged(CurrencyType type, long balance)
    {
        TMP_Text target = type == CurrencyType.Gold ? goldText : type == CurrencyType.Diamond ? diamondText : null;
        if (target != null) target.text = balance.ToString("N0", CultureInfo.InvariantCulture);
    }

    #endregion
}
