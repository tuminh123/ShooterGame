using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Gacha: quay banner tướng và trang bị, giữ bộ đếm pity. Đây là class <b>duy nhất</b> của Gacha được bind.
/// </summary>
/// <remarks>
/// <code>
/// GachaManager
/// ├── GachaBannerSO[]   data: giá, bảng tỉ lệ, pity, pool (IGachaPool: tướng / trang bị)
/// ├── GachaRoller       công thức quay (static, thuần)
/// └── pity              mỗi banner một bộ đếm (key gacha)
/// </code>
/// Quay trên BẢN SAO bộ đếm, rồi trừ tiền + trao quà + lưu pity trong MỘT lần ghi (IRewardGrant.PurchaseRewards):
/// không mất tiền mà không có quà, thoát game giữa chừng cũng không quay lại được kết quả.
/// Chiều phụ thuộc: Gacha → Progression (ví, trao quà) / Save.
/// </remarks>
public sealed class GachaManager : DomainManager, IGacha, IInitializable
{
    #region Settings

    /// <summary>Các banner, installer gán từ Inspector.</summary>
    [Serializable]
    public sealed class Settings
    {
        [SerializeField] private List<GachaBannerSO> banners = new List<GachaBannerSO>();
        [Tooltip("0 = ngẫu nhiên thật. Khác 0 = luôn ra cùng một chuỗi (chỉ dùng để kiểm tra).")]
        [SerializeField] private int seed;

        public Settings() { }

        public Settings(List<GachaBannerSO> banners, int seed)
        {
            this.banners = banners;
            this.seed = seed;
        }

        /// <summary>Các banner theo thứ tự.</summary>
        public IReadOnlyList<GachaBannerSO> Banners => banners;

        /// <summary>Seed của bộ quay; 0 = theo thời gian.</summary>
        public int Seed => seed;

        /// <summary>Mọi banner hợp lệ và không trùng ID.</summary>
        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GachaBannerSO banner in banners)
            {
                if (banner == null) throw new InvalidOperationException("Gacha settings has an empty banner.");
                banner.Validate();
                if (!ids.Add(banner.Id)) throw new InvalidOperationException("Duplicate gacha banner ID: " + banner.Id);
            }
        }
    }

    #endregion

    #region Fields

    private readonly IReadOnlyList<GachaBannerSO> banners;
    private readonly ISaveStore saves;
    private readonly IWallet wallet;
    private readonly IRewardGrant rewards;
    private readonly System.Random random;
    private readonly Dictionary<string, GachaSaveData.Banner> counters = new Dictionary<string, GachaSaveData.Banner>(StringComparer.Ordinal);
    private bool loaded;

    #endregion

    #region Constructor And Lifecycle

    /// <summary>Giữ banner và bộ quay; chưa đọc save.</summary>
    public GachaManager(Settings settings, ISaveStore saves, IWallet wallet, IRewardGrant rewardGrant)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        banners = settings.Banners;
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        rewards = rewardGrant ?? throw new ArgumentNullException(nameof(rewardGrant));
        random = settings.Seed != 0 ? new System.Random(settings.Seed) : new System.Random();
    }

    /// <summary>Đọc bộ đếm pity sau Save và Progression. Banner chưa có trong save bắt đầu từ 0.</summary>
    public override void Initialize()
    {
        if (loaded || !saves.IsLoaded) return;
        GachaSaveData data = saves.Load(GameConstants.SaveKeys.Gacha, new GachaSaveData(), GachaSaveData.Validate);
        foreach (GachaBannerSO banner in banners) counters[banner.Id] = new GachaSaveData.Banner { bannerId = banner.Id };
        foreach (GachaSaveData.Banner saved in data.banners)
            if (counters.ContainsKey(saved.bannerId)) counters[saved.bannerId] = saved.Copy();
        loaded = true;
    }

    #endregion

    #region IGacha

    /// <inheritdoc/>
    public IReadOnlyList<GachaBannerSO> Banners => banners;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public PurchaseBlock GetBlock(GachaBannerSO banner, int count)
    {
        if (!loaded || banner == null || !counters.ContainsKey(banner.Id)) return PurchaseBlock.Closed;
        return wallet.CanAfford(new[] { banner.GetCost(count) }) ? PurchaseBlock.None : PurchaseBlock.NotEnoughCurrency;
    }

    /// <inheritdoc/>
    public int PullsUntilPity(GachaBannerSO banner)
    {
        // Pool chưa có món nào ở bậc pity (ví dụ chưa có tướng Legendary) thì không hứa hẹn gì.
        if (banner == null || banner.HardPity <= 0 || GachaRoller.BaseChance(banner, banner.PityTier) <= 0f
            || !counters.TryGetValue(banner.Id, out GachaSaveData.Banner counter)) return 0;
        return Math.Max(1, banner.HardPity - counter.sinceTop);
    }

    /// <inheritdoc/>
    public GachaPullResult Pull(GachaBannerSO banner, int count)
    {
        PurchaseBlock block = GetBlock(banner, count);
        if (block != PurchaseBlock.None) return new GachaPullResult(block);

        GachaSaveData.Banner counter = counters[banner.Id].Copy();
        var tiers = new GachaTier[count];
        var picked = new List<IReward>(count);
        for (int i = 0; i < count; i++)
        {
            tiers[i] = GachaRoller.Roll(banner, counter, random);
            picked.Add(banner.Pool.Pick(tiers[i], random));
        }
        // Ném lỗi (cấu hình sai, thiếu tiền) trước khi đổi gì: bộ đếm thật chưa bị đụng tới.
        RewardResult result = rewards.PurchaseRewards(new[] { banner.GetCost(count) }, picked,
            SaveEntry.Of(GameConstants.SaveKeys.Gacha, CreateSaveData(counter)), out bool saved);
        counters[banner.Id] = counter;
        NotifyChanged();
        return new GachaPullResult(result, tiers, saved);
    }

    #endregion

    #region Private Helpers

    // Snapshot mọi banner, thay bộ đếm của banner vừa quay bằng bản mới.
    private GachaSaveData CreateSaveData(GachaSaveData.Banner changed)
    {
        var data = new GachaSaveData();
        foreach (GachaSaveData.Banner counter in counters.Values)
            data.banners.Add(counter.bannerId == changed.bannerId ? changed.Copy() : counter.Copy());
        GachaSaveData.Validate(data);
        return data;
    }

    // Đã trao và lưu xong: lỗi của UI không được làm tưởng chưa quay.
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
