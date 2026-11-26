using System;
using Cysharp.Threading.Tasks;

// Họ strategy "kiểm tra điều kiện và trả tiền" theo cách trả của gói (PaymentKind). ShopManager tạo mỗi loại một bản
// qua switch duy nhất (ShopManager.CreateCheck). Điều kiện chung (gói mở, còn lượt) do ShopManager kiểm tra trước.

#region Contract

/// <summary>Bước kiểm tra điều kiện và trả tiền riêng của một cách trả.</summary>
internal interface IPurchaseCheck
{
    /// <summary>Mua được lúc này không (không đổi gì). None = được.</summary>
    PurchaseBlock Check(PurchaseRequest request);

    /// <summary>
    /// Trả tiền. Xong mà trả về None thì ShopManager trao quà ngay. Tài nguyên trong game KHÔNG trừ ở đây mà trừ cùng lần ghi
    /// với phần thưởng (IRewardGrant.PurchaseRewards), để không bao giờ mất tiền mà không nhận quà.
    /// </summary>
    UniTask<PurchaseBlock> PayAsync(PurchaseRequest request);
}

#endregion

#region Currency

/// <summary>Trả bằng vàng/kim cương: chỉ cần đủ tiền. Không có khoản nào = miễn phí.</summary>
internal sealed class CurrencyPurchaseCheck : IPurchaseCheck
{
    private readonly IWallet wallet;

    internal CurrencyPurchaseCheck(IWallet wallet) => this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

    /// <inheritdoc/>
    public PurchaseBlock Check(PurchaseRequest request)
        => request.Costs.Count == 0 || wallet.CanAfford(request.Costs) ? PurchaseBlock.None : PurchaseBlock.NotEnoughCurrency;

    /// <inheritdoc/>
    // Không chờ gì: tiền được trừ ngay sau đó, cùng lần ghi với phần thưởng.
    public UniTask<PurchaseBlock> PayAsync(PurchaseRequest request) => UniTask.FromResult(Check(request));
}

#endregion

#region Store

/// <summary>
/// Trả bằng tiền thật qua store (IAP). Dự án chưa có Unity IAP nên đây là bản giả lập: store luôn sẵn sàng, mua là thành công.
/// Khi cài Unity IAP chỉ sửa thân hai hàm này (khởi tạo store, gọi mua và chờ kết quả); Shop và data không đổi.
/// </summary>
internal sealed class StorePurchaseCheck : IPurchaseCheck
{
    /// <inheritdoc/>
    public PurchaseBlock Check(PurchaseRequest request)
        => string.IsNullOrWhiteSpace(request.ProductId) ? PurchaseBlock.StoreUnavailable : PurchaseBlock.None;

    /// <inheritdoc/>
    public UniTask<PurchaseBlock> PayAsync(PurchaseRequest request) => UniTask.FromResult(Check(request));
}

#endregion
