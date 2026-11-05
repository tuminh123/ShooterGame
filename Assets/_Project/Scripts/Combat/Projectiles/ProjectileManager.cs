using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Zenject;

// Một vòng cập nhật chung cho đạn; MovementManager vẫn là nơi duy nhất đổi vị trí.
public sealed class ProjectileManager : ITickable, IDisposable
{
    private readonly struct Registration
    {
        public readonly ProjectileView Projectile; // Viên đạn đang hoạt động.
        public readonly MovementHandle Handle; // Phân biệt các lần spawn của cùng object trong pool.

        public Registration(ProjectileView projectile, MovementHandle handle)
        {
            Projectile = projectile;
            Handle = handle;
        }
    }

    private readonly MovementManager movementManager; // Kiểm tra đăng ký còn thuộc lượt spawn hiện tại.
    private readonly ISpatialManager spatial;
    private readonly ITimeSource timeSource;
    private NativeArray<SpatialQuery> queries;
    private NativeArray<SpatialHit> results;
    private readonly List<Registration> activeProjectiles = new List<Registration>(128); // Đạn đang sống.
    private readonly Dictionary<long, int> indices = new Dictionary<long, int>(128); // Thu hồi O(1), tránh quét lại cả đàn đạn mỗi lần despawn.
    private readonly List<Registration> tickSnapshot = new List<Registration>(128); // Bản duyệt ổn định khi callback spawn/despawn.
    private bool disposed; // Ngăn cập nhật sau khi scene giải phóng manager.

    public ProjectileManager(MovementManager movementManager, ISpatialManager spatial, ITimeSource timeSource)
    {
        this.movementManager = movementManager ?? throw new ArgumentNullException(nameof(movementManager));
        this.spatial = spatial;
        this.timeSource = timeSource;
        queries = new NativeArray<SpatialQuery>(128, Allocator.Persistent);
        results = new NativeArray<SpatialHit>(128, Allocator.Persistent);
    }

    internal void Register(ProjectileView projectile, MovementHandle handle)
    {
        if (disposed) throw new ObjectDisposedException(nameof(ProjectileManager));
        indices.Add(handle.Id, activeProjectiles.Count);
        activeProjectiles.Add(new Registration(projectile, handle));
    }

    internal void Unregister(MovementHandle handle)
    {
        if (!ReferenceEquals(handle.Owner, movementManager) || !indices.TryGetValue(handle.Id, out int index)) return;
        indices.Remove(handle.Id);
        int last = activeProjectiles.Count - 1;
        if (index != last)
        {
            Registration moved = activeProjectiles[last];
            activeProjectiles[index] = moved;
            indices[moved.Handle.Id] = index;
        }
        activeProjectiles.RemoveAt(last);
    }

    public void Tick()
    {
        float deltaTime = timeSource.DeltaTime;
        if (disposed || deltaTime <= 0f) return;

        // Đạn tạo trong callback sẽ được xử lý từ lượt kế tiếp, không chen vào bản đang duyệt.
        tickSnapshot.Clear();
        tickSnapshot.AddRange(activeProjectiles);
        EnsureCapacity(tickSnapshot.Count);
        for (int index = 0; index < tickSnapshot.Count; index++)
        {
            queries[index] = default;
            Registration registration = tickSnapshot[index];
            if (!movementManager.IsRegistered(registration.Handle) ||
                registration.Projectile == null || !registration.Projectile.isActiveAndEnabled) continue;
            try
            {
                if (registration.Projectile.PrepareQuery(deltaTime, out SpatialQuery query)) queries[index] = query;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, registration.Projectile);
                // Không thu hồi nhầm object nếu callback đã trả nó về pool và spawn lại.
                if (movementManager.IsRegistered(registration.Handle)) registration.Projectile.Release();
            }
        }
        spatial.ExecuteQueries(queries, results, tickSnapshot.Count);
        for (int index = 0; index < tickSnapshot.Count; index++)
        {
            Registration registration = tickSnapshot[index];
            if (queries[index].Type == SpatialQueryType.None || !movementManager.IsRegistered(registration.Handle) ||
                registration.Projectile == null || !registration.Projectile.isActiveAndEnabled) continue;
            try
            {
                SpatialHit hit = results[index];
                // Đạn trước có thể vừa giết mục tiêu chung. Quét lại đường này để không xuyên mục tiêu phía sau.
                if (!spatial.TryResolve(hit, out CharacterTarget target) && hit.Found != 0)
                    spatial.TryResolve(spatial.Query(queries[index]), out target);
                registration.Projectile.CompleteQuery(target);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, registration.Projectile);
                if (movementManager.IsRegistered(registration.Handle)) registration.Projectile.Release();
            }
        }
        tickSnapshot.Clear();
    }

    private void EnsureCapacity(int count)
    {
        if (queries.Length >= count) return;
        int capacity = Mathf.NextPowerOfTwo(count);
        queries.Dispose();
        results.Dispose();
        queries = new NativeArray<SpatialQuery>(capacity, Allocator.Persistent);
        results = new NativeArray<SpatialHit>(capacity, Allocator.Persistent);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        activeProjectiles.Clear();
        indices.Clear();
        tickSnapshot.Clear();
        queries.Dispose();
        results.Dispose();
    }
}
