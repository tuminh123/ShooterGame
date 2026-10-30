using System;
using UnityEngine;

public static class ShotGeometry
{
    public static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
        return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
    }
}
[Serializable]
public sealed class StraightShootingPattern : IShootingPattern
{
    public Vector2 GetDirection(ShootingPatternDataSO data, ShootingRuntime runtime, Vector2 aim, int index)
    {
        float spread = data.ProjectileCount == 1 ? 0f :
            -data.SpreadAngle * 0.5f + data.SpreadAngle * index / (data.ProjectileCount - 1);
        return ShotGeometry.Rotate(aim, data.StartAngle + spread);
    }
}
[Serializable]
public sealed class CircularShootingPattern : IShootingPattern
{
    public Vector2 GetDirection(ShootingPatternDataSO data, ShootingRuntime runtime, Vector2 aim, int index)
        => ShotGeometry.Rotate(aim, data.StartAngle + 360f * index / data.ProjectileCount);
}
[Serializable]
public sealed class SpiralShootingPattern : IShootingPattern
{
    public Vector2 GetDirection(ShootingPatternDataSO data, ShootingRuntime runtime, Vector2 aim, int index)
        => ShotGeometry.Rotate(aim, data.StartAngle + runtime.Phase + 360f * index / data.ProjectileCount);
}
