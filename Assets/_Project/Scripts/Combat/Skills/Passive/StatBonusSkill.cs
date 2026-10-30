using System;
[Serializable]
public sealed class StatBonusSkill : ISkill
{
    public SkillExecution Execute(SkillContext context, SkillRuntime runtime)
    {
        var data = (StatBonusSkillDataSO)runtime.Data;
        return new SkillExecution(true, context.Owner.RuntimeData.AddTemporaryBonus(data.Bonus));
    }
}
