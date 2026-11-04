using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Skills/Skill")]
public sealed class SkillDataSO : ScriptableObject
{
    [Header("Configuration")]
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(1)] private int maxLevel = 5;
    [SerializeField, Min(0f)] private float cooldown = 0.8f;
    [SerializeField, Min(0f)] private float range = 2f;

    [Header("Display")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;

    // Keep the serialized field name so existing managed-reference assets retain their data.
    [SerializeReference, InspectorName("Skill Data")] private ISkillData behavior;

    public int Level => level;
    public int MaxLevel => maxLevel;
    public float Cooldown => cooldown;
    public float Range => range;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    // This is a shared configuration template, never registered directly with SkillManager.
    public ISkillData Data => behavior;

    public void Validate()
    {
        if (level < 1 || maxLevel < level || float.IsNaN(cooldown) ||
            float.IsInfinity(cooldown) || cooldown < 0f)
            throw new InvalidOperationException($"Invalid skill level or cooldown: {name}.");
        if (float.IsNaN(range) || float.IsInfinity(range) || range < 0f)
            throw new InvalidOperationException($"Invalid skill range: {name}.");
        if (behavior == null)
            throw new InvalidOperationException($"Assign skill behavior: {name}.");
        if (behavior is ShootingSkill shootingData)
            shootingData.Validate();
        if (behavior is RegenerationSkill regenerationData && !IsPositiveFiniteNumber(regenerationData.HealAmount))
            throw new InvalidOperationException($"Invalid healing amount: {name}.");
        if (behavior is EmergencyShieldSkill shieldData &&
            (!IsPositiveFiniteNumber(shieldData.ShieldAmount) || float.IsNaN(shieldData.HealthThreshold) ||
             shieldData.HealthThreshold < 0f || shieldData.HealthThreshold > 100f))
            throw new InvalidOperationException($"Invalid emergency shield configuration: {name}.");
    }

    // Hồi máu/cấp khiên cần lượng hữu hạn lớn hơn 0; phép so sánh > 0 cũng loại NaN.
    private static bool IsPositiveFiniteNumber(float value) => value > 0f && !float.IsInfinity(value);
}
