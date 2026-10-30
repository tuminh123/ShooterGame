using System;
using System.Collections.Generic;
public sealed class SkillFactory
{
    private readonly Dictionary<Type, ISkill> strategies = new Dictionary<Type, ISkill>();
    public SkillFactory()
    { Register(new ProjectileSkill()); Register(new StatBonusSkill()); }
    public void Register(ISkill strategy)
    {
        if (strategy == null) throw new ArgumentNullException(nameof(strategy));
        strategies.Add(strategy.GetType(), strategy);
    }
    public SkillRuntime Create(SkillDataSO data, SkillContext context)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (context == null) throw new ArgumentNullException(nameof(context));
        data.Validate();
        if (!strategies.TryGetValue(data.StrategyType, out ISkill strategy))
            throw new InvalidOperationException($"Unregistered skill strategy: {data.StrategyType}.");
        return new SkillRuntime(data, strategy, context);
    }
}
