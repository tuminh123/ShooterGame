using System;
using System.Collections.Generic;
using UnityEngine;

// Mỗi nguồn pause giữ một token riêng. Đóng bảng skill không bỏ pause từ PauseUI.
public sealed class GameplayPauseService : IDisposable
{
    private readonly HashSet<long> leases = new HashSet<long>();
    private long nextId;
    private float previousTimeScale;
    private bool disposed;

    public IDisposable Acquire()
    {
        if (disposed) throw new ObjectDisposedException(nameof(GameplayPauseService));
        long id = checked(++nextId);
        if (leases.Count == 0) previousTimeScale = Time.timeScale;
        leases.Add(id);
        Time.timeScale = 0f;
        return new PauseLease(this, id);
    }

    private void Release(long id)
    {
        if (leases.Remove(id) && leases.Count == 0) Time.timeScale = previousTimeScale;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (leases.Count > 0) Time.timeScale = previousTimeScale;
        leases.Clear();
    }

    private sealed class PauseLease : IDisposable
    {
        private GameplayPauseService owner;
        private readonly long id;
        public PauseLease(GameplayPauseService owner, long id) { this.owner = owner; this.id = id; }
        public void Dispose() { GameplayPauseService previous = owner; owner = null; previous?.Release(id); }
    }
}
