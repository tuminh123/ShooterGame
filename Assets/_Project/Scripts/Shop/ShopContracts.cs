using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// Ranh giới của miền Shop, do ShopManager implement. Chỉ UI dùng (màn Shop). ShopPackRuntime ra ngoài chỉ đọc.

#region Shop (màn Shop)

/// <summary>Kết quả một lần mua: lý do bị chặn, phần thưởng thực tế (null nếu không mua được) và đã ghi file chưa.</summary>
public readonly struct ShopPurchaseResult
{
    public readonly PurchaseBlock Block;
    public readonly RewardResult Rewards;
    public readonly bool Saved;

    public ShopPurchaseResult(PurchaseBlock block, RewardResult rewards = null, bool saved = false)
    {
        Block = block;
        Rewards = rewards;
        Saved = saved;
    }

    /// <summary>Đã mua và nhận quà.</summary>
    public bool Succeeded => Block == PurchaseBlock.None && Rewards != null;
}

/// <summary>Đọc các gói, kiểm tra điều kiện và mua.</summary>
public interface IShop
{
    /// <summary>Mọi gói theo thứ tự cấu hình.</summary>
    IReadOnlyList<ShopPackRuntime> Packs { get; }

    /// <summary>Gói có ID <paramref name="packId"/>; null nếu không có.</summary>
    ShopPackRuntime Find(string packId);

    /// <summary>Lý do không mua được lúc này (đóng, hết lượt, thiếu tiền, store chưa sẵn sàng); None = mua được.</summary>
    PurchaseBlock GetBlock(ShopPackRuntime pack);

    /// <summary>Còn gói miễn phí nhận được (để hiện chấm than).</summary>
    bool HasFreePack { get; }

    /// <summary>Kiểm tra sang kỳ giới hạn mới; có đổi thì phát <see cref="Changed"/>. UI gọi khi mở và định kỳ.</summary>
    void Refresh();

    /// <summary>
    /// Kiểm tra điều kiện rồi trả tiền (tài nguyên: trừ cùng lần ghi với phần thưởng; store: chờ store xác nhận) và trao quà.
    /// Bất đồng bộ vì thanh toán store phải chờ người chơi.
    /// </summary>
    UniTask<ShopPurchaseResult> BuyAsync(ShopPackRuntime pack);

    /// <summary>Vừa mua, sang kỳ mới hoặc gói mở/đóng.</summary>
    event Action Changed;
}

#endregion
