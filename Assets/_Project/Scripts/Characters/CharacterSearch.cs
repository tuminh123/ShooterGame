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

    public static CharacterSearchResult FindAll(Vector2 center, float radius,
        CharacterTeam targetTeam, CharacterView exclude = null)
    {
        if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Search geometry must be finite and radius nonnegative.");
        string layer = targetTeam == CharacterTeam.Player ? GameConstants.PhysicsLayers.Player :
            targetTeam == CharacterTeam.Enemy ? GameConstants.PhysicsLayers.Enemy :
            throw new ArgumentOutOfRangeException(nameof(targetTeam));
        int layerIndex = LayerMask.NameToLayer(layer);
        if (layerIndex < 0)
            throw new InvalidOperationException($"Add the {layer} layer in Project Settings.");
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = 1 << layerIndex, useTriggers = true };

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
}
