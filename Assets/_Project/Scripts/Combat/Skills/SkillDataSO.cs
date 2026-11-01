using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Skills/Skill")]
public sealed class SkillDataSO : ScriptableObject
{
    [SerializeField] private SkillData data = new SkillData { level = 1, maxLevel = 5, cooldown = 0.8f };
    [SerializeField] private SkillVisual visual = new SkillVisual();
    [SerializeReference] private ISkill behavior;
    public SkillData Data => data;
    public SkillVisual Visual => visual;
    // This is a shared configuration template, never registered directly with SkillManager.
    public ISkill Behavior => behavior;

    public void Validate()
    {
        data.Validate();
        if (behavior == null || visual == null)
            throw new InvalidOperationException($"Assign skill behavior and visual: {name}.");
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
