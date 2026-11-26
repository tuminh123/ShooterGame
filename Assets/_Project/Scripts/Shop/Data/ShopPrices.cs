using System;
using System.Collections.Generic;
using UnityEngine;

// Họ strategy "giá của gói", chọn thẳng trong data bằng [SerializeReference] (giống IReward). Giá chỉ MÔ TẢ:
// tự ghi phần của mình vào PurchaseRequest; việc kiểm tra và trả tiền do ShopManager làm qua IPurchaseCheck.
// Thêm cách trả mới: một class ở đây (+ PaymentKind mới và một case trong ShopManager nếu cần bước kiểm tra riêng).

#region Request

/// <summary>Giá của một gói sau khi mô tả: cách trả, các khoản tiền trong game và sản phẩm store. UI cũng đọc để vẽ giá.</summary>
public sealed class PurchaseRequest
{
    private readonly List<CurrencyCost> costs = new List<CurrencyCost>();

    /// <summary>Cách trả tiền; quyết định bước kiểm tra điều kiện.</summary>
    public PaymentKind Kind { get; private set; } = PaymentKind.Currency;

    /// <summary>Các khoản tiền trong game phải trả; rỗng = miễn phí (với <see cref="PaymentKind.Currency"/>).</summary>
    public IReadOnlyList<CurrencyCost> Costs => costs;

    /// <summary>ID sản phẩm trên store; chỉ có với <see cref="PaymentKind.Store"/>.</summary>
    public string ProductId { get; private set; }

    /// <summary>Chữ giá hiện trên nút với gói store (ví dụ "100.000 VND"); sau này lấy giá theo vùng từ store.</summary>
    public string StoreLabel { get; private set; }

    /// <summary>Không tốn gì.</summary>
    public bool IsFree => Kind == PaymentKind.Currency && costs.Count == 0;

    /// <summary>Thêm một khoản tiền trong game.</summary>
    public void AddCost(CurrencyCost cost) => costs.Add(cost);

    /// <summary>Đổi sang trả bằng store.</summary>
    public void SetStoreProduct(string productId, string label)
    {
        Kind = PaymentKind.Store;
        ProductId = productId;
        StoreLabel = label;
    }
}

/// <summary>Giá của một gói, cấu hình trong ShopPackSO.</summary>
public interface IShopPrice
{
    /// <summary>Kiểm tra tham số; ném lỗi nếu cấu hình sai.</summary>
    void Validate();

    /// <summary>Ghi giá này vào yêu cầu mua.</summary>
    void AddTo(PurchaseRequest request);
}

#endregion

#region Prices

/// <summary>Miễn phí (thường kèm giới hạn Daily, ví dụ ô FREE của Daily Deals).</summary>
[Serializable]
public sealed class FreePrice : IShopPrice
{
    /// <inheritdoc/>
    public void Validate() { }

    /// <inheritdoc/>
    public void AddTo(PurchaseRequest request) { }
}

/// <summary>Trả bằng tài nguyên trong game (vàng, kim cương).</summary>
[Serializable]
public sealed class CurrencyPrice : IShopPrice
{
    [SerializeField] private CurrencyType currency = CurrencyType.Diamond;
    [SerializeField, Min(1)] private int amount = 10;

    public CurrencyPrice() { }

    public CurrencyPrice(CurrencyType currency, int amount)
    {
        this.currency = currency;
        this.amount = amount;
    }

    /// <inheritdoc/>
    public void Validate()
    {
        if (currency != CurrencyType.Gold && currency != CurrencyType.Diamond)
            throw new InvalidOperationException("Currency price must use Gold or Diamond.");
        if (amount < 1) throw new InvalidOperationException("Currency price amount must be at least 1.");
    }

    /// <inheritdoc/>
    public void AddTo(PurchaseRequest request) => request.AddCost(new CurrencyCost(currency, amount));
}

/// <summary>Trả bằng tiền thật qua store (IAP). Hiện store được giả lập: mua là thành công.</summary>
[Serializable]
public sealed class StorePrice : IShopPrice
{
    [Tooltip("ID sản phẩm khai báo trên Google Play / App Store.")]
    [SerializeField] private string productId;
    [Tooltip("Chữ giá hiện trên nút, ví dụ \"100.000 VND\". Không dùng ký tự \"đ\": font UI (Layer Lab) không có ký tự này.")]
    [SerializeField] private string label;

    public StorePrice() { }

    public StorePrice(string productId, string label)
    {
        this.productId = productId;
        this.label = label;
    }

    /// <inheritdoc/>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(productId)) throw new InvalidOperationException("Store price needs a Product Id.");
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("Store price needs a price label.");
    }

    /// <inheritdoc/>
    public void AddTo(PurchaseRequest request) => request.SetStoreProduct(productId, label);
}

#endregion
