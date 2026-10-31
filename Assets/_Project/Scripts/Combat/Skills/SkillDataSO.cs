using System;
using UnityEngine;
public enum SkillCategory { Active, Passive }
[CreateAssetMenu(menuName = "Shooter Game/Skills/Skill")]
public sealed class SkillDataSO : ScriptableObject
{
    [SerializeField] private SkillData data = new SkillData { level = 1, maxLevel = 5, cooldown = 0.8f };
    [SerializeField] private SkillVisual visual = new SkillVisual();
    [SerializeReference] private ISkill behavior;
    public SkillData Data => data;
    public SkillVisual Visual => visual;
    public ISkill Behavior => behavior;

    public void Validate()
    {
        data.Validate();
        if (behavior == null || visual == null)
            throw new InvalidOperationException($"Assign skill behavior and visual: {name}.");
    }
}
