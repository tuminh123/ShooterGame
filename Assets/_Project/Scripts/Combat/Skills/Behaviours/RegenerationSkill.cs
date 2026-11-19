using System;
using UnityEngine;

/// <summary>Hành vi hồi máu: mỗi lần hết cooldown thì hồi một lượng HP cố định cho chủ nhân.</summary>
/// <remarks>Data: <see cref="RegenerationData"/>.</remarks>
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
        if (!Owner.CanRun || data == null || data.HealAmount <= 0f) return false;
        Owner.Owner.ChangeHealth(data.HealAmount);
        return true;
    }

    #endregion
}
