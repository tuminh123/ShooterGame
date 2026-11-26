using System;
using System.Collections.Generic;

// Họ strategy "một loại thông báo". Mỗi nguồn bọc interface của MỘT miền tầng dưới và là nơi DUY NHẤT chứa luật
// "khi nào báo" của loại đó (giống MissionTrackers đổi event gameplay thành tín hiệu nhiệm vụ).
// Thêm loại thông báo mới: một NotificationKey + một class ở đây + một dòng trong NotificationManager.

#region Contract

/// <summary>Một loại thông báo: đếm việc đang chờ của khóa <see cref="Key"/> từ dữ liệu của miền tầng dưới.</summary>
public interface INotificationSource : IDisposable
{
    /// <summary>Khóa mà nguồn này báo.</summary>
    NotificationKey Key { get; }

    /// <summary>Số việc đang chờ; <paramref name="item"/> = null là tổng, có giá trị là riêng một mục.</summary>
    int Count(string item);

    /// <summary>Dữ liệu của nguồn có thể đã đổi.</summary>
    event Action Changed;

    /// <summary>Nhờ miền tầng dưới kiểm tra sang ngày/kỳ mới (gọi định kỳ); nguồn không phụ thuộc giờ thì để trống.</summary>
    void Sync();
}

#endregion

#region Quest

/// <summary>Mission đã xong chưa nhận, ở các nhóm đang mở. Mục: ID nhóm (MissionDatabaseSO.Id).</summary>
public sealed class QuestNotificationSource : INotificationSource
{
    private readonly IMissions missions;

    public QuestNotificationSource(IMissions missions)
    {
        this.missions = missions ?? throw new ArgumentNullException(nameof(missions));
        missions.Changed += OnChanged;
    }

    /// <inheritdoc/>
    public NotificationKey Key => NotificationKey.Quest;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public int Count(string item)
    {
        int count = 0;
        foreach (MissionBoard board in missions.Boards)
        {
            if (!board.IsOpen || (item != null && board.Database.Id != item)) continue;
            foreach (MissionRuntime mission in board.Missions)
                if (mission.State == MissionState.Claimable) count++;
        }
        return count;
    }

    /// <inheritdoc/>
    public void Sync() => missions.Refresh();

    /// <inheritdoc/>
    public void Dispose() => missions.Changed -= OnChanged;

    private void OnChanged() => Changed?.Invoke();
}

#endregion

#region Login

/// <summary>Lịch đăng nhập còn quà nhận được hôm nay. Mục: ID lịch (LoginCheckInCalendarSO.Id).</summary>
public sealed class LoginNotificationSource : INotificationSource
{
    private readonly ILoginCheckIn checkIn;

    public LoginNotificationSource(ILoginCheckIn checkIn)
    {
        this.checkIn = checkIn ?? throw new ArgumentNullException(nameof(checkIn));
        checkIn.Changed += OnChanged;
    }

    /// <inheritdoc/>
    public NotificationKey Key => NotificationKey.Login;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public int Count(string item)
    {
        int count = 0;
        foreach (LoginCheckInCalendar calendar in checkIn.Calendars)
            if (calendar.CanClaim && (item == null || calendar.Data.Id == item)) count++;
        return count;
    }

    /// <inheritdoc/>
    public void Sync() => checkIn.Refresh();

    /// <inheritdoc/>
    public void Dispose() => checkIn.Changed -= OnChanged;

    private void OnChanged() => Changed?.Invoke();
}

#endregion

#region Shop

/// <summary>
/// Gói MIỄN PHÍ còn lượt nhận. Không báo gói mua được bằng tiền: hầu như lúc nào cũng có, chấm sẽ sáng mãi. Mục: ID gói.
/// </summary>
public sealed class ShopNotificationSource : INotificationSource
{
    private readonly IShop shop;

    public ShopNotificationSource(IShop shop)
    {
        this.shop = shop ?? throw new ArgumentNullException(nameof(shop));
        shop.Changed += OnChanged;
    }

    /// <inheritdoc/>
    public NotificationKey Key => NotificationKey.Shop;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public int Count(string item)
    {
        int count = 0;
        foreach (ShopPackRuntime pack in shop.Packs)
            if ((item == null || pack.Data.Id == item) && pack.Request.IsFree && shop.GetBlock(pack) == PurchaseBlock.None) count++;
        return count;
    }

    /// <inheritdoc/>
    public void Sync() => shop.Refresh();

    /// <inheritdoc/>
    public void Dispose() => shop.Changed -= OnChanged;

    private void OnChanged() => Changed?.Invoke();
}

#endregion

#region Heroes

/// <summary>
/// Nhân vật có việc làm được ngay: lên level (đủ vàng cho CẢ level tiếp theo), lên sao (đủ mảnh) hoặc ghép (chưa có, đủ mảnh).
/// Đếm theo nhân vật, không cộng dồn việc. Mục: tên CharacterType.
/// </summary>
/// <remarks>
/// Level được chia thành nhiều lần bấm (ClicksPerLevel), mỗi lần chỉ tốn một phần giá; nếu chỉ hỏi CanUpgrade (đủ cho một lần bấm)
/// thì gần như lúc nào cũng đúng. Thanh tiến độ tính theo vàng 1:1 nên phần còn thiếu của level = ProgressRequired − Progress.
/// </remarks>
public sealed class HeroNotificationSource : INotificationSource
{
    private readonly ICharacterUpgrades upgrades;
    private readonly ICharacterCollection collection;
    private readonly IPlayerSelection selection;
    private readonly IWallet wallet;

    public HeroNotificationSource(ICharacterUpgrades upgrades, ICharacterCollection collection, IPlayerSelection selection, IWallet wallet)
    {
        this.upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
        this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
        this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        upgrades.Upgraded += OnUpgraded;
        collection.Changed += OnChanged;
        wallet.BalanceChanged += OnBalanceChanged;
    }

    /// <inheritdoc/>
    public NotificationKey Key => NotificationKey.Heroes;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public int Count(string item)
    {
        int count = 0;
        foreach (CharacterDataSO character in selection.PlayableCharacters)
            if ((item == null || character.CharacterType.ToString() == item) && HasAction(character.CharacterType)) count++;
        return count;
    }

    /// <inheritdoc/>
    public void Sync() { }

    /// <inheritdoc/>
    public void Dispose()
    {
        upgrades.Upgraded -= OnUpgraded;
        collection.Changed -= OnChanged;
        wallet.BalanceChanged -= OnBalanceChanged;
    }

    private bool HasAction(CharacterType type)
    {
        CharacterCollectionStatus status = collection.GetStatus(type);
        if (!status.Owned) return status.CanAssemble;
        return upgrades.GetQuote(type, CharacterUpgradeKind.Star).CanUpgrade || CanFinishLevel(type);
    }

    // Đủ vàng cho phần còn thiếu của cả level tiếp theo (không phải chỉ một lần bấm).
    private bool CanFinishLevel(CharacterType type)
    {
        UpgradeQuote quote = upgrades.GetQuote(type, CharacterUpgradeKind.Level);
        if (quote.Block == UpgradeBlock.MaxReached || quote.Block == UpgradeBlock.NotAvailable || quote.Costs == null || quote.Costs.Count == 0)
            return false;
        long remaining = Math.Max(0, quote.ProgressRequired - quote.Progress);
        return remaining > 0 && wallet.GetBalance(quote.Costs[0].Currency) >= remaining;
    }

    private void OnUpgraded(CharacterType type, CharacterUpgradeKind kind) => Changed?.Invoke();

    private void OnBalanceChanged(CurrencyType type, long balance) => Changed?.Invoke();

    private void OnChanged() => Changed?.Invoke();
}

#endregion

#region Gear

/// <summary>
/// Đồ ĐANG MẶC nâng cấp được (đủ tiền, chưa max). Không tính túi đồ: ở level thấp giá rẻ, hầu như luôn có món nâng được.
/// Mục: tên EquipmentSlot.
/// </summary>
public sealed class GearNotificationSource : INotificationSource
{
    private static readonly EquipmentSlot[] Slots = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));

    private readonly IEquipment equipment;
    private readonly IWallet wallet;

    public GearNotificationSource(IEquipment equipment, IWallet wallet)
    {
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        equipment.Changed += OnChanged;
        wallet.BalanceChanged += OnBalanceChanged;
    }

    /// <inheritdoc/>
    public NotificationKey Key => NotificationKey.Gear;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public int Count(string item)
    {
        int count = 0;
        foreach (EquipmentSlot slot in Slots)
        {
            if (item != null && slot.ToString() != item) continue;
            EquipmentItem equipped = equipment.GetEquipped(slot);
            if (equipped != null && equipment.GetQuote(equipped).CanUpgrade) count++;
        }
        return count;
    }

    /// <inheritdoc/>
    public void Sync() { }

    /// <inheritdoc/>
    public void Dispose()
    {
        equipment.Changed -= OnChanged;
        wallet.BalanceChanged -= OnBalanceChanged;
    }

    private void OnBalanceChanged(CurrencyType type, long balance) => Changed?.Invoke();

    private void OnChanged() => Changed?.Invoke();
}

#endregion
