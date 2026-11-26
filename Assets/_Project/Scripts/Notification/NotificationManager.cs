using System;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Notification: quản lý chấm báo cho các hệ thống. Đây là class <b>duy nhất</b> của Notification được bind.
/// </summary>
/// <remarks>
/// <code>
/// NotificationManager                   tầng TRÊN CÙNG: chỉ đọc interface của các Manager tầng dưới
/// └── NotificationCounter
///     └── INotificationSource ← Quest / Login / Shop / Heroes / Gear   (Strategy, NotificationSources.cs)
/// </code>
/// Chiều phụ thuộc: Notification → Mission / LoginCheckIn / Shop / Progression / Character. Không miền nào bên dưới biết nó;
/// chiều ngược chỉ là event có sẵn của các miền đó. Thêm loại thông báo: một NotificationKey + một nguồn + một dòng trong constructor.
/// Tick: báo UI các khóa đổi (một lần mỗi frame) và mỗi giây nhờ các miền kiểm tra sang ngày mới, kể cả khi Home đang pause.
/// </remarks>
public sealed class NotificationManager : DomainManager, INotifications, IInitializable, ITickable, IDisposable
{
    #region Fields

    private const float SyncInterval = 1f; // Giây thực giữa hai lần nhờ các miền kiểm tra sang ngày/kỳ mới.

    private readonly NotificationCounter counter = new NotificationCounter();
    private float nextSync;
    // Chưa Initialize thì các miền tầng dưới có thể chưa đọc save (Progression ném lỗi nếu bị hỏi): không hỏi nguồn nào.
    // View trong scene bật (OnEnable) ngay sau khi inject, TRƯỚC Initialize của mọi Manager, nên phải chặn ở đây.
    private bool initialized;
    private bool disposed;

    #endregion

    #region Constructor

    /// <summary>Tạo các nguồn thông báo từ interface của tầng dưới và khóa gộp Home.</summary>
    public NotificationManager(IMissions missions, ILoginCheckIn loginCheckIn, IShop shop, ICharacterUpgrades characterUpgrades,
        ICharacterCollection characterCollection, IPlayerSelection playerSelection, IEquipment equipment, IWallet wallet)
    {
        // Nguồn mới: thêm một dòng ở đây.
        counter.Add(new QuestNotificationSource(missions));
        counter.Add(new LoginNotificationSource(loginCheckIn));
        counter.Add(new ShopNotificationSource(shop));
        counter.Add(new HeroNotificationSource(characterUpgrades, characterCollection, playerSelection, wallet));
        counter.Add(new GearNotificationSource(equipment, wallet));
        counter.AddComposite(NotificationKey.Home, NotificationKey.Quest, NotificationKey.Login);
        counter.Changed += OnCounterChanged;
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Chạy sau khi các miền tầng dưới đã đọc save (xem InitializeOrder): từ đây mới trả lời <see cref="Get"/>,
    /// và báo mọi khóa ở Tick đầu tiên để các chấm đang tắt vẽ lại đúng.
    /// </summary>
    public override void Initialize()
    {
        if (disposed || initialized) return;
        initialized = true;
        counter.MarkAll();
    }

    /// <summary>Mỗi giây nhờ các miền kiểm tra sang ngày mới; rồi báo UI các khóa đã đổi (một lần mỗi frame).</summary>
    // Dùng unscaled time: Home pause (timeScale = 0) nhưng sang ngày mới vẫn phải bật/tắt chấm.
    public override void Tick()
    {
        if (disposed || !initialized) return;
        if (Time.unscaledTime >= nextSync)
        {
            nextSync = Time.unscaledTime + SyncInterval;
            counter.Sync();
        }
        counter.Flush();
    }

    /// <summary>Gỡ listener khỏi các miền tầng dưới khi chúng còn sống (Dispose chạy đầu tiên).</summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        counter.Changed -= OnCounterChanged;
        counter.Dispose();
        Changed = null;
    }

    #endregion

    #region INotifications

    /// <inheritdoc/>
    public event Action<NotificationKey> Changed;

    /// <inheritdoc/>
    /// <remarks>Trước Initialize (hoặc sau Dispose) luôn trả 0: chấm tắt, không hỏi miền nào.</remarks>
    public NotificationState Get(NotificationKey key, string item = null)
        => initialized && !disposed ? counter.Get(key, item) : new NotificationState(0);

    #endregion

    #region Private Helpers

    // Lỗi của một view không được chặn các view khác.
    private void OnCounterChanged(NotificationKey key)
    {
        if (Changed == null) return;
        foreach (Action<NotificationKey> listener in Changed.GetInvocationList())
        {
            try { listener(key); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion
}
