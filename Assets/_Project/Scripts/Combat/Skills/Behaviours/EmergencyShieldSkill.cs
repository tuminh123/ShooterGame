using System;
using UnityEngine;

// Hành vi runtime; nhận data theo cấp từ SkillRuntime.
[Serializable]
public sealed class EmergencyShieldSkill : Skill
{
    #region Runtime Configuration

    [NonSerialized] private EmergencyShieldData data; // Data của cấp đang chạy.
    public override SkillBehaviourType Type => SkillBehaviourType.EmergencyShield;

    // Runtime mới không giữ data của bản template.
    protected override void InitializeRuntime() => data = null;

    // Chỉ cập nhật config, không cấp shield ngay khi lên cấp.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as EmergencyShieldData ?? throw new ArgumentException("EmergencyShieldSkill requires EmergencyShieldData.");
        data.Validate();
    }

    #endregion

    #region Execution

    // Dùng cùng API shield của owner; chỉ kích hoạt khi HP đạt ngưỡng.
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null || Owner.Identity.CurrentHealth / Owner.Identity.FinalStats.maxHealth * 100f > data.healthThreshold)
            return false;
        Owner.Owner.GrantShield(data.shieldAmount);
        return true;
    }

    #endregion
}
