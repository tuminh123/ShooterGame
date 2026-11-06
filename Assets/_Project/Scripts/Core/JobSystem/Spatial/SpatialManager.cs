using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// Sở hữu đăng ký, snapshot hình học và grid. Chỉ bảng tra main thread giữ tham chiếu Unity.
public sealed class SpatialManager : ISpatialManager, IDisposable
{
    #region Registration Data

    // Dữ liệu managed để đổi handle thành đúng cá thể; không truyền struct này vào job.
    private struct Registration
    {
        public CharacterView View; // Vỏ Unity của đối tượng.
        public CharacterRuntimeData Identity; // Danh tính của lượt spawn đã đăng ký.
        public ulong Generation;
        public uint Version; // Tăng khi ô được tái sử dụng.
        public bool Registered; // Tách khỏi Unity == null để vẫn thu hồi slot khi GameObject bị Destroy trực tiếp.
    }

    #endregion

    #region Registry & Native Buffers

    private readonly ISpatialSearchFactory searchFactory; // Chọn strategy; manager không giữ thuật toán tìm cụ thể.
    private readonly HashSet<SpatialQueryType> batchTypes = new HashSet<SpatialQueryType>(); // Các loại xuất hiện trong batch, tái sử dụng qua mỗi lượt.
    private const float CellSize = 1f; // World unit; ô nhỏ giảm ứng viên nhưng tăng số ô phải duyệt.
    private Registration[] registrations = new Registration[128]; // Bảng tra main thread; 128 là ban đầu, đầy sẽ tăng dung lượng.
    private readonly Stack<int> freeSlots = new Stack<int>(); // Các ô rảnh có thể đăng ký lại với phiên bản mới.
    private NativeArray<SpatialEntity> entities; // Snapshot hitbox world-space để job đọc.
    private NativeMultiHashMap<int2, int> grid; // Tọa độ ô → các index đối tượng trong ô đó.
    private NativeList<SpatialHit> allHits; // Buffer dùng lại cho truy vấn lấy tất cả mục tiêu.
    private NativeArray<SpatialQuery> singleQuery; // Buffer một phần tử cho truy vấn tức thời.
    private NativeArray<SpatialHit> singleResult; // Kết quả tương ứng với singleQuery.
    private JobHandle pendingJob; // Hoàn tất trước callback, resize hoặc Dispose.
    private float2 maxHalfSize; // Nới vùng tìm theo hộp lớn nhất; mỗi entity chỉ cần chèn một lần vào grid.
    private int slotCount; // Số ô từng mở, gồm ô rảnh; không phải số nhân vật đang sống.
    private int snapshotFrame = -1; // Frame gần nhất đã chụp vị trí và dựng grid.
    private bool dirty = true; // Cần dựng lại snapshot do đăng ký/hình học thay đổi.
    private bool disposed; // Ngăn truy cập native buffer sau khi đã giải phóng.

    #endregion

    #region Initialization

    // Tạo buffer persistent; các truy vấn sau dùng lại thay vì cấp phát mỗi frame.
    public SpatialManager(ISpatialSearchFactory searchFactory)
    {
        this.searchFactory = searchFactory;
        entities = new NativeArray<SpatialEntity>(128, Allocator.Persistent);
        grid = new NativeMultiHashMap<int2, int>(128, Allocator.Persistent);
        allHits = new NativeList<SpatialHit>(128, Allocator.Persistent);
        singleQuery = new NativeArray<SpatialQuery>(1, Allocator.Persistent);
        singleResult = new NativeArray<SpatialHit>(1, Allocator.Persistent);
    }

    #endregion

    #region Registration & Target Resolution

    // Cấp handle cho một lượt hoạt động; ưu tiên ô rảnh trước khi mở ô mới.
    public SpatialHandle Register(CharacterView view)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SpatialManager));
        if (view == null || view.RuntimeData == null) throw new ArgumentException("Initialize the character before spatial registration.");
        int index = freeSlots.Count > 0 ? freeSlots.Pop() : slotCount++;
        EnsureCapacity(slotCount);
        uint version = checked(registrations[index].Version + 1u);
        registrations[index] = new Registration { View = view, Identity = view.RuntimeData,
            Generation = view.RuntimeData.Generation, Version = version, Registered = true };
        dirty = true;
        return new SpatialHandle(this, index, version);
    }

    // Handle chỉ hợp lệ với đúng manager, ô đang đăng ký và phiên bản hiện tại.
    public bool IsRegistered(SpatialHandle handle)
        => !disposed && ReferenceEquals(handle.Owner, this) && handle.Index >= 0 && handle.Index < slotCount &&
           registrations[handle.Index].Registered && registrations[handle.Index].Version == handle.Version;

    // Gỡ tham chiếu và trả ô rảnh; gọi lại với handle cũ không ảnh hưởng đăng ký mới.
    public void Unregister(SpatialHandle handle)
    {
        if (!IsRegistered(handle)) return;
        Registration registration = registrations[handle.Index];
        registration.View = null;
        registration.Identity = null;
        registration.Registered = false;
        registrations[handle.Index] = registration;
        freeSlots.Push(handle.Index);
        dirty = true;
    }

    public void Invalidate() => dirty = true; // Gọi sau movement/teleport hoặc khi hình học thay đổi trong cùng frame.

    // Đổi handle thành mục tiêu, kiểm tra thêm danh tính spawn và trạng thái sống/hoạt động.
    public bool TryResolve(SpatialHandle handle, out CharacterTarget target)
    {
        target = default;
        if (!IsRegistered(handle)) return false;
        Registration registration = registrations[handle.Index];
        if (registration.Identity == null || registration.Identity.Generation != registration.Generation) return false;
        target = new CharacterTarget(registration.View, registration.Identity);
        return target.TryGetCharacter(out _);
    }

    // Chuyển kết quả số từ job sang mục tiêu dùng được trên main thread.
    public bool TryResolve(SpatialHit hit, out CharacterTarget target)
    {
        target = default;
        return hit.Found != 0 && TryResolve(new SpatialHandle(this, hit.Index, hit.Version), out target);
    }

    #endregion

    #region Strategy Execution

    // API tức thời cho AI hiện tại: chạy cùng Burst job trên main thread, tránh Schedule/Complete cho từng query nhỏ.
    public SpatialHit Query(SpatialQuery query, SpatialHandle exclude = default)
    {
        if (query.Type == SpatialQueryType.None) return default;
        ISpatialSearchStrategy strategy = SelectStrategy(query, returnsMany: false);
        ApplyExclusion(ref query, exclude);
        PrepareSnapshot();
        singleQuery[0] = query;
        singleResult[0] = default;
        strategy.Run(CreateContext(singleQuery, singleResult, 1));
        return singleResult[0];
    }

    // Đường xử lý hàng loạt: toàn bộ query đạn được phân cho worker trong một lượt Schedule.
    public void ExecuteQueries(NativeArray<SpatialQuery> queries, NativeArray<SpatialHit> results, int count)
    {
        if (count < 0 || count > queries.Length || count > results.Length) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return;
        batchTypes.Clear();
        // Kiểm tra mọi query trước Schedule; mỗi strategy chỉ chạy một lần cho loại có mặt trong batch.
        for (int index = 0; index < count; index++)
        {
            results[index] = default;
            SpatialQuery query = queries[index];
            if (query.Type == SpatialQueryType.None) continue;
            SelectStrategy(query, returnsMany: false);
            batchTypes.Add(query.Type);
        }
        if (batchTypes.Count == 0) return;
        PrepareSnapshot();
        SpatialSearchContext context = CreateContext(queries, results, count);
        try
        {
            foreach (SpatialQueryType type in batchTypes)
                pendingJob = searchFactory.Get(type).Schedule(context, pendingJob);
        }
        finally { pendingJob.Complete(); } // Callback/resize chỉ được diễn ra sau khi mọi strategy hoàn tất.
    }

    // View tạm của buffer native; caller phải sao chép kết quả trước query tiếp theo, không giữ qua callback/frame.
    public NativeArray<SpatialHit> FindAll(SpatialQuery query, SpatialHandle exclude = default)
    {
        ISpatialSearchStrategy strategy = SelectStrategy(query, returnsMany: true);
        ApplyExclusion(ref query, exclude);
        PrepareSnapshot();
        allHits.Clear();
        singleQuery[0] = query;
        strategy.Run(CreateContext(singleQuery, singleResult, 1));
        return allHits.AsArray();
    }

    // Cấp dữ liệu mượn cho strategy, quyền sở hữu buffer vẫn thuộc manager/caller.
    private SpatialSearchContext CreateContext(NativeArray<SpatialQuery> queries, NativeArray<SpatialHit> results, int count)
        => new SpatialSearchContext
        {
            Entities = entities, Grid = grid, Queries = queries, Results = results,
            AllResults = allHits, Count = count, CellSize = CellSize, MaxHalfSize = maxHalfSize
        };

    // Chặn dùng nhầm query nhiều hit với layout một hit/query, không phụ thuộc loại thuật toán cụ thể.
    private ISpatialSearchStrategy SelectStrategy(SpatialQuery query, bool returnsMany)
    {
        searchFactory.Validate(query);
        ISpatialSearchStrategy strategy = searchFactory.Get(query.Type);
        if (strategy.ReturnsMany != returnsMany)
            throw new ArgumentException("Use FindAll for multiple-result strategies and Query/ExecuteQueries for single-result strategies.");
        return strategy;
    }

    // Chỉ bỏ qua handle cùng manager; handle từ scene khác không được loại nhầm slot trùng index.
    private void ApplyExclusion(ref SpatialQuery query, SpatialHandle exclude)
    {
        query.ExcludeIndex = ReferenceEquals(exclude.Owner, this) ? exclude.Index : -1;
        query.ExcludeVersion = exclude.Version;
    }

    #endregion

    #region Snapshot & Capacity

    // Chụp lại khi sang frame mới hoặc dirty; mọi query cùng snapshot đọc cùng dữ liệu hình học.
    private void PrepareSnapshot()
    {
        if (disposed) throw new ObjectDisposedException(nameof(SpatialManager));
        if (!dirty && snapshotFrame == Time.frameCount) return;
        pendingJob.Complete();
        grid.Clear();
        maxHalfSize = float2.zero;
        for (int index = 0; index < slotCount; index++)
        {
            Registration registration = registrations[index];
            SpatialEntity entity = default;
            if (registration.View != null && registration.View.isActiveAndEnabled &&
                ReferenceEquals(registration.View.RuntimeData, registration.Identity) &&
                registration.Identity.Generation == registration.Generation && registration.Identity.IsAlive)
            {
                entity = registration.View.GetSpatialEntity();
                entity.Index = index;
                entity.Version = registration.Version;
                entity.Team = (int)registration.Identity.Team;
                entity.Active = 1;
                maxHalfSize = math.max(maxHalfSize, entity.BoundsHalfSize);
            }
            entities[index] = entity;
        }
        if (slotCount > 0)
        {
            pendingJob = new BuildSpatialGridJob { Entities = entities, Grid = grid.AsParallelWriter(), CellSize = CellSize }
                .Schedule(slotCount, 64);
            pendingJob.Complete();
        }
        snapshotFrame = Time.frameCount;
        dirty = false;
    }

    // Vượt 128 sẽ tăng 256, 512...; giữ đăng ký cũ và dựng lại dữ liệu native từ các đăng ký đó.
    public void PrewarmCapacity(int count)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SpatialManager));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        EnsureCapacity(count);
        if (allHits.Capacity < count) allHits.Capacity = count;
    }

    private void EnsureCapacity(int count)
    {
        if (entities.Length >= count) return;
        pendingJob.Complete();
        int capacity = Mathf.NextPowerOfTwo(count);
        entities.Dispose(); // Snapshot sẽ được dựng lại toàn bộ, không cần sao chép hình học cũ.
        entities = new NativeArray<SpatialEntity>(capacity, Allocator.Persistent);
        grid.Capacity = capacity;
        allHits.Capacity = capacity; // Mỗi entity chỉ xuất hiện một lần; FindAll không cắt bớt kết quả.
        Array.Resize(ref registrations, capacity);
        dirty = true;
    }

    #endregion

    #region Cleanup

    // Hoàn tất job rồi thu hồi native memory và các tham chiếu nhân vật.
    public void Dispose()
    {
        if (disposed) return;
        pendingJob.Complete();
        disposed = true;
        entities.Dispose();
        grid.Dispose();
        allHits.Dispose();
        singleQuery.Dispose();
        singleResult.Dispose();
        Array.Clear(registrations, 0, registrations.Length);
        freeSlots.Clear();
    }

    #endregion
}
