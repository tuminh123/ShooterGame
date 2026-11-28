using System;
using UnityEngine;

// Các quỹ đạo đạn (strategy của IProjectileMove). Asset chọn quỹ đạo qua [SerializeReference].
// KHÔNG đổi tên class hoặc field [SerializeField] trong file này: Unity lưu tên class và field trong asset skill.
// Quỹ đạo không có trạng thái (state nằm trong ProjectileMoveState của từng viên), nên mọi viên dùng chung instance trong asset.

/// <summary>Phần dùng chung của các quỹ đạo.</summary>
internal static class ProjectileMoveMath
{
    /// <summary>Hướng tới một điểm; trùng điểm thì giữ hướng cũ.</summary>
    public static Vector2 Toward(Vector2 from, Vector2 to, Vector2 fallback)
    {
        Vector2 offset = to - from;
        return offset.sqrMagnitude > 0.000001f ? offset.normalized : fallback;
    }

    /// <summary>Đã tới gần người bắn (bay về tay xong).</summary>
    public static bool ReachedOwner(in ProjectileMoveState state) =>
        state.HasOwner && (state.OwnerPosition - state.Position).sqrMagnitude < 0.08f * 0.08f;

    /// <summary>Bay về người bắn; người bắn không còn thì kết thúc.</summary>
    public static void ReturnToOwner(ref ProjectileMoveState state)
    {
        if (!state.HasOwner || ReachedOwner(state)) { state.Finished = true; return; }
        state.Direction = Toward(state.Position, state.OwnerPosition, state.Direction);
    }
}

/// <summary>Bay thẳng theo hướng lúc được bắn ra.</summary>
[Serializable]
public sealed class StraightProjectileMove : IProjectileMove
{
    public bool TracksTarget => false;
    public void Advance(ref ProjectileMoveState state) { }
    public bool OnHit(ref ProjectileMoveState state, CharacterHandle hitTarget, ICharacterSearch search, CharacterTeam opponents,
        Func<CharacterHandle, bool> alreadyHit) => false;
    public void Validate() { }
}

/// <summary>Luôn bay thẳng về phía mục tiêu. Mục tiêu chết hoặc về pool thì giữ hướng cuối cùng.</summary>
[Serializable]
public sealed class HomingProjectileMove : IProjectileMove
{
    public bool TracksTarget => true;

    public void Advance(ref ProjectileMoveState state)
    {
        if (state.Target.TryGetCharacter(out CharacterView target))
            state.Direction = ProjectileMoveMath.Toward(state.Position, target.HitPosition, state.Direction);
    }

    public bool OnHit(ref ProjectileMoveState state, CharacterHandle hitTarget, ICharacterSearch search, CharacterTeam opponents,
        Func<CharacterHandle, bool> alreadyHit) => false;
    public void Validate() { }
}

/// <summary>
/// Bay ra tới hết quãng (tầm chiến đấu) rồi quay về tay người bắn; trúng được cả lượt đi lẫn lượt về.
/// Về tới tay thì kết thúc (chạy tác động "khi hết đường bay").
/// </summary>
[Serializable]
public sealed class BoomerangMove : IProjectileMove
{
    public bool TracksTarget => false;

    public void Advance(ref ProjectileMoveState state)
    {
        if (!state.Returning && state.Travelled >= state.MaxDistance) state.Returning = true;
        if (state.Returning) ProjectileMoveMath.ReturnToOwner(ref state);
    }

    public bool OnHit(ref ProjectileMoveState state, CharacterHandle hitTarget, ICharacterSearch search, CharacterTeam opponents,
        Func<CharacterHandle, bool> alreadyHit) => false;
    public void Validate() { }
}

/// <summary>Nảy ngược lại khi chạm mép màn hình (camera chính), tối đa N lần; hết lượt nảy thì bay thẳng ra ngoài.</summary>
[Serializable]
public sealed class BounceInCameraMove : IProjectileMove
{
    [Tooltip("Số lần nảy tối đa; -1 = vô hạn (giới hạn bằng thời gian tồn tại).")]
    [SerializeField, Min(-1)] private int maxBounces = 3;
    [Tooltip("Lùi vào trong mép màn hình (world unit) để đạn nảy trước khi khuất.")]
    [SerializeField, Min(0f)] private float inset = 0.05f;

    public bool TracksTarget => false;

    public void Advance(ref ProjectileMoveState state)
    {
        if (maxBounces >= 0 && state.Bounces >= maxBounces) return;
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic) return;
        Vector2 center = camera.transform.position;
        float halfHeight = camera.orthographicSize - inset, halfWidth = camera.orthographicSize * camera.aspect - inset;
        Vector2 local = state.Position - center;
        Vector2 direction = state.Direction;
        bool bounced = false;
        if ((local.x > halfWidth && direction.x > 0f) || (local.x < -halfWidth && direction.x < 0f)) { direction.x = -direction.x; bounced = true; }
        if ((local.y > halfHeight && direction.y > 0f) || (local.y < -halfHeight && direction.y < 0f)) { direction.y = -direction.y; bounced = true; }
        if (!bounced) return;
        state.Direction = direction.normalized;
        state.Bounces++;
    }

    public bool OnHit(ref ProjectileMoveState state, CharacterHandle hitTarget, ICharacterSearch search, CharacterTeam opponents,
        Func<CharacterHandle, bool> alreadyHit) => false;

    public void Validate()
    {
        if (maxBounces < -1 || float.IsNaN(inset)) throw new InvalidOperationException("Invalid bounce move.");
    }
}

/// <summary>
/// Đuổi mục tiêu; trúng thì nảy sang đối thủ gần nhất chưa trúng (trong tầm nảy), tối đa N lần, rồi bay về tay (khiên ném).
/// </summary>
[Serializable]
public sealed class RicochetMove : IProjectileMove
{
    [Tooltip("Số lần nảy sang mục tiêu khác sau lần trúng đầu.")][SerializeField, Min(0)] private int bounces = 2;
    [Tooltip("Tầm tìm mục tiêu kế tiếp tính từ chỗ vừa trúng (world unit).")][SerializeField, Min(0.05f)] private float bounceRange = 0.5f;
    [Tooltip("Hết lượt nảy thì bay về tay người bắn.")][SerializeField] private bool returnToOwner = true;

    private static CharacterHandle[] candidates = new CharacterHandle[32]; // Chỉ sống trong một lời gọi OnHit (main thread).

    public bool TracksTarget => true;

    public void Advance(ref ProjectileMoveState state)
    {
        if (state.Returning) { ProjectileMoveMath.ReturnToOwner(ref state); return; }
        if (state.Target.TryGetCharacter(out CharacterView target))
            state.Direction = ProjectileMoveMath.Toward(state.Position, target.HitPosition, state.Direction);
        else if (returnToOwner && state.Bounces > 0) state.Returning = true; // Mục tiêu nảy tới đã chết: quay về.
    }

    public bool OnHit(ref ProjectileMoveState state, CharacterHandle hitTarget, ICharacterSearch search, CharacterTeam opponents,
        Func<CharacterHandle, bool> alreadyHit)
    {
        if (state.Returning) return true; // Lượt về xuyên qua mọi thứ.
        if (state.Bounces < bounces && search != null)
        {
            int found = search.FindAll(state.Position, bounceRange, opponents, ref candidates);
            CharacterHandle best = default;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < found; index++)
            {
                CharacterHandle candidate = candidates[index];
                if (candidate.Equals(hitTarget) || alreadyHit(candidate) || !candidate.TryGetCharacter(out CharacterView view)) continue;
                float distance = ((Vector2)view.HitPosition - state.Position).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            Array.Clear(candidates, 0, found);
            if (best.IsAlive)
            {
                state.Target = best;
                state.Bounces++;
                return true;
            }
        }
        if (!returnToOwner) return false;
        state.Returning = true;
        return true;
    }

    public void Validate()
    {
        if (bounces < 0 || !(bounceRange > 0f)) throw new InvalidOperationException("Invalid ricochet move.");
    }
}
