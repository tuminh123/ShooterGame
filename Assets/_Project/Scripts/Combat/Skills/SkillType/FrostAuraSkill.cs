using System;
using UnityEngine;

[Serializable]
public sealed class FrostAuraSkill : IRunLevelSkillData
{
    [SerializeField] private SkillAreaVisual visualPrefab;
    [SerializeField, Min(0.01f)] private float initialRadius = 1f;
    [SerializeField, Min(0.01f)] private float radiusPerLevel = 1f;
    [SerializeField, Range(0f, 1f)] private float slowFraction = 0.3f;

    public SkillAreaVisual VisualPrefab => visualPrefab;
    public float SlowFraction => slowFraction;

    public float RadiusAt(int level)
    {
        if (level < 1 || level > 5) throw new ArgumentOutOfRangeException(nameof(level));
        return initialRadius + radiusPerLevel * (level - 1);
    }

    public void Validate()
    {
        if (visualPrefab == null || !(initialRadius > 0f) || float.IsInfinity(initialRadius) ||
            !(radiusPerLevel > 0f) || float.IsInfinity(RadiusAt(5)) ||
            float.IsNaN(slowFraction) || slowFraction < 0f || slowFraction > 1f)
            throw new InvalidOperationException("Frost Aura needs a visual, finite radii and slow between zero and one.");
    }
}
