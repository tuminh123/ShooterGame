using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// System đạn của miền Combat, sở hữu trọn vòng đời của mọi viên đạn: spawn → bay → quét va chạm → thu hồi.
/// </summary>
/// <remarks>
/// Mỗi frame chỉ có một vòng cập nhật chung, và việc quét va chạm của tất cả các viên được gom vào cùng một job.
/// MovementSystem vẫn là nơi duy nhất đổi vị trí của đạn.
/// Do <see cref="CombatManager"/> tạo và gọi <see cref="Tick"/>; không bind vào Zenject.
/// </remarks>
public sealed class ProjectileSystem : IDisposable
{
    #region Types

    /// <summary>Một viên đang bay, kèm movement handle của đúng lượt bay đó.</summary>
    private readonly struct Registration
    {
        public readonly ProjectileView Projectile;
        public readonly MovementHandle Handle; // Phân biệt các lượt bay của cùng một vỏ trong pool.

        public Registration(ProjectileView projectile, MovementHandle handle)
        {
            Projectile = projectile;
            Handle = handle;
        }
    }

    #endregion

    #region Dependencies

    private readonly IGameObjectPool pools;
    private readonly CombatAssetsSO combatAssets; // Prefab vỏ đạn và catalog BulletType → visual.
    private readonly IMovementRegistration movementManager; // Kiểm tra đăng ký còn thuộc lượt bay hiện tại.
    private readonly ISpatialResolution spatialResolution;
    private readonly ISpatialQuery spatialQuery;
    private readonly ProjectileContext projectileContext; // Truyền vào mọi viên đạn lúc spawn.

    #endregion

    #region Runtime State

    private readonly List<Registration> activeProjectiles = new List<Registration>(128);
    private readonly Dictionary<long, int> indices = new Dictionary<long, int>(128); // handle → index, để gỡ đăng ký O(1).
    private readonly List<Registration> tickSnapshot = new List<Registration>(128); // Bản duyệt ổn định khi callback spawn/despawn.
    private NativeArray<SpatialQuery> queries;
    private NativeArray<SpatialHit> results;

    private const float CameraDespawnMargin = 0.1f; // Đệm ngoài khung hình (world unit), để không mất đạn ngay sát mép.
    private readonly Plane[] cameraPlanes = new Plane[6]; // Tái sử dụng, không tạo mảng mới mỗi frame.
    private bool disposed;

    #endregion

    #region Constructor

    public ProjectileSystem(
        IGameObjectPool pools,
        CombatAssetsSO combatAssets,
        IMovementRegistration movementRegistration,
        IMovementCommand movementCommand,
        ISpatialResolution spatialResolution,
        ISpatialQuery spatialQuery,
        ISpatialQueryBuilder queryBuilder,
        EffectSystem effects,
        CombatVfx vfx)
    {
        this.combatAssets = combatAssets != null ? combatAssets : throw new ArgumentNullException(nameof(combatAssets));
        combatAssets.Validate(); // Kiểm tra trước khi cấp phát NativeArray, để cấu hình sai không gây leak.
        this.pools = pools ?? throw new ArgumentNullException(nameof(pools));
        movementManager = movementRegistration ?? throw new ArgumentNullException(nameof(movementRegistration));
        this.spatialResolution = spatialResolution ?? throw new ArgumentNullException(nameof(spatialResolution));
        this.spatialQuery = spatialQuery ?? throw new ArgumentNullException(nameof(spatialQuery));
        projectileContext = new ProjectileContext(
            movementManager,
            movementCommand ?? throw new ArgumentNullException(nameof(movementCommand)),
            queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder)),
            pools,
            this,
            effects ?? throw new ArgumentNullException(nameof(effects)),
            vfx ?? throw new ArgumentNullException(nameof(vfx)));

        queries = new NativeArray<SpatialQuery>(128, Allocator.Persistent);
        results = new NativeArray<SpatialHit>(128, Allocator.Persistent);
        Camera.onPreCull += DespawnOutsideCamera;
    }

    #endregion

    #region Spawn And Release

    /// <summary>
    /// Tạo một viên đạn. Chỉ tạo đúng một viên; số viên mỗi lượt và cách chia hướng do skill quyết định.
    /// Trả về false nếu pool không tạo được vỏ.
    /// </summary>
    public bool SpawnBullet(ProjectileSpawnData projectileData, Vector2 spawnPosition, Vector2 direction, CharacterHandle target = default)
    {
        projectileData.Validate();
        if (!IsFinite(direction.x) || !IsFinite(direction.y) || direction.sqrMagnitude < 0.000001f)
            throw new ArgumentException("Bullet direction must be finite and nonzero.", nameof(direction));

        CombatAssetsSO.BulletVisual bulletVisual = combatAssets.GetBullet(projectileData.BulletType);
        // Thuê vỏ rồi phóng ngay trong cùng lời gọi: OnEnable của vỏ không làm gì nên không cần lambda beforeActivate
        // (lambda có capture tạo closure + delegate mới cho mỗi viên).
        GameObject shell = pools.Spawn(combatAssets.BulletPrefab.gameObject, spawnPosition, Quaternion.identity, PoolGroup.Bullets);
        if (shell == null) return false;
        shell.transform.localScale = combatAssets.BulletPrefab.transform.localScale;
        var view = shell.GetComponent<ProjectileView>();
        try { view.Launch(projectileContext, bulletVisual, projectileData, direction, target); }
        catch
        {
            // Phóng lỗi thì trả vỏ ngay (visual con trước), không để một vỏ "trống" đang bật trong scene.
            Release(view);
            throw;
        }
        return true;
    }

    /// <summary>Trả một viên đạn về pool: thu hồi visual con trước (khi vỏ còn bật), rồi mới trả vỏ.</summary>
    internal void Release(ProjectileView projectile)
    {
        if (projectile == null) return;
        try { projectile.ClearVisual(); }
        finally { pools.Despawn(projectile.gameObject); }
    }

    /// <summary>Thu hồi mọi viên đang bay. Giữ buffer để trận tiếp theo dùng lại.</summary>
    public void ClearProjectiles()
    {
        foreach (Registration registration in new List<Registration>(activeProjectiles))
            if (registration.Projectile != null && movementManager.IsRegistered(registration.Handle))
                registration.Projectile.Release();
    }

    #endregion

    #region Registration (ProjectileView gọi khi phóng/tắt)

    /// <summary>Đạn tự đăng ký ở cuối <see cref="ProjectileView.Launch"/>, sau khi đã có đủ dữ liệu lượt bay.</summary>
    internal void Register(ProjectileView projectile, MovementHandle handle)
    {
        if (disposed) throw new ObjectDisposedException(nameof(ProjectileSystem));
        indices.Add(handle.Id, activeProjectiles.Count);
        activeProjectiles.Add(new Registration(projectile, handle));
    }

    /// <summary>Đạn tự gỡ đăng ký trong OnDisable. Handle lạ hoặc đã gỡ thì bỏ qua.</summary>
    internal void Unregister(MovementHandle handle)
    {
        // So với handle đã đăng ký (cùng Id, cùng bên cấp), không so với movementManager: handle do MovementSystem
        // bên trong Simulation cấp, còn movementManager ở đây là SimulationManager nên hai reference không bao giờ bằng nhau.
        if (!indices.TryGetValue(handle.Id, out int index) ||
            !ReferenceEquals(activeProjectiles[index].Handle.Owner, handle.Owner)) return;
        indices.Remove(handle.Id);

        // Swap-remove: đưa phần tử cuối vào chỗ trống để không phải dời cả danh sách.
        int last = activeProjectiles.Count - 1;
        if (index != last)
        {
            Registration moved = activeProjectiles[last];
            activeProjectiles[index] = moved;
            indices[moved.Handle.Id] = index;
        }
        activeProjectiles.RemoveAt(last);
    }

    #endregion

    #region Frame Update

    /// <summary>
    /// Một vòng cho mọi viên: trừ tuổi thọ và tạo query → chạy tất cả query trong một job → xử lý kết quả.
    /// Chạy trước MovementSystem.LateTick, nên vị trí áp dụng sau đó đã tính tới va chạm của frame này.
    /// </summary>
    public void Tick()
    {
        float deltaTime = Time.deltaTime;
        if (disposed || deltaTime <= 0f) return;

        // Đạn tạo ra trong callback (ví dụ hiệu ứng nổ bắn thêm) chỉ được xử lý từ frame sau.
        tickSnapshot.Clear();
        tickSnapshot.AddRange(activeProjectiles);
        EnsureCapacity(tickSnapshot.Count);

        PrepareQueries(deltaTime);
        spatialQuery.ExecuteQueries(queries, results, tickSnapshot.Count);
        CompleteQueries();
        tickSnapshot.Clear();
    }

    private void PrepareQueries(float deltaTime)
    {
        for (int index = 0; index < tickSnapshot.Count; index++)
        {
            queries[index] = default;
            Registration registration = tickSnapshot[index];
            if (!IsStillFlying(registration)) continue;
            try
            {
                if (!registration.Projectile.AdvanceLifetime(deltaTime)) continue;
                if (registration.Projectile.PrepareQuery(deltaTime, out SpatialQuery query)) queries[index] = query;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, registration.Projectile);
                // Callback có thể đã trả vỏ về pool và spawn lại; chỉ thu hồi nếu vẫn đúng lượt bay.
                if (movementManager.IsRegistered(registration.Handle)) registration.Projectile.Release();
            }
        }
    }

    private void CompleteQueries()
    {
        for (int index = 0; index < tickSnapshot.Count; index++)
        {
            Registration registration = tickSnapshot[index];
            if (queries[index].Type == SpatialQueryType.None || !IsStillFlying(registration)) continue;
            try
            {
                SpatialHit hit = results[index];
                // Viên trước trong cùng frame có thể vừa giết đúng mục tiêu này. Quét lại đường bay
                // để viên này không xuyên qua mục tiêu phía sau; giữ cả vị trí trúng mới cho VFX.
                if (!spatialResolution.TryResolve(hit, out CharacterHandle target) && hit.Found != 0)
                {
                    hit = spatialQuery.Query(queries[index]);
                    spatialResolution.TryResolve(hit, out target);
                }
                registration.Projectile.CompleteQuery(target, new Vector2(hit.Position.x, hit.Position.y));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, registration.Projectile);
                if (movementManager.IsRegistered(registration.Handle)) registration.Projectile.Release();
            }
        }
    }

    private bool IsStillFlying(Registration registration)
        => movementManager.IsRegistered(registration.Handle) &&
           registration.Projectile != null && registration.Projectile.isActiveAndEnabled;

    #endregion

    #region Camera Despawn

    // Built-in pipeline gọi sau LateUpdate và trước khi render, nên dùng đúng vị trí mới của đạn và camera.
    private void DespawnOutsideCamera(Camera renderingCamera)
    {
        // Chỉ camera gameplay (tag MainCamera); camera của Scene view không được thu hồi đạn.
        if (disposed || activeProjectiles.Count == 0 || Time.deltaTime <= 0f ||
            renderingCamera.cameraType != CameraType.Game || renderingCamera != Camera.main) return;

        GeometryUtility.CalculateFrustumPlanes(renderingCamera, cameraPlanes);
        for (int index = 0; index < cameraPlanes.Length; index++)
        {
            Plane plane = cameraPlanes[index];
            plane.distance += CameraDespawnMargin; // Nới vùng giữ đạn ra ngoài mỗi mặt của camera.
            cameraPlanes[index] = plane;
        }

        // Release làm thay đổi danh sách đăng ký, nên duyệt bản chụp.
        tickSnapshot.Clear();
        tickSnapshot.AddRange(activeProjectiles);
        try
        {
            for (int index = 0; index < tickSnapshot.Count; index++)
            {
                Registration registration = tickSnapshot[index];
                if (IsStillFlying(registration) &&
                    !GeometryUtility.TestPlanesAABB(cameraPlanes, registration.Projectile.VisualBounds))
                    registration.Projectile.Release();
            }
        }
        finally { tickSnapshot.Clear(); }
    }

    #endregion

    #region Capacity

    /// <summary>Cấp phát trước buffer cho <paramref name="count"/> viên, để không resize giữa trận.</summary>
    public void PrewarmCapacity(int count)
    {
        if (disposed) throw new ObjectDisposedException(nameof(ProjectileSystem));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        EnsureCapacity(count);
        if (activeProjectiles.Capacity < count) activeProjectiles.Capacity = count;
        if (tickSnapshot.Capacity < count) tickSnapshot.Capacity = count;
        indices.EnsureCapacity(count);
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

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Camera.onPreCull -= DespawnOutsideCamera; // Event static, không được giữ manager sau khi scene kết thúc.
        activeProjectiles.Clear();
        indices.Clear();
        tickSnapshot.Clear();
        queries.Dispose();
        results.Dispose();
    }

    #endregion

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
