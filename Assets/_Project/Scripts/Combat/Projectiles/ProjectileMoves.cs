using System;
using UnityEngine;

// Các quỹ đạo đạn (strategy của IProjectileMove). Asset chọn quỹ đạo qua [SerializeReference].
// KHÔNG đổi tên class trong file này: Unity lưu tên class trong asset skill.
// Các quỹ đạo không có trạng thái, nên mọi viên đạn dùng chung instance trong asset.

/// <summary>Bay thẳng theo hướng lúc được bắn ra.</summary>
[Serializable]
public sealed class StraightProjectileMove : IProjectileMove
{
    /// <inheritdoc/>
    public bool TracksTarget => false;

    /// <inheritdoc/>
    public Vector2 GetDirection(Vector2 currentPosition, Vector2 currentDirection, CharacterHandle homingTarget)
        => currentDirection;
}

/// <summary>Luôn bay thẳng về phía mục tiêu. Mục tiêu chết hoặc về pool thì giữ hướng cuối cùng.</summary>
[Serializable]
public sealed class HomingProjectileMove : IProjectileMove
{
    /// <inheritdoc/>
    public bool TracksTarget => true;

    /// <inheritdoc/>
    public Vector2 GetDirection(Vector2 currentPosition, Vector2 currentDirection, CharacterHandle homingTarget)
    {
        if (!homingTarget.TryGetCharacter(out CharacterView target)) return currentDirection;

        Vector2 toTarget = (Vector2)target.HitPosition - currentPosition;
        // Đạn đã nằm đúng tâm mục tiêu thì không chuẩn hóa được vector 0; giữ hướng cũ.
        return toTarget.sqrMagnitude > 0.000001f ? toTarget.normalized : currentDirection;
    }
}
