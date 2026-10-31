using System;
using UnityEngine;

[Serializable]
public struct SkillData
{
    [Min(1)] public int level;
    [Min(1)] public int maxLevel;
    [Min(0f)] public float cooldown;

    public void Validate()
    {
        if (level < 1 || maxLevel < level || float.IsNaN(cooldown) ||
            float.IsInfinity(cooldown) || cooldown < 0f)
            throw new InvalidOperationException("Invalid skill level or cooldown.");
    }
}
