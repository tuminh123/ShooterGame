using System;
using System.Collections.Generic;

// Runtime mutable được cấp theo lease; object giữ lại dictionary để dùng lại giữa các lượt spawn.
public sealed class CharacterRuntimePool : IDisposable
{
    private readonly Stack<CharacterRuntimeData> available = new Stack<CharacterRuntimeData>();
    private readonly HashSet<CharacterRuntimeData> owned = new HashSet<CharacterRuntimeData>();
    private readonly HashSet<CharacterRuntimeData> leased = new HashSet<CharacterRuntimeData>();
    private bool disposed;

    public int AvailableCount => available.Count;
    public int ActiveCount => leased.Count;
    public int CreatedCount => owned.Count;

    public void Prewarm(int count)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterRuntimePool));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        leased.EnsureCapacity(checked(leased.Count + count));
        while (available.Count < count)
        {
            var runtime = new CharacterRuntimeData();
            owned.Add(runtime);
            available.Push(runtime);
        }
    }

    public CharacterRuntimeData Rent(CharacterDataSO template, StatBonus bonus)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterRuntimePool));
        if (available.Count == 0) Prewarm(1); // Thiếu số đã dự trù thì mở rộng, không bỏ mất spawn.
        CharacterRuntimeData runtime = available.Pop();
        try { runtime.InitializeSpawn(template, bonus); }
        catch { runtime.ResetForPool(); available.Push(runtime); throw; }
        leased.Add(runtime);
        return runtime;
    }

    internal bool TryReturn(CharacterRuntimeData runtime, ulong generation)
    {
        // Chấp nhận cả runtime được caller tự new: factory không nhận quyền sở hữu object bên ngoài.
        if (disposed || runtime == null || runtime.Generation != generation || !leased.Remove(runtime)) return false;
        runtime.ResetForPool();
        available.Push(runtime);
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (CharacterRuntimeData runtime in owned) runtime.ResetForPool();
        available.Clear();
        leased.Clear();
        owned.Clear();
    }
}
