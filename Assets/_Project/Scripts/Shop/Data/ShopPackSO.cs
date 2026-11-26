using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Một gói trong shop: danh sách phần thưởng (<see cref="IReward"/>), giá (<see cref="IShopPrice"/>: tài nguyên, miễn phí
/// hoặc tiền thật) và giới hạn số lần mua. Asset chỉ chứa cấu hình; số lần đã mua nằm ở <see cref="ShopPackRuntime"/>.
/// </summary>
[CreateAssetMenu(fileName = "ShopPack", menuName = "Shooter Game/Shop/Pack")]
public sealed class ShopPackSO : ScriptableObject
{
    #region Configuration

    [Tooltip("ID ổn định được lưu trong save; không trống, không trùng. UI ghép thẻ với gói theo ID này.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("Icon trên thẻ; để trống thì dùng icon của phần thưởng đầu tiên.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Nơi lấy giờ để reset giới hạn Daily/Weekly. Device khi test, Server khi chạy thật.")]
    [SerializeField] private ClockSource clock = ClockSource.Device;
    [SerializeReference] private IShopPrice price = new CurrencyPrice();
    [SerializeField] private PurchaseLimit limit = PurchaseLimit.None;
    [Tooltip("Số lần mua mỗi kỳ (bỏ qua khi Limit = None).")]
    [SerializeField, Min(1)] private int limitCount = 1;
    [SerializeReference] private List<IReward> rewards = new List<IReward>();

    /// <summary>ID ổn định.</summary>
    public string Id => id;

    /// <summary>Tên hiện trên thẻ.</summary>
    public string DisplayName => displayName;

    /// <summary>Icon riêng; null = dùng icon của phần thưởng đầu.</summary>
    public Sprite Icon => icon;

    /// <summary>Nơi lấy giờ của gói.</summary>
    public ClockSource Clock => clock;

    /// <summary>Giới hạn số lần mua.</summary>
    public PurchaseLimit Limit => limit;

    /// <summary>Số lần mua mỗi kỳ.</summary>
    public int LimitCount => limitCount;

    /// <summary>Gói thưởng.</summary>
    public IReadOnlyList<IReward> Rewards => rewards;

    #endregion

    #region Price & Limit

    /// <summary>Mô tả giá thành một yêu cầu mua (ShopManager và UI cùng đọc).</summary>
    public PurchaseRequest CreateRequest()
    {
        var request = new PurchaseRequest();
        price.AddTo(request);
        return request;
    }

    /// <summary>
    /// Nơi DUY NHẤT tính kỳ giới hạn tại <paramref name="utcNow"/>: mã kỳ đổi thì số lần đã mua về 0.
    /// <paramref name="resetsAt"/> là lúc kỳ kết thúc; không có kỳ (None/Lifetime) thì DateTime.MaxValue.
    /// </summary>
    public long GetCycle(DateTime utcNow, out DateTime resetsAt)
    {
        long day = TimeCycles.DayOf(utcNow);
        switch (limit)
        {
            case PurchaseLimit.Daily:
                resetsAt = TimeCycles.DayStart(day + 1);
                return day;
            case PurchaseLimit.Weekly:
                long week = TimeCycles.WeekOf(day);
                resetsAt = TimeCycles.WeekStart(week + 1);
                return week;
            case PurchaseLimit.None:
            case PurchaseLimit.Lifetime:
                resetsAt = DateTime.MaxValue;
                return 0;
            default:
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "Unknown purchase limit.");
        }
    }

    #endregion

    #region Validation

    /// <summary>ID, giá, giới hạn và phần thưởng hợp lệ. Việc thưởng khớp database nhân vật/trang bị do Progression kiểm tra.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException($"{name}: gói chưa có ID.");
        if (!Enum.IsDefined(typeof(ClockSource), clock)) throw new InvalidOperationException($"{name}: nguồn giờ không hợp lệ.");
        if (!Enum.IsDefined(typeof(PurchaseLimit), limit)) throw new InvalidOperationException($"{name}: giới hạn không hợp lệ.");
        if (limitCount < 1) throw new InvalidOperationException($"{name}: Limit Count phải ≥ 1.");
        if (price == null) throw new InvalidOperationException($"{name}: gói chưa có giá.");
        price.Validate();
        if (rewards.Count == 0) throw new InvalidOperationException($"{name}: gói cần ít nhất một phần thưởng.");
        RewardBatch.From(rewards);
    }

    #endregion
}
