using System;
using UnityEngine;
using UnityEngine.Serialization;

// Dữ liệu SO cũ được giữ để không mất asset. Skill mới chọn IGameplayEffect qua SerializeReference.
public abstract class StatusEffectDataSO : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField, Range(0f, 100f)] private float applicationChance = 100f;
    [FormerlySerializedAs("useAccuracyAndDodge")]
    [SerializeField] private bool useEffectStats = true;
    public float Duration => duration;
    public float ApplicationChance => applicationChance;
    public bool UseEffectStats => useEffectStats;
    public virtual void Validate()
    {
        if (!(duration > 0f) || float.IsInfinity(duration) || float.IsNaN(applicationChance) ||
            applicationChance < 0f || applicationChance > 100f)
            throw new InvalidOperationException($"Invalid status effect: {name}.");
    }
}
