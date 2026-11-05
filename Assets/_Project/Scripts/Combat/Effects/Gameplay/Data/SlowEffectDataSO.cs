using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Effects/Slow")]
public sealed class SlowEffectDataSO : StatusEffectDataSO
{
    [SerializeField, Range(0f, 1f)] private float slowFraction = 0.3f;
    public float SlowFraction => slowFraction;
    public override void Validate()
    {
        base.Validate();
        if (float.IsNaN(slowFraction) || slowFraction < 0f || slowFraction > 1f)
            throw new InvalidOperationException($"Invalid slow fraction: {name}.");
    }
}
