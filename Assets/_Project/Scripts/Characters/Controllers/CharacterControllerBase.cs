using System;
using UnityEngine;

// Shared combat and innate-skill activation. Derived controllers specialize movement and death.
public abstract class CharacterControllerBase : ICharacterController
{
    public virtual void SetPaused(bool paused) { }
    public virtual void Dispose() { }
    private CharacterRuntimeData deathHandled;
    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive) return;

        try { view.Skills?.Tick(view.Timers.DeltaTime); }
        catch (Exception exception) { Debug.LogException(exception, view); }

        // A skill can kill, disable or despawn its owner synchronously.
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive) return;

        TickMovement(view, runtime);
    }

    protected abstract void TickMovement(CharacterView view, CharacterRuntimeData runtime);
    public abstract void OnDeath(CharacterView view, CharacterRuntimeData runtime);

    public DamageResult ReceiveDamage(CharacterView view, CharacterRuntimeData runtime, DamageInfo damage)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive ||
            damage.SourceTeam == runtime.Team || damage.Amount <= 0f)
            return default;

        float health = runtime.CurrentHealth;
        float shield = runtime.CurrentShield;
        float absorbed = Mathf.Min(shield, damage.Amount);
        float healthLost = Mathf.Min(health, damage.Amount - absorbed);
        var result = new DamageResult(healthLost, absorbed, healthLost >= health);

        try
        {
            runtime.SetVitals(health - healthLost, shield - absorbed);
            if (IsCurrent(view, runtime))
                view.NotifyDamageReceived(damage, result);
        }
        finally
        {
            if (result.Killed) CompleteDeath(view, runtime);
        }
        return result;
    }

    // Direct HP changes bypass shield. Positive amounts heal; dead characters are not revived.
    public void ChangeHealth(CharacterView view, CharacterRuntimeData runtime, float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive) return;
        try
        {
            runtime.SetVitals(Mathf.Clamp(runtime.CurrentHealth + amount, 0f, runtime.FinalStats.maxHealth),
                runtime.CurrentShield);
        }
        finally
        {
            if (!runtime.IsAlive) CompleteDeath(view, runtime);
        }
    }

    public DamageResult DealDamage(CharacterView view, CharacterRuntimeData runtime,
        CharacterTarget target, float multiplier = 1f)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive ||
            !target.TryGetCharacter(out CharacterView victim)) return default;
        Vector2 away = view.HitPosition - victim.HitPosition;
        if (away.sqrMagnitude <= 0.000001f)
            away = view.GetInstanceID() < victim.GetInstanceID() ? Vector2.left : Vector2.right;
        DamageResult result = victim.ReceiveDamage(runtime.CreateDamage(multiplier));
        if (result.Accepted && IsCurrent(view, runtime) && runtime.IsAlive)
            OnAttack(view, runtime, away);
        return result;
    }

    protected virtual void OnAttack(CharacterView view, CharacterRuntimeData runtime, Vector2 away) { }

    public void GrantShield(CharacterView view, CharacterRuntimeData runtime, float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (IsCurrent(view, runtime) && view.isActiveAndEnabled && runtime.IsAlive)
            runtime.AddShield(amount);
    }

    private void CompleteDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        if (!IsCurrent(view, runtime) || runtime.IsAlive || ReferenceEquals(deathHandled, runtime)) return;
        deathHandled = runtime;
        try { view.EndCombatLifetime(); }
        finally { if (IsCurrent(view, runtime)) OnDeath(view, runtime); }
    }

    private static bool IsCurrent(CharacterView view, CharacterRuntimeData runtime)
        => view != null && runtime != null &&
           ReferenceEquals(view.RuntimeData, runtime);
}
