using System;

// Idempotent ownership token for temporary bonuses, slow modifiers and passive cleanup.
public sealed class StatModifierLease : IDisposable
{
    private Action release;
    public StatModifierLease(Action release) => this.release = release;
    public void Dispose()
    {
        Action action = release;
        release = null;
        action?.Invoke();
    }
}
