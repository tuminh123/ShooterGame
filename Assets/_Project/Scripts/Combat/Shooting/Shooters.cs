using UnityEngine;

public sealed class ShooterTypeAttribute : PropertyAttribute { }

public interface IShooter
{
    int Shoot(ShootingSkill skill, Vector2 position, Vector2 aim, CharacterTarget target);
}

public sealed class StraightShooter : IShooter
{
    public int Shoot(ShootingSkill skill, Vector2 position, Vector2 aim, CharacterTarget target)
    {
        int fired = 0;
        for (int i = 0; i < skill.ProjectileCount; i++)
        {
            float spread = skill.ProjectileCount == 1 ? 0f :
                -skill.SpreadAngle * 0.5f + skill.SpreadAngle * i / (skill.ProjectileCount - 1);
            if (skill.SpawnBullet(position, ShotGeometry.Rotate(aim, skill.StartAngle + spread), target)) fired++;
        }
        return fired;
    }
}

public sealed class CircularShooter : IShooter
{
    public int Shoot(ShootingSkill skill, Vector2 position, Vector2 aim, CharacterTarget target)
    {
        int fired = 0;
        for (int i = 0; i < skill.ProjectileCount; i++)
        {
            float angle = skill.StartAngle + 360f * i / skill.ProjectileCount;
            if (skill.SpawnBullet(position, ShotGeometry.Rotate(aim, angle), target)) fired++;
        }
        return fired;
    }
}

public sealed class SpiralShooter : IShooter
{
    public int Shoot(ShootingSkill skill, Vector2 position, Vector2 aim, CharacterTarget target)
    {
        int fired = 0;
        for (int i = 0; i < skill.ProjectileCount; i++)
        {
            float angle = skill.StartAngle + skill.Phase + 360f * i / skill.ProjectileCount;
            if (skill.SpawnBullet(position, ShotGeometry.Rotate(aim, angle), target)) fired++;
        }
        return fired;
    }
}

internal static class ShotGeometry
{
    public static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
        return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
    }
}
