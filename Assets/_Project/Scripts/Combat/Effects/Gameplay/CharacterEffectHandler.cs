using System;
using System.Collections.Generic;
using UnityEngine;

// Giữ modifier của đúng một lượt spawn. Vòng đời gameplay effect do EffectManager quản lý.
public sealed class CharacterEffectHandler : IDisposable
{
    private readonly CharacterView owner;
    private readonly CharacterRuntimeData identity;
    private readonly EffectManager manager;
    private readonly Dictionary<long, float> speedMultipliers = new Dictionary<long, float>();
    private long nextModifierId;

    public bool IsDisposed { get; private set; }
    public int Count => manager.CountFor(owner, identity);

    public CharacterEffectHandler(CharacterView owner, EffectManager manager)
    {
        this.owner = owner;
        identity = owner.RuntimeData;
        this.manager = manager;
    }

    internal long AddSpeedMultiplier(float multiplier)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(CharacterEffectHandler));
        if (float.IsNaN(multiplier) || multiplier < 0f || multiplier > 1f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        long id = checked(++nextModifierId);
        speedMultipliers.Add(id, multiplier);
        try { RefreshMovementScale(); }
        catch
        {
            speedMultipliers.Remove(id);
            RefreshMovementScale();
            throw;
        }
        return id;
    }

    internal void RemoveSpeedMultiplier(long id)
    {
        if (speedMultipliers.Remove(id)) RefreshMovementScale();
    }

    private void RefreshMovementScale()
    {
        // Nhiều nguồn làm chậm cùng tồn tại: lấy mức mạnh nhất, không nhân chồng lên nhau.
        float scale = 1f;
        foreach (float multiplier in speedMultipliers.Values) scale = Mathf.Min(scale, multiplier);
        identity.SetMovementScale(scale);
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        manager.ClearTarget(owner, identity);
        // ClearTarget có thể hoãn Remove đến cuối callback; dọn modifier ngay khi cá thể kết thúc.
        speedMultipliers.Clear();
        RefreshMovementScale();
    }
}
