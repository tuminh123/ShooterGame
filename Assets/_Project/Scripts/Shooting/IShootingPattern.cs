using UnityEngine;

// Shot geometry only. No prefab spawning, damage, cooldown or element-specific logic.
public interface IShootingPattern
{
    Vector2 GetDirection(ShootingPatternDataSO data, ShootingRuntime runtime, Vector2 aim, int index);
}
public sealed class ShootingRuntime
{
    public float Phase { get; internal set; }
    public long CastCount { get; internal set; }
}
