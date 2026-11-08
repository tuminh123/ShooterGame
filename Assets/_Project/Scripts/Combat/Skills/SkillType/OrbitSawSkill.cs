using System;
using UnityEngine;

[Serializable]
public sealed class OrbitSawSkill : IRunLevelSkillData
{
    [SerializeField] private SkillAreaVisual visualPrefab;
    [SerializeField, Min(0.01f)] private float orbitRadius = 0.5f;
    [SerializeField, Min(0.01f)] private float bladeRadius = 0.1f;
    [SerializeField, Min(0.01f)] private float degreesPerSecond = 180f;
    [SerializeField, Min(0.01f)] private float hitInterval = 0.5f;

    public SkillAreaVisual VisualPrefab => visualPrefab;
    public float OrbitRadius => orbitRadius;
    public float BladeRadius => bladeRadius;
    public float DegreesPerSecond => degreesPerSecond;
    public float HitInterval => hitInterval;

    public int BladeCountAt(int level)
    {
        if (level < 1 || level > 5) throw new ArgumentOutOfRangeException(nameof(level));
        return level; // Chỉ tăng số lưỡi, không tự tăng damage/crit của nhân vật.
    }

    public void Validate()
    {
        if (visualPrefab == null || !Positive(orbitRadius) || !Positive(bladeRadius) ||
            !Positive(degreesPerSecond) || !Positive(hitInterval))
            throw new InvalidOperationException("Orbit Saw needs a visual prefab and positive finite settings.");
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
}
