using System;
using System.Collections.Generic;
using UnityEngine;

// State of one spawned character. Services own levels, EXP and the origins of bonuses.
public sealed class CharacterRuntimeData
{
    private readonly Dictionary<long, StatBonus> temporaryBonuses = new Dictionary<long, StatBonus>();
    private long nextModifierId;
    private StatBonus externalBonus;
    private float movementScale = 1f;
    private float grantedShieldCapacity;

    public CharacterType CharacterType { get; }
    public CharacterTeam Team { get; }
    public string DisplayName { get; }
    public BaseStats BaseStats { get; }
    public BaseStats FinalStats { get; private set; }
    public float CurrentHealth { get; private set; }
    public float MaxShield => FinalStats.defense + grantedShieldCapacity;
    public float CurrentShield { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public event Action Changed;

    public CharacterRuntimeData(CharacterDataSO template, StatBonus bonus = default)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        template.ValidateStats();
        CharacterType = template.CharacterType;
        Team = template.Team;
        DisplayName = template.DisplayName;
        BaseStats = template.BaseStats;
        externalBonus = bonus;
        RebuildFinalStats();
        CurrentHealth = FinalStats.maxHealth;
        CurrentShield = MaxShield;
    }

    // The caller combines progression/equipment bonuses. Runtime does not know their origins.
    internal void SetExternalBonus(StatBonus bonus)
    {
        StatBonus previous = externalBonus;
        externalBonus = bonus;
        try { RebuildFinalStats(); }
        catch { externalBonus = previous; throw; }
        ClampVitalsAndNotify();
    }

    public IDisposable AddTemporaryBonus(StatBonus bonus)
    {
        long id = checked(++nextModifierId);
        temporaryBonuses.Add(id, bonus);
        try { RebuildFinalStats(); }
        catch { temporaryBonuses.Remove(id); throw; }
        float health = CurrentHealth, shield = CurrentShield;
        try { ClampVitalsAndNotify(); }
        catch
        {
            temporaryBonuses.Remove(id);
            RebuildFinalStats();
            CurrentHealth = health;
            CurrentShield = shield;
            throw;
        }
        return new StatModifierLease(() =>
        {
            if (!temporaryBonuses.Remove(id)) return;
            RebuildFinalStats();
            ClampVitalsAndNotify();
        });
    }

    // Effect owns stacking and duration. Runtime only applies its resolved movement multiplier.
    internal void SetMovementScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f || scale > 1f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (movementScale == scale) return;
        float previous = movementScale;
        movementScale = scale;
        try { RebuildFinalStats(); }
        catch { movementScale = previous; throw; }
        ClampVitalsAndNotify();
    }

    private void RebuildFinalStats()
    {
        StatBonus total = externalBonus;
        foreach (StatBonus bonus in temporaryBonuses.Values) total += bonus;
        BaseStats stats = total.ApplyTo(BaseStats);
        // Check before clamping so invalid bonuses cannot hide NaN/infinity behind a limit.
        ValidateFinite(stats);
        stats.maxHealth = Mathf.Max(1f, stats.maxHealth);
        stats.damage = Mathf.Max(0f, stats.damage);
        stats.defense = Mathf.Max(0f, stats.defense);
        stats.moveSpeed = Mathf.Clamp(stats.moveSpeed, 0f, BaseStats.moveSpeed * 2f) * movementScale;
        stats.range = Mathf.Max(0f, stats.range);
        stats.critChance = Mathf.Max(0f, stats.critChance);
        stats.critDamageMultiplier = Mathf.Max(0f, stats.critDamageMultiplier);
        stats.effectHit = Mathf.Max(0f, stats.effectHit);
        stats.effectResistance = Mathf.Clamp(stats.effectResistance, 0f, 80f);
        FinalStats = stats;
    }

    private static void ValidateFinite(BaseStats stats)
    {
        float[] values = { stats.maxHealth, stats.damage, stats.defense, stats.moveSpeed, stats.range,
            stats.critChance, stats.critDamageMultiplier, stats.effectHit, stats.effectResistance };
        foreach (float value in values)
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Stat totals must be finite.");
    }

    private void ClampVitalsAndNotify()
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, FinalStats.maxHealth);
        CurrentShield = Mathf.Clamp(CurrentShield, 0f, MaxShield);
        Changed?.Invoke();
    }

    // Mutation only. Controllers own damage, healing and death decisions.
    internal void AddShield(float amount)
    {
        float shield = CurrentShield + amount;
        if (float.IsNaN(shield) || float.IsInfinity(shield) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        // Explicit grants can extend shield capacity for this spawn; stat recalculation never refills it.
        grantedShieldCapacity = Mathf.Max(grantedShieldCapacity, shield - FinalStats.defense);
        SetVitals(CurrentHealth, shield);
    }

    internal void SetVitals(float health, float shield)
    {
        if (float.IsNaN(health) || float.IsInfinity(health) ||
            float.IsNaN(shield) || float.IsInfinity(shield))
            throw new ArgumentOutOfRangeException(nameof(health));
        health = Mathf.Clamp(health, 0f, FinalStats.maxHealth);
        shield = Mathf.Clamp(shield, 0f, MaxShield);
        if (health == CurrentHealth && shield == CurrentShield) return;
        CurrentHealth = health;
        CurrentShield = shield;
        Changed?.Invoke();
    }

    // Calculation only; existing skill/projectile callers keep using this without skill changes.
    public DamageInfo CreateDamage(float multiplier = 1f)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        float chance = Mathf.Min(FinalStats.critChance, 100f);
        float critBonus = FinalStats.critDamageMultiplier + Mathf.Max(0f, FinalStats.critChance - 100f) * 0.25f;
        bool critical = chance >= 100f || (chance > 0f && UnityEngine.Random.value * 100f < chance);
        float amount = FinalStats.damage * multiplier * (critical ? 1f + critBonus / 100f : 1f);
        return new DamageInfo(amount, Team, critical);
    }
}
