using System;
using System.Collections.Generic;
using UnityEngine;

// Hành vi runtime; nhận data theo cấp từ SkillRuntime.
[Serializable]
public sealed class NormalAttackSkill : Skill
{
    #region Runtime State

    [NonSerialized] private NormalAttackData data; // Cấu hình cấp hiện tại.
    [NonSerialized] private IGameplayEffect[] effects; // Snapshot effect riêng của hành vi.
    public override SkillBehaviourType Type => SkillBehaviourType.NormalAttack;

    #endregion

    #region Configuration And Execution

    // Không giữ collection/state của template.
    protected override void InitializeRuntime() { data = null; effects = Array.Empty<IGameplayEffect>(); }

    // Cập nhật cấu hình mà không tạo lại runtime.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as NormalAttackData ?? throw new ArgumentException("NormalAttackSkill requires NormalAttackData.");
        data.Validate();
        effects = data.effects.ToArray();
    }

    // Chỉ tiêu cooldown khi hitbox chạm và mục tiêu nhận đòn.
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null) return false;
        CharacterTarget target = Owner.Owner.Search.FindNearest(Owner.Owner.HitPosition, data.range,
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
