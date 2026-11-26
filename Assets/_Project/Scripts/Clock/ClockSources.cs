using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Họ strategy "lấy giờ từ đâu". ClockManager tạo mỗi nguồn một bản qua switch duy nhất (ClockManager.Create).
// Thêm nguồn mới: một giá trị ClockSource + một class ở đây + một case trong switch đó.

#region Contract

/// <summary>Một cách lấy giờ UTC. Chỉ trả giờ thô; luật dùng giờ nằm ở bên gọi.</summary>
internal interface IClockSource : IDisposable
{
    /// <summary>Giờ hiện tại; false nếu nguồn chưa có giờ.</summary>
    bool TryGetUtcNow(out DateTime utcNow);

    /// <summary>Bắt đầu hoặc làm mới việc lấy giờ (lúc khởi động và khi app quay lại từ nền).</summary>
    void Sync();

    /// <summary>Nguồn vừa có hoặc vừa mất giờ.</summary>
    event Action Changed;
}

#endregion

#region Device

/// <summary>Giờ máy: luôn có, nhưng người chơi chỉnh được. Dùng cho tính năng không cần chống gian lận (Daily/Weekly mission).</summary>
internal sealed class DeviceClockSource : IClockSource
{
    /// <inheritdoc/>
    public event Action Changed { add { } remove { } } // Giờ máy không bao giờ mất.

    /// <inheritdoc/>
    public bool TryGetUtcNow(out DateTime utcNow)
    {
        utcNow = DateTime.UtcNow;
        return true;
    }

    /// <inheritdoc/>
    public void Sync() { }

    /// <inheritdoc/>
    public void Dispose() { }
}

#endregion

#region Server

/// <summary>
/// Giờ server: hỏi một URL HTTPS và đọc header <c>Date</c> của phản hồi, rồi cộng thời gian thực đã trôi.
/// Chỉnh giờ máy tới hay lùi đều không ảnh hưởng, vì <see cref="Time.realtimeSinceStartupAsDouble"/> không theo giờ máy.
/// </summary>
/// <remarks>
/// Header <c>Date</c> chỉ chính xác tới giây, đủ cho việc tính ngày/kỳ. Chưa hỏi được (mất mạng, lỗi, đang chờ) thì báo chưa có giờ.
/// Sau này có backend riêng chỉ cần đổi URL trong <see cref="ClockManager.Settings"/>.
/// </remarks>
internal sealed class ServerClockSource : IClockSource
{
    #region Fields

    private readonly string url;
    private readonly int timeoutSeconds;
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource(); // Hủy request đang chờ khi đóng scene.
    private DateTime serverUtcAtSync; // Giờ server ở lần hỏi gần nhất.
    private double realtimeAtSync; // Thời gian thực của app lúc nhận được giờ đó.
    private bool synced;
    private bool fetching;
    private bool disposed;

    #endregion

    #region Constructor

    /// <summary>Chưa hỏi gì; ClockManager gọi <see cref="Sync"/> lúc Initialize.</summary>
    internal ServerClockSource(string url, int timeoutSeconds)
    {
        this.url = url ?? throw new ArgumentNullException(nameof(url));
        this.timeoutSeconds = timeoutSeconds;
    }

    #endregion

    #region IClockSource

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public bool TryGetUtcNow(out DateTime utcNow)
    {
        if (!synced)
        {
            utcNow = default;
            return false;
        }
        utcNow = serverUtcAtSync.AddSeconds(Time.realtimeSinceStartupAsDouble - realtimeAtSync);
        return true;
    }

    /// <inheritdoc/>
    // Quên giờ cũ trước khi hỏi lại: lúc app ở nền, thời gian thực của app không chắc chạy đúng trên mọi nền tảng.
    public void Sync()
    {
        if (disposed) return;
        if (synced)
        {
            synced = false;
            Changed?.Invoke();
        }
        if (!fetching) FetchAsync(lifetime.Token).Forget();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Changed = null;
        lifetime.Cancel();
        lifetime.Dispose();
    }

    #endregion

    #region Fetch

    private async UniTaskVoid FetchAsync(CancellationToken token)
    {
        fetching = true;
        try
        {
            using (UnityWebRequest request = UnityWebRequest.Head(url))
            {
                request.timeout = timeoutSeconds;
                await request.SendWebRequest().ToUniTask(cancellationToken: token);
                if (!TryParseDate(request.GetResponseHeader("Date"), out DateTime serverUtc))
                {
                    Debug.LogWarning("[Clock] Server response has no valid Date header: " + url);
                    return;
                }
                serverUtcAtSync = serverUtc;
                realtimeAtSync = Time.realtimeSinceStartupAsDouble;
                synced = true;
            }
        }
        catch (OperationCanceledException)
        {
            return; // Đóng scene trong lúc chờ.
        }
        catch (UnityWebRequestException exception)
        {
            // Mất mạng hoặc server lỗi: giữ trạng thái "chưa có giờ", tính năng dùng giờ server tiếp tục ẩn.
            Debug.LogWarning("[Clock] Could not get server time: " + exception.Message);
            return;
        }
        finally
        {
            fetching = false;
        }
        if (!disposed) Changed?.Invoke();
    }

    // Header Date theo RFC 1123, ví dụ "Thu, 26 Nov 2026 03:15:42 GMT".
    private static bool TryParseDate(string value, out DateTime utc)
        => DateTime.TryParseExact(value, "r", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);

    #endregion
}

#endregion
