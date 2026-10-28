using System;
using UnityEngine;
public enum SkillCategory { Active, Passive }
public abstract class SkillDataSO : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private SkillCategory category;
    [SerializeField, Min(0.01f)] private float cooldown = 0.6f;
    [SerializeField] private bool autoCast = true;
    [SerializeField, Min(0.01f)] private float retryInterval = 0.1f;
    [SerializeReference] private ISkill skill;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public SkillCategory Category => category;
    public float Cooldown => cooldown;
    public bool AutoCast => autoCast;
    public float RetryInterval => retryInterval;
    public Type StrategyType => skill?.GetType();
    public virtual void Validate()
    {
        if (skill == null || !Enum.IsDefined(typeof(SkillCategory), category) ||
            !Positive(cooldown) || !Positive(retryInterval))
            throw new InvalidOperationException($"Invalid skill configuration: {name}.");
    }
    protected static bool Positive(float v) => v > 0f && !float.IsInfinity(v);
}
