using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Một banner gacha: giá, <b>bảng tỉ lệ</b>, pity và pool. Mọi con số cân bằng của gacha nằm ở đây (chỉnh trên Inspector);
/// Inspector hiện luôn "trung bình bao nhiêu lượt ra bậc cao nhất" để cân bằng mà không cần chạy game.
/// </summary>
/// <remarks>Chỉ chứa tham số; phép quay ở <see cref="GachaRoller"/>, trạng thái pity của người chơi nằm trong save.</remarks>
[CreateAssetMenu(fileName = "GachaBanner", menuName = "Shooter Game/Gacha/Banner")]
public sealed class GachaBannerSO : ScriptableObject
{
    #region Types

    /// <summary>Trọng số của một bậc. Xác suất = trọng số / tổng trọng số các bậc pool có.</summary>
    [Serializable]
    public sealed class Rate
    {
        [SerializeField] private GachaTier tier;
        [SerializeField, Min(0f)] private float weight;

        public Rate() { }

        public Rate(GachaTier tier, float weight)
        {
            this.tier = tier;
            this.weight = weight;
        }

        public GachaTier Tier => tier;
        public float Weight => weight;
    }

    #endregion

    #region Fields

    [Tooltip("Lưu trong save (bộ đếm pity); không trống, không trùng. Đổi ID thì pity về 0.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("Ảnh trên thẻ banner.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Rương hiện giữa màn khi quay banner này (rung lắc). Trống = rương mặc định của GachaChestView.")]
    [SerializeField] private Sprite chestClosed;
    [Tooltip("Rương đã mở. Trống = rương mở mặc định của GachaChestView.")]
    [SerializeField] private Sprite chestOpen;

    [Tooltip("Tiền dùng để quay.")]
    [SerializeField] private CurrencyType currency = CurrencyType.Diamond;
    [SerializeField, Min(1)] private int singleCost = 150;
    [Tooltip("Giá x10; thường rẻ hơn 10 lần x1 (ví dụ giảm 10%).")]
    [SerializeField, Min(1)] private int tenCost = 1350;

    [Tooltip("Trọng số từng bậc. Bậc pool không có thì bị bỏ, phần còn lại tự chia lại.")]
    [SerializeField] private List<Rate> rates = new List<Rate>();

    [Tooltip("Bậc có pity (thường là bậc cao nhất).")]
    [SerializeField] private GachaTier pityTier = GachaTier.Legendary;
    [Tooltip("Soft pity: từ lượt thứ này (tính từ lần ra bậc pity gần nhất) tỉ lệ bắt đầu tăng. 0 = không có.")]
    [SerializeField, Min(0)] private int softPityStart = 46;
    [Tooltip("Mỗi lượt sau mốc soft pity cộng thêm bao nhiêu xác suất (0.05 = +5%).")]
    [SerializeField, Range(0f, 1f)] private float softPityStep = 0.05f;
    [Tooltip("Hard pity: lượt thứ này chắc chắn ra bậc pity. 0 = không có.")]
    [SerializeField, Min(0)] private int hardPity = 60;

    [Tooltip("Bậc được bảo đảm theo nhóm lượt (thường là Epic: ra Epic hoặc cao hơn).")]
    [SerializeField] private GachaTier guaranteeTier = GachaTier.Epic;
    [Tooltip("Cứ bấy nhiêu lượt liên tiếp có ít nhất một món >= Guarantee Tier. 0 = không có.")]
    [SerializeField, Min(0)] private int guaranteeEvery = 10;

    [Tooltip("CharacterGachaPool = gacha tướng; EquipmentGachaPool = gacha trang bị.")]
    [SerializeReference] private IGachaPool pool;

    #endregion

    #region Getters

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public Sprite ChestClosed => chestClosed;
    public Sprite ChestOpen => chestOpen;
    public CurrencyType Currency => currency;
    public IReadOnlyList<Rate> Rates => rates;
    public GachaTier PityTier => pityTier;
    public int SoftPityStart => softPityStart;
    public float SoftPityStep => softPityStep;
    public int HardPity => hardPity;
    public GachaTier GuaranteeTier => guaranteeTier;
    public int GuaranteeEvery => guaranteeEvery;
    public IGachaPool Pool => pool;

    /// <summary>Giá của <paramref name="count"/> lượt (1 hoặc 10).</summary>
    public CurrencyCost GetCost(int count)
    {
        if (count == 1) return new CurrencyCost(currency, singleCost);
        if (count == 10) return new CurrencyCost(currency, tenCost);
        throw new ArgumentOutOfRangeException(nameof(count), count, "Gacha pulls are x1 or x10.");
    }

    #endregion

    #region Validation

    /// <summary>Ném lỗi nếu cấu hình sai.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Gacha banner needs an ID: " + name);
        if (currency != CurrencyType.Gold && currency != CurrencyType.Diamond)
            throw new InvalidOperationException("Gacha banner must cost Gold or Diamond: " + id);
        if (singleCost < 1 || tenCost < 1) throw new InvalidOperationException("Gacha costs must be at least 1: " + id);
        if (pool == null) throw new InvalidOperationException("Gacha banner needs a pool: " + id);
        pool.Validate();
        var tiers = new HashSet<GachaTier>();
        foreach (Rate rate in rates)
            if (rate == null || !Enum.IsDefined(typeof(GachaTier), rate.Tier) || !tiers.Add(rate.Tier) || rate.Weight < 0f)
                throw new InvalidOperationException("Gacha rates need each defined tier once with weight >= 0: " + id);
        if (GachaRoller.TotalWeight(this) <= 0f) throw new InvalidOperationException("Gacha banner has no tier the pool can give: " + id);
        if (hardPity > 0 && softPityStart > hardPity) throw new InvalidOperationException("Soft pity must start before hard pity: " + id);
        if (guaranteeTier > pityTier) throw new InvalidOperationException("Guarantee Tier must not be above Pity Tier: " + id);
    }

    #endregion
}
