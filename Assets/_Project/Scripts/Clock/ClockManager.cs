using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Clock: giữ mỗi nguồn giờ một bản và trả giờ theo nguồn mà data chọn.
/// Đây là class <b>duy nhất</b> của Clock được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// ClockManager
/// └── IClockSource ← DeviceClockSource / ServerClockSource   (Strategy, ClockSources.cs)
/// </code>
/// Không phụ thuộc miền nào. Mission (và sau này Login Check-in) đọc giờ qua <see cref="IGameClock"/>;
/// installer gọi <see cref="IClockSync.Resync"/> khi app quay lại từ nền.
/// </remarks>
public sealed class ClockManager : DomainManager, IGameClock, IClockSync, IInitializable, IDisposable
{
    #region Settings

    /// <summary>Cấu hình nguồn giờ server, installer gán từ Inspector.</summary>
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("URL HTTPS bất kỳ; chỉ đọc header Date của phản hồi. Có backend riêng thì đổi sang endpoint của mình.")]
        [SerializeField] private string serverUrl = "https://www.google.com";
        [Tooltip("Thời gian chờ tối đa của một lần hỏi giờ, theo giây.")]
        [SerializeField, Min(1)] private int timeoutSeconds = 5;

        public Settings() { }

        /// <summary>URL hỏi giờ.</summary>
        public string ServerUrl => serverUrl;

        /// <summary>Thời gian chờ tối đa, theo giây.</summary>
        public int TimeoutSeconds => timeoutSeconds;

        /// <summary>URL là địa chỉ HTTPS tuyệt đối và thời gian chờ dương.</summary>
        public void Validate()
        {
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Clock settings: Server Url must be an absolute HTTPS URL.");
            if (timeoutSeconds < 1) throw new InvalidOperationException("Clock settings: Timeout Seconds must be at least 1.");
        }
    }

    #endregion

    #region Fields

    private readonly Dictionary<ClockSource, IClockSource> sources = new Dictionary<ClockSource, IClockSource>();
    private bool disposed;

    #endregion

    #region Constructor

    /// <summary>Tạo mỗi nguồn trong <see cref="ClockSource"/> một bản; chưa hỏi server.</summary>
    public ClockManager(Settings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        foreach (ClockSource type in (ClockSource[])Enum.GetValues(typeof(ClockSource)))
        {
            IClockSource source = Create(type, settings);
            source.Changed += OnSourceChanged;
            sources.Add(type, source);
        }
    }

    // Switch DUY NHẤT tạo nguồn giờ. Thêm nguồn mới: thêm một case.
    private static IClockSource Create(ClockSource type, Settings settings)
    {
        switch (type)
        {
            case ClockSource.Device: return new DeviceClockSource();
            case ClockSource.Server: return new ServerClockSource(settings.ServerUrl, settings.TimeoutSeconds);
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown clock source.");
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>Bắt đầu hỏi giờ server; trong lúc chờ, tính năng dùng giờ server bị ẩn.</summary>
    public override void Initialize()
    {
        if (!disposed) SyncAll();
    }

    /// <summary>Hủy request đang chờ và gỡ listener.</summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Changed = null;
        foreach (IClockSource source in sources.Values)
        {
            source.Changed -= OnSourceChanged;
            source.Dispose();
        }
    }

    #endregion

    #region IGameClock / IClockSync

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public bool TryGetUtcNow(ClockSource source, out DateTime utcNow)
    {
        if (!sources.TryGetValue(source, out IClockSource clock))
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown clock source.");
        if (!disposed) return clock.TryGetUtcNow(out utcNow);
        utcNow = default;
        return false;
    }

    /// <inheritdoc/>
    public void Resync()
    {
        if (!disposed) SyncAll();
    }

    #endregion

    #region Private Helpers

    private void SyncAll()
    {
        foreach (IClockSource source in sources.Values) source.Sync();
    }

    private void OnSourceChanged() => Changed?.Invoke();

    #endregion
}
