using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Shop: quản lý các gói, kiểm tra điều kiện mua, trả tiền và trao quà.
/// Đây là class <b>duy nhất</b> của Shop được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// ShopManager
/// ├── ShopPackRuntime[]   mỗi asset một gói: kỳ giới hạn + số lần đã mua (key shop)
/// └── IPurchaseCheck      ← CurrencyPurchaseCheck / StorePurchaseCheck   (Strategy, theo PaymentKind)
/// </code>
/// Mua: điều kiện chung (gói mở, còn lượt) → điều kiện riêng của cách trả → trả tiền → trao quà. Tài nguyên được trừ cùng
/// MỘT lần ghi với phần thưởng và số lần đã mua (IRewardGrant.PurchaseRewards).
/// Chiều phụ thuộc: Shop → Progression (ví, trao quà) / Clock / Save.
/// </remarks>
public sealed class ShopManager : DomainManager, IShop, IInitializable, IDisposable
{
    #region Settings

    /// <summary>Các gói bán trong shop, installer gán từ Inspector.</summary>
    [Serializable]
    public sealed class Settings
    {
        [SerializeField] private List<ShopPackSO> packs = new List<ShopPackSO>();

        public Settings() { }

        public Settings(List<ShopPackSO> packs) => this.packs = packs;

        /// <summary>Các gói theo thứ tự.</summary>
        public IReadOnlyList<ShopPackSO> Packs => packs;

        /// <summary>Mọi gói hợp lệ và không trùng ID.</summary>
        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ShopPackSO pack in packs)
            {
                if (pack == null) throw new InvalidOperationException("Shop settings has an empty pack.");
                pack.Validate();
                if (!ids.Add(pack.Id)) throw new InvalidOperationException("Duplicate shop pack ID: " + pack.Id);
            }
        }
    }

    #endregion

    #region Fields

    private readonly ISaveStore saves;
    private readonly IGameClock clock;
    private readonly IRewardGrant rewards;
    private readonly List<ShopPackRuntime> packs = new List<ShopPackRuntime>();
    private readonly Dictionary<PaymentKind, IPurchaseCheck> checks = new Dictionary<PaymentKind, IPurchaseCheck>();
    private bool loaded;
    private bool buying; // Một lần mua mỗi lúc: thanh toán store phải chờ, không cho bấm chồng.
    private bool disposed;

    #endregion

    #region Constructor

    /// <summary>Dựng các gói và bước kiểm tra; chưa đọc save.</summary>
    public ShopManager(Settings settings, ISaveStore saves, IGameClock gameClock, IWallet wallet, IRewardGrant rewardGrant)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (wallet == null) throw new ArgumentNullException(nameof(wallet));
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        rewards = rewardGrant ?? throw new ArgumentNullException(nameof(rewardGrant));
        foreach (ShopPackSO pack in settings.Packs) packs.Add(new ShopPackRuntime(pack));
        foreach (PaymentKind kind in (PaymentKind[])Enum.GetValues(typeof(PaymentKind))) checks.Add(kind, CreateCheck(kind, wallet));
        clock.Changed += Refresh; // Server vừa trả giờ hoặc vừa mất giờ: gói dùng giờ server mở/đóng ngay.
    }

    // Switch DUY NHẤT chọn bước kiểm tra theo cách trả. Thêm cách trả mới: thêm một case.
    private static IPurchaseCheck CreateCheck(PaymentKind kind, IWallet wallet)
    {
        switch (kind)
        {
            case PaymentKind.Currency: return new CurrencyPurchaseCheck(wallet);
            case PaymentKind.Store: return new StorePurchaseCheck();
            default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown payment kind.");
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>Đọc số lần đã mua sau Save và Progression; thưởng cấu hình sai chỉ báo lỗi, không chặn game.</summary>
    public override void Initialize()
    {
        if (disposed || loaded || !saves.IsLoaded) return;
        ShopSaveData data = saves.Load(GameConstants.SaveKeys.Shop, new ShopSaveData(), ShopSaveData.Validate);
        foreach (ShopSaveData.Pack saved in data.packs)
            foreach (ShopPackRuntime pack in packs)
                if (string.Equals(saved.packId, pack.Data.Id, StringComparison.Ordinal)) pack.Restore(saved);
        loaded = true;
        RefreshPacks();
        foreach (ShopPackRuntime pack in packs)
        {
            try { rewards.ValidateRewards(pack.Data.Rewards); }
            catch (Exception exception) { Debug.LogException(exception, pack.Data); }
        }
    }

    /// <summary>Gỡ listener. Không ghi ở đây: mọi thay đổi cần lưu đã ghi lúc mua.</summary>
    public override void Dispose()
    {
        if (disposed) return;
        clock.Changed -= Refresh;
        disposed = true;
        Changed = null;
    }

    #endregion

    #region IShop

    /// <inheritdoc/>
    public IReadOnlyList<ShopPackRuntime> Packs => packs;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public ShopPackRuntime Find(string packId)
    {
        foreach (ShopPackRuntime pack in packs)
            if (string.Equals(pack.Data.Id, packId, StringComparison.Ordinal)) return pack;
        return null;
    }

    /// <inheritdoc/>
    public PurchaseBlock GetBlock(ShopPackRuntime pack)
    {
        if (pack == null) throw new ArgumentNullException(nameof(pack));
        if (!loaded || disposed || !pack.IsOpen) return PurchaseBlock.Closed;
        if (pack.Remaining <= 0) return PurchaseBlock.LimitReached;
        return checks[pack.Request.Kind].Check(pack.Request);
    }

    /// <inheritdoc/>
    public bool HasFreePack
    {
        get
        {
            foreach (ShopPackRuntime pack in packs)
                if (pack.Request.IsFree && GetBlock(pack) == PurchaseBlock.None) return true;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Refresh()
    {
        if (loaded && !disposed && RefreshPacks()) NotifyChanged();
    }

    /// <inheritdoc/>
    public async UniTask<ShopPurchaseResult> BuyAsync(ShopPackRuntime pack)
    {
        if (pack == null) throw new ArgumentNullException(nameof(pack));
        if (!packs.Contains(pack)) throw new ArgumentException("Pack does not belong to this shop.", nameof(pack));
        if (buying) return new ShopPurchaseResult(PurchaseBlock.Busy);
        Refresh(); // Có thể vừa qua kỳ mới trong lúc UI còn mở.
        PurchaseBlock block = GetBlock(pack);
        if (block != PurchaseBlock.None) return new ShopPurchaseResult(block);
        // Kiểm tra gói TRƯỚC khi trả tiền: cấu hình sai thì ném lỗi mà người chơi không mất gì.
        rewards.ValidateRewards(pack.Data.Rewards);

        buying = true;
        try
        {
            block = await checks[pack.Request.Kind].PayAsync(pack.Request);
            if (block != PurchaseBlock.None) return new ShopPurchaseResult(block);
            if (disposed)
            {
                // Đóng scene trong lúc chờ store: không còn chỗ ghi; store sẽ báo lại giao dịch chưa hoàn tất ở lần mở sau (khi có IAP thật).
                Debug.LogError("Shop closed while a payment was pending: " + pack.Data.Id);
                return new ShopPurchaseResult(PurchaseBlock.PaymentFailed);
            }
            RewardResult result = Grant(pack, out bool saved);
            NotifyChanged();
            return new ShopPurchaseResult(PurchaseBlock.None, result, saved);
        }
        finally { buying = false; }
    }

    #endregion

    #region Private Helpers

    // Số lần mua tính trước snapshot để nằm cùng lần ghi với tiền và phần thưởng; trao lỗi (ném trước khi đổi gì) thì hoàn lại.
    private RewardResult Grant(ShopPackRuntime pack, out bool saved)
    {
        pack.MarkBought();
        try
        {
            return rewards.PurchaseRewards(pack.Request.Costs, pack.Data.Rewards,
                SaveEntry.Of(GameConstants.SaveKeys.Shop, CreateSaveData()), out saved);
        }
        catch
        {
            pack.UndoBought();
            throw;
        }
    }

    // Mỗi gói đọc giờ từ nguồn asset của nó chọn.
    private bool RefreshPacks()
    {
        bool changed = false;
        foreach (ShopPackRuntime pack in packs)
        {
            bool hasTime = clock.TryGetUtcNow(pack.Data.Clock, out DateTime now);
            changed |= pack.Refresh(now, hasTime);
        }
        return changed;
    }

    private ShopSaveData CreateSaveData()
    {
        var data = new ShopSaveData();
        foreach (ShopPackRuntime pack in packs) data.packs.Add(pack.ToSaveData());
        ShopSaveData.Validate(data);
        return data;
    }

    // Lỗi trình bày UI không được làm tưởng chưa mua.
    private void NotifyChanged()
    {
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion
}
