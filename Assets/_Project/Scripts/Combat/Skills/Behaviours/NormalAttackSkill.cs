using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Hành vi đánh thường: đánh mục tiêu gần nhất đang chạm hitbox, rồi áp effect nếu đòn trúng.</summary>
/// <remarks>Data: <see cref="NormalAttackData"/>.</remarks>
public sealed class NormalAttackSkill : Skill
{
    #region Runtime State

    private NormalAttackData data; // Cấu hình cấp hiện tại.
    private IGameplayEffect[] effects; // Snapshot effect riêng của hành vi.
    public override SkillBehaviourType Type => SkillBehaviourType.NormalAttack;

    #endregion

    #region Configuration And Execution

    // Gắn chủ mới hoặc trả pool: bỏ data cũ. Mảng effect được giữ để lượt sau cùng cấu hình dùng lại.
    protected override void InitializeRuntime()
    {
        data = null;
        if (effects == null) effects = Array.Empty<IGameplayEffect>();
    }

    // Cập nhật cấu hình mà không tạo lại runtime.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as NormalAttackData ?? throw new ArgumentException("NormalAttackSkill requires NormalAttackData.");
        data.Validate();
        effects = SnapshotEffects(effects, data.Effects);
    }

    // Chỉ tiêu cooldown khi hitbox chạm và mục tiêu nhận đòn.
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null) return false;
        CharacterHandle target = Owner.Owner.Search.FindNearest(Owner.Owner.HitPosition, data.Range,
            CharacterSearch.OpponentOf(Owner.Identity.Team), Owner.Owner);
        if (!target.TryGetCharacter(out CharacterView victim) || !CharacterSearch.Overlaps(Owner.Owner, victim)) return false;
        DamageResult hit = Owner.Owner.DealDamage(target);
        if (hit.Accepted && Services.Effects != null && Owner.CanRun)
            foreach (IGameplayEffect effect in effects)
            {
                if (!Owner.CanRun) break;
                Services.Effects.Apply(effect, target, Owner.Identity.Team, Owner.Identity.FinalStats.effectHit);
            }
        return hit.Accepted;
    }

    #endregion
}
