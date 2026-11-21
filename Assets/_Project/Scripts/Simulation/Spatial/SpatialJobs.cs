using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

#region Grid Construction

/// <summary>Job Burst dựng lưới không gian: chèn mỗi entity hợp lệ vào ô chứa tâm của nó.</summary>
[BurstCompile]
internal struct BuildSpatialGridJob : IJobParallelFor
{
    /// <summary>Các hitbox đã chụp trên main thread.</summary>
    [ReadOnly] public NativeArray<SpatialEntity> Entities;
    /// <summary>Nhiều worker chèn đối tượng vào ô tương ứng.</summary>
    public NativeMultiHashMap<int2, int>.ParallelWriter Grid;
    /// <summary>Chiều dài cạnh ô theo world unit.</summary>
    public float CellSize;

    /// <summary>Chèn một đối tượng hợp lệ; không truy cập CharacterView/Transform trong job.</summary>
    public void Execute(int index)
    {
        SpatialEntity entity = Entities[index];
        // Một tâm chỉ ở một ô; truy vấn được mở rộng theo kích thước hitbox lớn nhất để không bỏ sót hộp to.
        if (entity.Active != 0) Grid.Add((int2)math.floor(entity.Center / CellSize), index);
    }
}

#endregion

#region Single Hit Queries

/// <summary>Job Burst xử lý song song các query lấy một mục tiêu tốt nhất cho một loại query.</summary>
[BurstCompile]
internal struct SpatialQueryJob : IJobParallelFor
{
    /// <summary>Strategy chọn loại cần xử lý; các query khác giữ nguyên kết quả.</summary>
    public SpatialQueryType SupportedType;
    /// <summary>Hình học để kiểm tra chính xác sau khi lọc ứng viên.</summary>
    [ReadOnly] public NativeArray<SpatialEntity> Entities;
    /// <summary>Danh sách ứng viên theo tọa độ ô.</summary>
    [ReadOnly] public NativeMultiHashMap<int2, int> Grid;
    /// <summary>Yêu cầu tìm đã được factory/manager kiểm tra.</summary>
    [ReadOnly] public NativeArray<SpatialQuery> Queries;
    /// <summary>Mỗi worker chỉ ghi kết quả ở index của mình.</summary>
    public NativeArray<SpatialHit> Results;
    /// <summary>Dùng cùng kích thước ô với bước dựng grid.</summary>
    public float CellSize;
    /// <summary>Nới vùng lấy ứng viên để bao cả hộp có tâm nằm ngoài vùng query.</summary>
    public float2 MaxHalfSize;

    /// <summary>Xử lý một query: tìm ứng viên, kiểm tra hình học và chọn điểm số tốt nhất.</summary>
    public void Execute(int index)
    {
        SpatialQuery query = Queries[index];
        SpatialHit result = default;
        if (query.Type != SupportedType) return;
        float2 extent = MaxHalfSize + query.Radius; // Phần mở rộng quanh vùng/đường tìm.
        int2 min = (int2)math.floor((math.min(query.Start, query.End) - extent) / CellSize); // Ô nhỏ nhất cần duyệt.
        int2 max = (int2)math.floor((math.max(query.Start, query.End) + extent) / CellSize); // Ô lớn nhất cần duyệt.
        float best = float.PositiveInfinity; // Khoảng cách bình phương hoặc tỷ lệ đường bay nhỏ nhất.
        for (int x = min.x; x <= max.x; x++)
        for (int y = min.y; y <= max.y; y++)
        {
            if (!Grid.TryGetFirstValue(new int2(x, y), out int entityIndex, out var iterator)) continue;
            do
            {
                SpatialEntity entity = Entities[entityIndex];
                if (!Matches(query, entity)) continue;
                float score; // Nearest: khoảng cách tâm; Sweep: tỷ lệ đoạn đường tới lúc trúng.
                if (query.Type == SpatialQueryType.Sweep)
                {
                    if (!SpatialGeometry.SweepCircle(query.Start, query.End, query.Radius, entity, out score)) continue;
                }
                else
                {
                    if (!SpatialGeometry.CircleOverlapsBox(query.Start, query.Radius, entity)) continue;
                    score = math.lengthsq(entity.Center - query.Start);
                }
                // Cùng khoảng cách chọn index thấp hơn để kết quả không phụ thuộc thứ tự chèn của parallel grid.
                if (score > best || (score == best && result.Found != 0 && entity.Index >= result.Index)) continue;
                best = score;
                result = new SpatialHit
                {
                    Found = 1, Index = entity.Index, Version = entity.Version,
                    Fraction = query.Type == SpatialQueryType.Sweep ? score : 0f,
                    Position = query.Type == SpatialQueryType.Sweep ? math.lerp(query.Start, query.End, score) : entity.Center
                };
            } while (Grid.TryGetNextValue(out entityIndex, ref iterator));
        }
        Results[index] = result;
    }

    /// <summary>Lọc trạng thái, phe và chính đối tượng bị loại trừ trước khi tính hình học.</summary>
    internal static bool Matches(SpatialQuery query, SpatialEntity entity)
        => entity.Active != 0 && entity.Team == query.TargetTeam &&
           !(entity.Index == query.ExcludeIndex && entity.Version == query.ExcludeVersion);
}

#endregion

#region Multiple Hit Queries

/// <summary>Job Burst lấy mọi mục tiêu trong vùng tròn của một query.</summary>
[BurstCompile]
internal struct SpatialFindAllJob : IJob
{
    /// <summary>Snapshot hitbox dùng chung.</summary>
    [ReadOnly] public NativeArray<SpatialEntity> Entities;
    /// <summary>Chỉ duyệt các ô gần vùng tròn.</summary>
    [ReadOnly] public NativeMultiHashMap<int2, int> Grid;
    /// <summary>Đủ chỗ cho mọi entity; không tăng dung lượng trong job.</summary>
    public NativeList<SpatialHit> Results;
    /// <summary>Một yêu cầu lấy tất cả mục tiêu.</summary>
    public SpatialQuery Query;
    /// <summary>Kích thước ô của grid hiện tại.</summary>
    public float CellSize;
    /// <summary>Nới vùng ứng viên theo kích thước hitbox lớn nhất.</summary>
    public float2 MaxHalfSize;

    /// <summary>Thu mọi hitbox giao vòng tròn; mỗi entity ở một ô nên không lặp kết quả.</summary>
    public void Execute()
    {
        float2 extent = MaxHalfSize + Query.Radius; // Bao cả hitbox to có tâm ngoài vòng tròn.
        int2 min = (int2)math.floor((Query.Start - extent) / CellSize);
        int2 max = (int2)math.floor((Query.Start + extent) / CellSize);
        for (int x = min.x; x <= max.x; x++)
        for (int y = min.y; y <= max.y; y++)
        {
            if (!Grid.TryGetFirstValue(new int2(x, y), out int entityIndex, out var iterator)) continue;
            do
            {
                SpatialEntity entity = Entities[entityIndex];
                if (SpatialQueryJob.Matches(Query, entity) && SpatialGeometry.CircleOverlapsBox(Query.Start, Query.Radius, entity))
                    Results.AddNoResize(new SpatialHit { Found = 1, Index = entity.Index, Version = entity.Version, Position = entity.Center });
            } while (Grid.TryGetNextValue(out entityIndex, ref iterator));
        }
    }
}

#endregion
