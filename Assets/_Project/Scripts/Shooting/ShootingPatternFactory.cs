using System;
using System.Collections.Generic;

public sealed class ShootingPatternFactory
{
    private readonly Dictionary<Type, IShootingPattern> strategies = new Dictionary<Type, IShootingPattern>();
    public ShootingPatternFactory()
    {
        Register(new StraightShootingPattern());
        Register(new CircularShootingPattern());
        Register(new SpiralShootingPattern());
    }
    public void Register(IShootingPattern strategy)
    {
        if (strategy == null) throw new ArgumentNullException(nameof(strategy));
        strategies.Add(strategy.GetType(), strategy);
    }
    public IShootingPattern Resolve(ShootingPatternDataSO data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.Validate();
        if (!strategies.TryGetValue(data.StrategyType, out IShootingPattern strategy))
            throw new InvalidOperationException($"Unregistered shooting pattern: {data.StrategyType}.");
        return strategy;
    }
}
