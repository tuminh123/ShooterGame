using System;
using System.Buffers;
using UnityEngine;

// * Kết quả chỉ tồn tại trong phạm vi using trên main thread; không giữ qua frame/await.
// ! Không sao chép biến kết quả hoặc Dispose hai bản sao: buffer chỉ có một chủ sở hữu.
// * ref struct không tạo object quản lý kết quả trên managed heap mỗi lần quét.
public ref struct CharacterSearchResult
{
    private Collider2D[] hits;
    private CharacterRuntimeData[] identities;
    public int Count { get; private set; }

    internal CharacterSearchResult(Collider2D[] hits, CharacterRuntimeData[] identities, int count)
    {
        this.hits = hits;
        this.identities = identities;
        Count = count;
    }

    public CharacterTarget this[int index]
    {
        get
        {
            if (hits == null || index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            CharacterView view = null;
            if (hits[index] != null)
                hits[index].TryGetComponent(out view);
            return new CharacterTarget(view, identities[index]);
        }
    }

    public void Dispose()
    {
        if (hits == null)
            return;
        ArrayPool<Collider2D>.Shared.Return(hits, clearArray: true);
        ArrayPool<CharacterRuntimeData>.Shared.Return(identities, clearArray: true);
        hits = null;
        identities = null;
        Count = 0;
    }
}

// * Truy vấn hình học dùng chung; không gây damage, không chạy AI hoặc spawn VFX.
// * Mỗi Character có một hitbox trên vỏ gốc. Visual con không nằm trên layer gameplay.
public static class CharacterSearch
{
    private static int lastSyncFrame = -1;
    private static bool physicsDirty = true;

    // * Spawn/despawn hoặc hệ di chuyển gọi khi thay đổi hình học trong cùng frame.
    public static void InvalidatePhysics() => physicsDirty = true;

    // * Dùng chung cho query vùng và kiểm tra khoảng cách giữa hai hitbox đã biết.
    // * Collider di chuyển bằng Transform cần được đồng bộ trước khi đọc hình học physics.
    public static void SynchronizePhysics()
    {
        if (!physicsDirty && lastSyncFrame == Time.frameCount)
            return;
        Physics2D.SyncTransforms();
        physicsDirty = false;
        lastSyncFrame = Time.frameCount;
    }

    // Known pair: test their hitboxes directly rather than querying the whole layer.
    public static bool Overlaps(CharacterView first, CharacterView second)
    {
        if (first == null || second == null || first == second ||
            !first.isActiveAndEnabled || !second.isActiveAndEnabled ||
            first.RuntimeData == null || second.RuntimeData == null ||
            !first.RuntimeData.IsAlive || !second.RuntimeData.IsAlive)
            return false;
        Collider2D firstHitbox = first.Hitbox;
        Collider2D secondHitbox = second.Hitbox;
        if (firstHitbox == null || secondHitbox == null || !firstHitbox.enabled || !secondHitbox.enabled)
            return false;
        SynchronizePhysics();
        if (!firstHitbox.bounds.Intersects(secondHitbox.bounds))
            return false;
        ColliderDistance2D contact = firstHitbox.Distance(secondHitbox);
        return contact.isValid && contact.distance <= 0f;
    }

    public static CharacterTeam OpponentOf(CharacterTeam team)
    {
        if (team == CharacterTeam.Player) return CharacterTeam.Enemy;
        if (team == CharacterTeam.Enemy) return CharacterTeam.Player;
        throw new ArgumentOutOfRangeException(nameof(team));
    }

    public static CharacterTarget FindNearest(Vector2 center, float radius,
        CharacterTeam targetTeam, CharacterView exclude = null)
    {
        using (var results = FindAll(center, radius, targetTeam, exclude))
        {
            CharacterTarget nearest = default;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < results.Count; i++)
            {
                CharacterTarget candidate = results[i];
                if (!candidate.TryGetCharacter(out CharacterView view))
                    continue;
                float distance = ((Vector2)view.HitPosition - center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = candidate;
                }
            }
            return nearest;
        }
    }

    // Sweep the whole movement segment, including fast bullets and long frames.
    public static CharacterTarget FindFirstOnPath(Vector2 origin, float radius, Vector2 direction,
        float distance, CharacterTeam targetTeam, out Vector2 hitPosition)
    {
        hitPosition = origin;
        ContactFilter2D filter = TargetFilter(targetTeam);
        SynchronizePhysics();
        RaycastHit2D[] hits = ArrayPool<RaycastHit2D>.Shared.Rent(16);
        try
        {
            int count;
            while (true)
            {
                count = Physics2D.CircleCast(origin, radius, direction, filter, hits, distance);
                if (count < hits.Length) break;
                RaycastHit2D[] larger = ArrayPool<RaycastHit2D>.Shared.Rent(checked(hits.Length * 2));
                ArrayPool<RaycastHit2D>.Shared.Return(hits, clearArray: true);
                hits = larger;
            }
            CharacterTarget nearest = default;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == null ||
                    !hits[i].collider.TryGetComponent(out CharacterView view) ||
                    view.RuntimeData == null || view.RuntimeData.Team != targetTeam ||
                    !new CharacterTarget(view).TryGetCharacter(out _) || hits[i].distance >= bestDistance)
                    continue;
                bestDistance = hits[i].distance;
                nearest = new CharacterTarget(view);
                hitPosition = hits[i].centroid;
            }
            DrawSearchPath(origin, radius, direction, distance,
                float.IsPositiveInfinity(bestDistance) ? (Vector2?)null : hitPosition);
            return nearest;
        }
        finally { ArrayPool<RaycastHit2D>.Shared.Return(hits, clearArray: true); }
    }

    private static ContactFilter2D TargetFilter(CharacterTeam targetTeam)
    {
        string layer = targetTeam == CharacterTeam.Player ? GameConstants.PhysicsLayers.Player :
            targetTeam == CharacterTeam.Enemy ? GameConstants.PhysicsLayers.Enemy :
            throw new ArgumentOutOfRangeException(nameof(targetTeam));
        int layerIndex = LayerMask.NameToLayer(layer);
        if (layerIndex < 0)
            throw new InvalidOperationException($"Add the {layer} layer in Project Settings.");
        return new ContactFilter2D { useLayerMask = true, layerMask = 1 << layerIndex, useTriggers = true };
    }

    public static CharacterSearchResult FindAll(Vector2 center, float radius,
        CharacterTeam targetTeam, CharacterView exclude = null)
    {
        if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Search geometry must be finite and radius nonnegative.");
        ContactFilter2D filter = TargetFilter(targetTeam);

        // * Gộp đồng bộ Transform; đạn không có collider nên di chuyển đạn không làm bẩn physics.
        SynchronizePhysics();

        Collider2D[] hits = ArrayPool<Collider2D>.Shared.Rent(32);
        CharacterRuntimeData[] identities = null;
        try
        {
            int count;
            while (true)
            {
                count = Physics2D.OverlapCircle(center, radius, filter, hits);
                if (count < hits.Length) break;
                // * Đầy buffer chưa chắc đã đủ kết quả; quét lại trước khi có bất kỳ tác động nào.
                Collider2D[] larger = ArrayPool<Collider2D>.Shared.Rent(checked(hits.Length * 2));
                ArrayPool<Collider2D>.Shared.Return(hits, clearArray: true);
                hits = larger;
            }

            identities = ArrayPool<CharacterRuntimeData>.Shared.Rent(Mathf.Max(1, count));
            int validCount = 0;
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || !hit.TryGetComponent(out CharacterView view) || view == exclude ||
                    !view.isActiveAndEnabled || view.RuntimeData == null ||
                    !view.RuntimeData.IsAlive || view.RuntimeData.Team != targetTeam)
                    continue;
                hits[validCount] = hit;
                // * Chốt danh tính lúc quét để callback damage không đổi kết quả sang cá thể pool mới.
                identities[validCount++] = view.RuntimeData;
            }
            DrawSearchCircle(center, radius);
            return new CharacterSearchResult(hits, identities, validCount);
        }
        catch
        {
            ArrayPool<Collider2D>.Shared.Return(hits, clearArray: true);
            if (identities != null)
                ArrayPool<CharacterRuntimeData>.Shared.Return(identities, clearArray: true);
            throw;
        }
    }

     private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #region Search Gizmos

    // Queries run outside OnDrawGizmos, so draw debug lines visible with Scene Gizmos enabled.
    // Conditional calls (including argument evaluation) are removed from player builds.
    private const float SearchGizmoDuration = 0.1f;
    private const int SearchGizmoSegments = 32;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void DrawSearchCircle(Vector2 center, float radius)
    {
        DrawSearchRing(center, radius, Color.yellow);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void DrawSearchPath(Vector2 origin, float radius, Vector2 direction,
        float distance, Vector2? hitPosition = null)
    {
        Vector2 heading = direction.normalized;
        Vector2 end = origin + heading * distance;
        Vector2 side = new Vector2(-heading.y, heading.x) * radius;
        DrawSearchRing(origin, radius, Color.cyan);
        DrawSearchRing(end, radius, Color.cyan);
        Debug.DrawLine(origin + side, end + side, Color.cyan, SearchGizmoDuration, false);
        Debug.DrawLine(origin - side, end - side, Color.cyan, SearchGizmoDuration, false);
        Debug.DrawLine(origin, end, Color.cyan, SearchGizmoDuration, false);

        if (hitPosition.HasValue)
            DrawSearchRing(hitPosition.Value, radius, Color.red);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void DrawSearchRing(Vector2 center, float radius, Color color)
    {
        Vector2 previous = center + Vector2.right * radius;
        for (int i = 1; i <= SearchGizmoSegments; i++)
        {
            float angle = i * (Mathf.PI * 2f / SearchGizmoSegments);
            Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Debug.DrawLine(previous, next, color, SearchGizmoDuration, false);
            previous = next;
        }
    }

    #endregion

   
}
