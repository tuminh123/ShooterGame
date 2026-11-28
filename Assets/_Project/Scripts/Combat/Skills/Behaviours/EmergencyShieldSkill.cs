using System;
using UnityEngine;

/// <summary>Hành vi khiên khẩn cấp: khi HP xuống dưới ngưỡng phần trăm, cấp một lượng khiên cho chủ nhân.</summary>
/// <remarks>HP còn trên ngưỡng thì không tiêu cooldown. Data: <see cref="EmergencyShieldData"/>.</remarks>
public sealed class EmergencyShieldSkill : Skill
{
    #region Runtime Configuration

    private EmergencyShieldData data; // Data của cấp đang chạy.
    private int triggers; // Số lần đã kích hoạt trong lượt spawn này.
    private IGameplayEffect[] pulseSnapshot = Array.Empty<IGameplayEffect>();
    public override SkillBehaviourType Type => SkillBehaviourType.EmergencyShield;

    // Runtime mới không giữ data của bản template.
    protected override void InitializeRuntime()
    {
        data = null;
        triggers = 0;
    }

    // Chỉ cập nhật config, không cấp shield ngay khi lên cấp.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as EmergencyShieldData ?? throw new ArgumentException("EmergencyShieldSkill requires EmergencyShieldData.");
        data.Validate();
        pulseSnapshot = SnapshotEffects(pulseSnapshot, data.PulseEffects);
    }

    #endregion

    #region Execution

    // Dùng cùng API shield của owner; chỉ kích hoạt khi HP đạt ngưỡng.
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null || Owner.Identity.CurrentHealth / Owner.Identity.FinalStats.maxHealth * 100f > data.HealthThreshold)
            return false;
        if (data.MaxTriggers > 0 && triggers >= data.MaxTriggers) return false;
        triggers++;
        CharacterView self = Owner.Owner;
        if (data.ShieldAmount > 0f) self.GrantShield(data.ShieldAmount);
        if (data.RestorePercent > 0f) self.Restore(Owner.Identity.MaxHealth * data.RestorePercent / 100f);
        if (data.InvulnerableSeconds > 0f) self.GrantInvulnerability(data.InvulnerableSeconds);
        if (data.PulseRadius > 0f)
        {
            Services.Vfx.Spawn(data.PulseVfx, self.HitPosition);
            var hit = new HitPayload(Owner.Handle, Owner.Identity.FinalStats, Owner.Identity.FinalBonuses, Owner.Identity.Team,
                data.PulseDamageMultiplier, pulseSnapshot, data.PulseKnockback);
            SkillImpacts.HitCircle(Services.Hits, self.Search, self.HitPosition, data.PulseRadius, in hit);
        }
        return true;
    }

    #endregion
}
