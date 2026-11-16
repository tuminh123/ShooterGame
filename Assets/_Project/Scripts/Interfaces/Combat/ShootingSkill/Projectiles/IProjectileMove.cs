using UnityEngine;

#region ProjectileMove

/// <summary>Tính hướng bay; không thay đổi Transform.</summary>
public interface IProjectileMove
{
    // Chỉ quyết định hướng; MovementManager áp dụng tốc độ và đổi vị trí.
    Vector2 GetDirection(Vector2 currentPosition, Vector2 currentDirection, CharacterTarget homingTarget);
}

#endregion
