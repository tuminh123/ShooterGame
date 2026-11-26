using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Dữ liệu vẽ một thẻ gói: tên, icon, số lượng, giá và lý do không mua được.</summary>
public readonly struct ShopPackModel
{
    public readonly string Name;
    public readonly Sprite Icon;
    public readonly string Amount;
    public readonly bool Free;
    public readonly Sprite CurrencyIcon; // null = giá tiền thật (chỉ có chữ).
    public readonly string Price;
    public readonly PurchaseBlock Block;

    public ShopPackModel(string name, Sprite icon, string amount, bool free, Sprite currencyIcon, string price, PurchaseBlock block)
    {
        Name = name;
        Icon = icon;
        Amount = amount;
        Free = free;
        CurrencyIcon = currencyIcon;
        Price = price;
        Block = block;
    }
}

/// <summary>
/// Một thẻ gói trong shop (thẻ nhỏ hoặc banner); chỉ hiển thị. Ghép với gói qua <see cref="PackId"/>.
/// Field nào để trống thì giữ nguyên hình của bản dựng (ví dụ icon túi vàng/kim cương được vẽ sẵn).
/// </summary>
public sealed class ShopPackView : MonoBehaviour
{
    #region References

    [Tooltip("ID của ShopPackSO hiện ở thẻ này.")]
    [SerializeField] private string packId;
    [SerializeField] private Button button;
    [Tooltip("Icon phần thưởng; để trống thì giữ hình vẽ sẵn của thẻ.")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Số lượng phần thưởng; để trống nếu thẻ không hiện.")]
    [SerializeField] private TMP_Text amountText;
    [Tooltip("Chữ FREE của thẻ miễn phí; để trống nếu thẻ không có.")]
    [SerializeField] private TMP_Text freeText;
    [Tooltip("Nhóm giá (icon tiền + số); để trống nếu thẻ chỉ có chữ giá (banner).")]
    [SerializeField] private GameObject priceGroup;
    [SerializeField] private Image priceIcon;
    [SerializeField] private TMP_Text priceText;
    [Tooltip("Làm mờ cả thẻ khi hết lượt.")]
    [SerializeField] private CanvasGroup group;

    [Header("Trình bày")]
    [SerializeField] private string freeLabel = "FREE";
    [SerializeField] private string soldOutLabel = "Sold out";
    [SerializeField] private Color notEnoughColor = new Color32(0xFF, 0x5A, 0x5A, 0xFF);
    [SerializeField, Range(0f, 1f)] private float soldOutAlpha = 0.55f;

    private Color priceColor = Color.white; // Màu gốc của chữ giá, chụp lần vẽ đầu.
    private bool priceColorCaptured;

    /// <summary>ID của gói hiện ở thẻ.</summary>
    public string PackId => packId;

    #endregion

    #region Events

    /// <summary>Người chơi bấm vào thẻ.</summary>
    public event Action<ShopPackView> Clicked;

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick() => Clicked?.Invoke(this);

    #endregion

    #region Draw

    /// <summary>Vẽ thẻ theo dữ liệu. Hết lượt (hoặc gói đóng) thì mờ đi và ghi "Sold out"; thiếu tiền thì giá màu đỏ.</summary>
    public void Draw(in ShopPackModel model)
    {
        bool soldOut = model.Block == PurchaseBlock.LimitReached || model.Block == PurchaseBlock.Closed;
        if (nameText != null) nameText.text = model.Name;
        if (amountText != null) amountText.text = model.Amount;
        if (icon != null && model.Icon != null)
        {
            icon.sprite = model.Icon;
            icon.preserveAspect = true;
        }
        if (group != null) group.alpha = soldOut ? soldOutAlpha : 1f;
        if (button != null) button.interactable = !soldOut; // Thiếu tiền vẫn bấm được để báo người chơi.

        if (freeText != null) freeText.gameObject.SetActive(model.Free || (soldOut && priceGroup == null));
        if (freeText != null && freeText.gameObject.activeSelf) freeText.text = soldOut ? soldOutLabel : freeLabel;
        if (priceGroup != null) priceGroup.SetActive(!model.Free || (soldOut && freeText == null));
        DrawPrice(model, soldOut);
    }

    private void DrawPrice(in ShopPackModel model, bool soldOut)
    {
        if (priceText == null) return;
        if (!priceColorCaptured)
        {
            priceColor = priceText.color;
            priceColorCaptured = true;
        }
        bool showIcon = !soldOut && model.CurrencyIcon != null;
        if (priceIcon != null)
        {
            priceIcon.gameObject.SetActive(showIcon);
            if (showIcon) priceIcon.sprite = model.CurrencyIcon;
        }
        priceText.text = soldOut ? soldOutLabel : model.Price;
        priceText.color = model.Block == PurchaseBlock.NotEnoughCurrency ? notEnoughColor : priceColor;
    }

    #endregion
}
