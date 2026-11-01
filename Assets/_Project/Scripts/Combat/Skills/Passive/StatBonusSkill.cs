using System;
using UnityEngine;

[Serializable]
public sealed class StatBonusSkill : ISkill, IDisposable
{
    [SerializeField] private StatBonus bonus;
    [NonSerialized] private readonly Func<StatBonus, IDisposable> apply;
    [NonSerialized] private IDisposable lease;
    [NonSerialized] private bool disposed;
    public StatBonus Bonus => bonus;

    public StatBonusSkill() { }
    public StatBonusSkill(StatBonus bonus, Func<StatBonus, IDisposable> apply)
    { this.bonus = bonus; this.apply = apply ?? throw new ArgumentNullException(nameof(apply)); }

    public bool TryExecute()
    {
        if (disposed || lease != null || apply == null) return false;
        IDisposable result = apply(bonus);
        // Applying a modifier can synchronously release this registration.
        if (disposed) { result?.Dispose(); return false; }
        lease = result ?? throw new InvalidOperationException("A persistent bonus must return a cleanup token.");
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        IDisposable previous = lease; lease = null;
        previous?.Dispose();
    }
}
