using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pause/slow motion nhiều nguồn: mỗi nguồn giữ token riêng; mức chậm nhất có hiệu lực đến khi nguồn đó được gỡ.
/// </summary>
/// <remarks>Do <see cref="GameManager"/> tạo; thay cho GameplayPauseService cũ. Miền khác dùng qua <see cref="IGamePause"/>.</remarks>
public sealed class GamePause : IDisposable
{

    #region Dependencies And State
    private readonly Dictionary<long, float> leases = new Dictionary<long, float>();
    private long nextId;
    private float previousTimeScale;
    private bool disposed;

    #endregion

    #region Runtime Operations

    public IDisposable Acquire() => AcquireScale(0f);

    // Multiplier tính trên tốc độ trước token đầu tiên; slow motion không thể gỡ pause của nguồn khác.
    public IDisposable AcquireSlowMotion(float multiplier)
    {
        if (float.IsNaN(multiplier) || multiplier <= 0f || multiplier > 1f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        return AcquireScale(multiplier);
    }
    private IDisposable AcquireScale(float multiplier)
    {
        if (disposed) throw new ObjectDisposedException(nameof(GamePause));
        long id = checked(++nextId);
        if (leases.Count == 0) previousTimeScale = Time.timeScale;
        leases.Add(id, multiplier);
        ApplyTimeScale();
        return new PauseLease(this, id);
    }

    #endregion

    #region Cleanup

    private void Release(long id)
    {
        if (leases.Remove(id)) ApplyTimeScale();
    }

    #endregion

    #region Runtime Operations

    private void ApplyTimeScale()
    {
        float multiplier = 1f;
        foreach (float value in leases.Values) multiplier = Mathf.Min(multiplier, value);
        Time.timeScale = previousTimeScale * multiplier;
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (leases.Count > 0) Time.timeScale = previousTimeScale;
        leases.Clear();
    }

    #endregion

    #region Supporting Types

    private sealed class PauseLease : IDisposable
    {
        private GamePause owner;
        private readonly long id;
        public PauseLease(GamePause owner, long id) { this.owner = owner;
        this.id = id; }
        public void Dispose() { GamePause previous = owner; owner = null; previous?.Release(id); }
    }

    #endregion
}
