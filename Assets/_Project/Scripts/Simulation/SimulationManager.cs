using System;
using Unity.Collections;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Simulation: di chuyển và truy vấn không gian bằng Jobs/Burst.
/// Đây là class <b>duy nhất</b> của Simulation được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// SimulationManager
/// ├── SpatialSearchFactory   tạo query hợp lệ, chọn strategy truy vấn
/// ├── SpatialSystem         lưới hitbox, snapshot mỗi frame, chạy truy vấn
/// └── MovementSystem        job di chuyển XY; báo SpatialSystem dựng lại lưới sau khi đổi vị trí
/// </code>
/// Simulation là hạ tầng nằm dưới mọi miền gameplay. Manager chỉ ủy quyền cho system con;
/// miền khác dùng các vai trò trong <c>SimulationContracts.cs</c>.
/// </remarks>
public sealed class SimulationManager :
    DomainManager,
    IMovementRegistration,
    IMovementCommand,
    IMovementArena,
    ISpatialRegistration,
    ISpatialInvalidation,
    ISpatialResolution,
    ISpatialQuery,
    ISpatialQueryBuilder,
    ISimulationCapacity,
    ILateTickable,
    IDisposable
{
    #region Owned Systems

    private readonly SpatialSearchFactory searchFactory;
    private readonly SpatialSystem spatial;
    private readonly MovementSystem movement;
    private bool disposed;

    #endregion

    #region Construction

    /// <summary>Dựng miền Simulation. Thứ tự tạo đi theo chiều phụ thuộc: factory → spatial → movement.</summary>
    public SimulationManager()
    {
        try
        {
            searchFactory = new SpatialSearchFactory();
            spatial = new SpatialSystem(searchFactory);
            movement = new MovementSystem(spatial);
        }
        catch
        {
            // SpatialSystem cấp phát NativeArray ngay trong constructor; lỗi ở bước sau không được làm rò bộ nhớ.
            Dispose();
            throw;
        }
    }

    #endregion

    #region Frame Update

    /// <summary>
    /// Áp dụng di chuyển của cả frame trong một job. Chạy ở LateTick để mọi Update/Tick
    /// (input, AI, đạn quét va chạm) đã gửi lệnh di chuyển xong.
    /// </summary>
    public override void LateTick()
    {
        if (disposed) return;
        movement.LateTick();
    }

    #endregion

    #region Movement Roles

    /// <inheritdoc/>
    public MovementHandle Register(Transform target) => movement.Register(target);

    /// <inheritdoc/>
    public bool IsRegistered(MovementHandle handle) => movement.IsRegistered(handle);

    /// <inheritdoc/>
    public bool Unregister(MovementHandle handle) => movement.Unregister(handle);

    /// <inheritdoc/>
    public bool SetMovement(MovementHandle handle, Vector2 direction, float speed, float maxDistance = float.PositiveInfinity)
        => movement.SetMovement(handle, direction, speed, maxDistance);

    /// <inheritdoc/>
    public void SetArenaBounds(Bounds? bounds) => movement.SetArenaBounds(bounds);

    #endregion

    #region Spatial Roles

    /// <inheritdoc/>
    public SpatialHandle Register(CharacterView view) => spatial.Register(view);

    /// <inheritdoc/>
    public bool IsRegistered(SpatialHandle handle) => spatial.IsRegistered(handle);

    /// <inheritdoc/>
    public void Unregister(SpatialHandle handle) => spatial.Unregister(handle);

    /// <inheritdoc/>
    public void Invalidate() => spatial.Invalidate();

    /// <inheritdoc/>
    public bool TryResolve(SpatialHandle handle, out CharacterHandle target) => spatial.TryResolve(handle, out target);

    /// <inheritdoc/>
    public bool TryResolve(SpatialHit hit, out CharacterHandle target) => spatial.TryResolve(hit, out target);

    /// <inheritdoc/>
    public SpatialHit Query(SpatialQuery query, SpatialHandle exclude = default) => spatial.Query(query, exclude);

    /// <inheritdoc/>
    public void ExecuteQueries(NativeArray<SpatialQuery> queries, NativeArray<SpatialHit> results, int count)
        => spatial.ExecuteQueries(queries, results, count);

    /// <inheritdoc/>
    public NativeArray<SpatialHit> FindAll(SpatialQuery query, SpatialHandle exclude = default) => spatial.FindAll(query, exclude);

    #endregion

    #region Query Builder Role

    /// <inheritdoc/>
    public SpatialQuery Nearest(Vector2 center, float radius, CharacterTeam team) => searchFactory.Nearest(center, radius, team);

    /// <inheritdoc/>
    public SpatialQuery AllInCircle(Vector2 center, float radius, CharacterTeam team) => searchFactory.AllInCircle(center, radius, team);

    /// <inheritdoc/>
    public SpatialQuery Sweep(Vector2 start, Vector2 end, float radius, CharacterTeam team) => searchFactory.Sweep(start, end, radius, team);

    #endregion

    #region ISimulationCapacity

    /// <inheritdoc/>
    public void PrewarmCapacity(int movers, int hitboxes)
    {
        movement.PrewarmCapacity(movers);
        spatial.PrewarmCapacity(hitboxes);
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Movement trước, spatial sau: movement báo spatial dựng lại lưới, nên spatial phải sống lâu hơn.
    /// (Giữ đúng thứ tự cũ khi hai system được Zenject dispose riêng: -100 rồi -250.)
    /// Mỗi bước được cô lập để lỗi của một bên không làm rò NativeArray của bên kia.
    /// </summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DisposeSafely(movement);
        DisposeSafely(spatial);
    }

    // System có thể null nếu constructor lỗi giữa chừng.
    private static void DisposeSafely(IDisposable system)
    {
        if (system == null) return;
        try { system.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    #endregion
}
