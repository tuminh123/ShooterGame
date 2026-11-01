using System;
using UnityEngine.Serialization;

// Additive values only. Percentage bonuses are percentage points (5 = +5%).
[Serializable]
public struct StatBonus
{
    public float maxHealth;
    public float damage;
    public float defense;
    public float moveSpeed;
    public float critChance;
    public float critDamageMultiplier;
    [FormerlySerializedAs("dodge")] public float effectResistance;
    [FormerlySerializedAs("accuracy")] public float effectHit;

    public static StatBonus operator +(StatBonus left, StatBonus right) => new StatBonus
    {
        maxHealth = left.maxHealth + right.maxHealth,
        damage = left.damage + right.damage,
        defense = left.defense + right.defense,
        moveSpeed = left.moveSpeed + right.moveSpeed,
        critChance = left.critChance + right.critChance,
        critDamageMultiplier = left.critDamageMultiplier + right.critDamageMultiplier,
        effectResistance = left.effectResistance + right.effectResistance,
        effectHit = left.effectHit + right.effectHit
    };

    public static StatBonus operator *(StatBonus bonus, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return new StatBonus
        {
            maxHealth = bonus.maxHealth * count, damage = bonus.damage * count,
            defense = bonus.defense * count, moveSpeed = bonus.moveSpeed * count,
            critChance = bonus.critChance * count,
            critDamageMultiplier = bonus.critDamageMultiplier * count,
            effectResistance = bonus.effectResistance * count, effectHit = bonus.effectHit * count
        };
    }

    // Summation does not clamp. Runtime limits the final result after combining all sources.
    public BaseStats ApplyTo(BaseStats source)
    {
        source.maxHealth += maxHealth;
        source.damage += damage;
        source.defense += defense;
        source.moveSpeed += moveSpeed;
        source.critChance += critChance;
        source.critDamageMultiplier += critDamageMultiplier;
        source.effectResistance += effectResistance;
        source.effectHit += effectHit;
        return source;
    }
}
