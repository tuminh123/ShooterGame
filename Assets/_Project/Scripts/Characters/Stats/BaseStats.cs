using System;
using UnityEngine;
using UnityEngine.Serialization;

// All percentage stats use 100 = 100%. Crit damage is the additional percentage.
[Serializable]
public struct BaseStats
{
    [Min(1f)] public float maxHealth;
    [Min(0f)] public float damage;
    [Min(0f)] public float defense; // Consumable shield, not damage reduction.
    [Min(0f)] public float moveSpeed;
    [Min(0f)] public float range;
    [Min(0f)] public float critChance;
    [Min(0f)] public float critDamageMultiplier;
    [FormerlySerializedAs("dodge"), Min(0f)] public float effectResistance;
    [FormerlySerializedAs("accuracy"), Min(0f)] public float effectHit;

    public static BaseStats Default => new BaseStats
    {
        maxHealth = 100f, damage = 10f, moveSpeed = 3f, range = 2f,
        critChance = 5f, critDamageMultiplier = 50f
    };

    public void Validate()
    {
        if (!Finite(maxHealth) || !Finite(damage) || !Finite(defense) || !Finite(moveSpeed) ||
            !Finite(range) || !Finite(critChance) || !Finite(critDamageMultiplier) ||
            !Finite(effectResistance) || !Finite(effectHit))
            throw new ArgumentException("Character stats must be finite.");
        if (maxHealth < 1f || damage < 0f || defense < 0f || moveSpeed < 0f || range < 0f ||
            critChance < 0f || critDamageMultiplier < 0f || effectResistance < 0f || effectHit < 0f)
            throw new ArgumentException("Character base stats must be non-negative, with at least one max HP.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
