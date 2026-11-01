using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Skills/Skill")]
public sealed class SkillDataSO : ScriptableObject
{
    [Header("Configuration")]
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(1)] private int maxLevel = 5;
    [SerializeField, Min(0f)] private float cooldown = 0.8f;

    [Header("Display")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;

    [SerializeReference] private ISkill behavior;

    public int Level => level;
    public int MaxLevel => maxLevel;
    public float Cooldown => cooldown;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    // This is a shared configuration template, never registered directly with SkillManager.
    public ISkill Behavior => behavior;

    public void Validate()
    {
        if (level < 1 || maxLevel < level || float.IsNaN(cooldown) ||
            float.IsInfinity(cooldown) || cooldown < 0f)
            throw new InvalidOperationException($"Invalid skill level or cooldown: {name}.");
        if (behavior == null)
            throw new InvalidOperationException($"Assign skill behavior: {name}.");
        if (behavior is ShootingSkill shot)
        {
            if (shot.Shooting == null) throw new InvalidOperationException($"Assign shooting data: {name}.");
            shot.Shooting.Validate();
        }
        if (behavior is RegenerationSkill heal && !Positive(heal.HealAmount))
            throw new InvalidOperationException($"Invalid healing amount: {name}.");
        if (behavior is EmergencyShieldSkill shield &&
            (!Positive(shield.ShieldAmount) || float.IsNaN(shield.HealthThreshold) ||
             shield.HealthThreshold < 0f || shield.HealthThreshold > 100f))
            throw new InvalidOperationException($"Invalid emergency shield configuration: {name}.");
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
}
