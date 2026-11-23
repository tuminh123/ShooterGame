using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Nút debug của panel test: mỗi lần bấm cộng <see cref="amount"/> cho <b>mỗi</b> loại tiền trong <see cref="currencies"/>
/// vào ví vĩnh viễn (ghi chung một lần), để test giá nâng level, trang bị, ghép và nâng sao mà không cần đi trận.
/// </summary>
/// <remarks>Ví dụ: nút "+1000 GOLD" chỉ có Gold; nút "+50 SHARDS" có cả 5 loại mảnh.</remarks>
[RequireComponent(typeof(Button))]
public sealed class AddCurrencyButton : MonoBehaviour
{
    #region Settings

    [Tooltip("Các loại tiền được cộng (mỗi loại cộng cùng một lượng).")]
    [SerializeField] private List<CurrencyType> currencies = new List<CurrencyType> { CurrencyType.Gold };
    [Tooltip("Số tiền cộng cho mỗi loại, mỗi lần bấm.")]
    [SerializeField, Min(1)] private long amount = 1000;

    #endregion

    #region Dependencies And State

    private IWallet wallet;
    private Button button;

    #endregion

    #region Injection And Initialization

    /// <summary>SceneContext inject ví vĩnh viễn (miền Progression).</summary>
    [Inject]
    public void Construct(IWallet walletService) => wallet = walletService;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(AddCurrency);
    }

    private void OnDestroy() => button.onClick.RemoveListener(AddCurrency);

    #endregion

    #region Debug Action

    private void AddCurrency()
    {
        if (wallet == null || currencies.Count == 0)
            return;
        var amounts = new Dictionary<CurrencyType, long>(currencies.Count);
        foreach (CurrencyType currency in currencies)
            amounts[currency] = amount;
        bool saved = wallet.AddBatch(amounts);
        Debug.Log($"[Test] +{amount} {string.Join(", ", currencies)}.", this);
        if (!saved)
            Debug.LogWarning("[Test] Currency was added but not saved yet; SaveManager will retry.", this);
    }

    #endregion
}
