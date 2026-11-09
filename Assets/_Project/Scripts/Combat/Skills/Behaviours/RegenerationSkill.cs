using System;
using UnityEngine;

// Hành vi runtime; nhận data theo cấp từ SkillRuntime.
[Serializable]
public sealed class RegenerationSkill : Skill
{
    #region Runtime Configuration

    [NonSerialized] private RegenerationData data; // Data cấp hiện tại.
    public override SkillBehaviourType Type => SkillBehaviourType.Regeneration;

    // Bản clone không dùng cấu hình đang gắn ở instance khác.
    protected override void InitializeRuntime() => data = null;

    // Nhận cấu hình đầy đủ của cấp mới.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        data = configuration as RegenerationData ?? throw new ArgumentException("RegenerationSkill requires RegenerationData.");
        data.Validate();
    }

    #endregion

    #region Execution

    // HP được phép cộng vượt giá trị ban đầu; runtime tăng max/current và thông báo UI trong cùng thao tác.
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null || data.healAmount <= 0f) return false;
        Owner.Owner.ChangeHealth(data.healAmount);
        return true;
    }

    #endregion
}
