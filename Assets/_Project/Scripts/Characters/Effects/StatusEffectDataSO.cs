using System;
using UnityEngine;

public abstract class StatusEffectDataSO : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField, Range(0f, 1f)] private float applicationChance = 1f;
    [SerializeField] private bool useAccuracyAndDodge = true;
    public float Duration => duration;
    public float ApplicationChance => applicationChance;
    public bool UseAccuracyAndDodge => useAccuracyAndDodge;
    public virtual void Validate()
    {
        if (!(duration > 0f) || float.IsInfinity(duration) || float.IsNaN(applicationChance) ||
            applicationChance < 0f || applicationChance > 1f)
            throw new InvalidOperationException($"Invalid status effect: {name}.");
    }
}
