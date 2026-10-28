using System;
using UnityEngine;

public sealed class StatusEffectRuntime : IDisposable
{
    public StatusEffectDataSO Data { get; }
    public float Remaining { get; internal set; }
    public CharacterTeam SourceTeam { get; internal set; }
    public float SourceDamage { get; internal set; }
    internal float TickElapsed;
    internal IDisposable Modifier;
    internal IStatusEffect Strategy;
    internal bool Disposed;
    internal StatusEffectRuntime(StatusEffectDataSO data, IStatusEffect strategy, CharacterTeam sourceTeam,
        float sourceDamage)
    {
        Data = data; Strategy = strategy;
        Refresh(sourceTeam, sourceDamage);
    }
    internal void Refresh(CharacterTeam sourceTeam, float sourceDamage)
    {
        Remaining = Data.Duration;
        SourceTeam = sourceTeam;
        SourceDamage = sourceDamage;
        // Preserve periodic cadence so rapid reapplication cannot postpone all damage.
    }
    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Modifier?.Dispose();
        Modifier = null;
    }
}
