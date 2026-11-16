using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectileMoveFactory : IProjectileMoveFactory, IProjectileMoveSelection
{

    #region Validation
    public static void ValidateSupported(IProjectileMove movement)
    {
        if (movement == null || !projectileMoves.ContainsKey(movement.GetType()))
            throw new ArgumentException("Unsupported projectile movement.", nameof(movement));
    }

    #endregion

    #region Dependencies And State
    // Chọn strategy trực tiếp từ type được lưu trong SerializeReference.
    // Hai strategy hiện stateless: dùng chung an toàn, không chứa target/hướng của từng viên.
    private static readonly Dictionary<Type, IProjectileMove> projectileMoves = new Dictionary<Type, IProjectileMove>
    {
        [typeof(StraightProjectileMove)] = new StraightProjectileMove(),
        [typeof(HomingProjectileMove)] = new HomingProjectileMove()
    };

    #endregion

    #region Creation

    public IProjectileMove Create(IProjectileMove selectedMovement)
    {
        if (selectedMovement == null) throw new ArgumentNullException(nameof(selectedMovement));
        if (projectileMoves.TryGetValue(selectedMovement.GetType(), out IProjectileMove projectileMove))
            return projectileMove;
        throw new ArgumentException($"Unsupported projectile movement: {selectedMovement.GetType().Name}.", nameof(selectedMovement));
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IProjectileMoveSelection GetProjectileMoveSelection() => this;

    #endregion
}



[Serializable]
public sealed class StraightProjectileMove : IProjectileMove
{

    #region Runtime Operations
    public Vector2 GetDirection(Vector2 currentPosition, Vector2 currentDirection, CharacterTarget homingTarget)
        => currentDirection;

    #endregion
}

[Serializable]
public sealed class HomingProjectileMove : IProjectileMove
{

    #region Runtime Operations
    public Vector2 GetDirection(Vector2 currentPosition, Vector2 currentDirection, CharacterTarget homingTarget)
    {
        if (homingTarget.TryGetCharacter(out CharacterView targetCharacter))
        {
            Vector2 directionToTarget = (Vector2)targetCharacter.HitPosition - currentPosition;
            if (directionToTarget.sqrMagnitude > 0.000001f) return directionToTarget.normalized;
        }
        // Mục tiêu chết hoặc về pool: tiếp tục bay theo hướng cuối cùng.
        return currentDirection;
    }

    #endregion
}
