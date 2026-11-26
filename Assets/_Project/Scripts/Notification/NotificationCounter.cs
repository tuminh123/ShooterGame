using System;
using System.Collections.Generic;

/// <summary>
/// Gom các nguồn thông báo: đánh dấu khóa khi nguồn đổi, phát <see cref="Changed"/> một lần mỗi khóa ở <see cref="Flush"/>,
/// và cộng khóa gộp (ví dụ Home = Quest + Login). Class thuần, NotificationManager tạo; tách riêng để kiểm tra bằng nguồn giả.
/// </summary>
/// <remarks>
/// Không cache số đếm: <see cref="Get"/> hỏi thẳng nguồn (vài phần tử, rất rẻ), nên không bao giờ lệch với dữ liệu thật.
/// Chỉ việc báo UI được gom lại, vì một thao tác (mua gói) có thể làm nhiều nguồn phát event liền nhau.
/// </remarks>
public sealed class NotificationCounter : IDisposable
{
    #region State

    private readonly Dictionary<NotificationKey, INotificationSource> sources = new Dictionary<NotificationKey, INotificationSource>();
    private readonly Dictionary<NotificationKey, Action> handlers = new Dictionary<NotificationKey, Action>();
    private readonly Dictionary<NotificationKey, NotificationKey[]> composites = new Dictionary<NotificationKey, NotificationKey[]>();
    private readonly HashSet<NotificationKey> dirty = new HashSet<NotificationKey>();
    private readonly List<NotificationKey> flushing = new List<NotificationKey>(); // Dùng lại mỗi lần Flush, không cấp phát.

    /// <summary>Thông báo của một khóa có thể đã đổi (phát trong <see cref="Flush"/>).</summary>
    public event Action<NotificationKey> Changed;

    #endregion

    #region Setup

    /// <summary>Thêm một nguồn; mỗi khóa một nguồn.</summary>
    public void Add(INotificationSource source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        NotificationKey key = source.Key;
        if (sources.ContainsKey(key) || composites.ContainsKey(key)) throw new InvalidOperationException("Duplicate notification key: " + key);
        Action handler = () => dirty.Add(key);
        sources.Add(key, source);
        handlers.Add(key, handler);
        source.Changed += handler;
        dirty.Add(key);
    }

    /// <summary>Thêm khóa gộp: tổng của các khóa <paramref name="parts"/>; đổi khi một trong số đó đổi.</summary>
    public void AddComposite(NotificationKey key, params NotificationKey[] parts)
    {
        if (parts == null || parts.Length == 0) throw new ArgumentException("A composite needs at least one part.", nameof(parts));
        if (sources.ContainsKey(key) || composites.ContainsKey(key)) throw new InvalidOperationException("Duplicate notification key: " + key);
        composites.Add(key, parts);
        dirty.Add(key);
    }

    #endregion

    #region Read

    /// <summary>Số việc đang chờ; khóa gộp bỏ qua <paramref name="item"/> và cộng tổng các phần.</summary>
    public NotificationState Get(NotificationKey key, string item = null)
    {
        if (sources.TryGetValue(key, out INotificationSource source)) return new NotificationState(source.Count(item));
        if (!composites.TryGetValue(key, out NotificationKey[] parts)) return new NotificationState(0);
        int total = 0;
        foreach (NotificationKey part in parts) total += Get(part).Count;
        return new NotificationState(total);
    }

    #endregion

    #region Update (NotificationManager)

    /// <summary>Nhờ các nguồn kiểm tra sang ngày/kỳ mới; nguồn nào đổi sẽ tự đánh dấu.</summary>
    public void Sync()
    {
        foreach (INotificationSource source in sources.Values) source.Sync();
    }

    /// <summary>Đánh dấu mọi khóa (lần đầu sau khi các miền đã đọc save).</summary>
    public void MarkAll()
    {
        foreach (NotificationKey key in sources.Keys) dirty.Add(key);
        foreach (NotificationKey key in composites.Keys) dirty.Add(key);
    }

    /// <summary>Phát <see cref="Changed"/> một lần cho mỗi khóa đã đánh dấu và mỗi khóa gộp chứa nó.</summary>
    public void Flush()
    {
        if (dirty.Count == 0) return;
        foreach (KeyValuePair<NotificationKey, NotificationKey[]> composite in composites)
            foreach (NotificationKey part in composite.Value)
                if (dirty.Contains(part)) dirty.Add(composite.Key);
        flushing.Clear();
        flushing.AddRange(dirty);
        dirty.Clear(); // Xóa trước khi báo: listener có thể làm nguồn đổi tiếp, lần đó báo ở frame sau.
        foreach (NotificationKey key in flushing) Changed?.Invoke(key);
    }

    /// <summary>Gỡ listener và giải phóng các nguồn.</summary>
    public void Dispose()
    {
        foreach (KeyValuePair<NotificationKey, INotificationSource> pair in sources)
        {
            pair.Value.Changed -= handlers[pair.Key];
            pair.Value.Dispose();
        }
        sources.Clear();
        handlers.Clear();
        Changed = null;
    }

    #endregion
}
