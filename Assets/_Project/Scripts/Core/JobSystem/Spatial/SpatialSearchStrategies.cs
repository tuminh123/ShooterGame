using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

#region Shared Strategy Context

// Mượn buffer từ manager trong một lượt chạy; không sở hữu hay Dispose các buffer này.
public struct SpatialSearchContext
{
    public NativeArray<SpatialEntity> Entities; // Snapshot hình học đã dựng xong.
    public NativeMultiHashMap<int2, int> Grid; // Bảng ứng viên theo ô.
    public NativeArray<SpatialQuery> Queries; // Các yêu cầu đầu vào.
    public NativeArray<SpatialHit> Results; // Một kết quả cho mỗi query đơn.
    public NativeList<SpatialHit> AllResults; // Kết quả nhiều mục tiêu cho query vùng.
    public int Count; // Số query thực tế, không phải dung lượng mảng.
    public float CellSize; // Chiều dài cạnh ô tính theo world unit.
    public float2 MaxHalfSize; // Nới vùng duyệt để không bỏ sót hitbox lớn.
}

// Chỉ được gọi trên main thread. Strategy tạo job thuần số, không chạy interface bên trong Burst.
public interface ISpatialSearchStrategy
{
    bool ReturnsMany { get; } // Phân biệt buffer một hit/query và buffer nhiều hit.
    void Run(SpatialSearchContext context); // Trả kết quả ngay, không tạo worker job cho truy vấn nhỏ.
    JobHandle Schedule(SpatialSearchContext context, JobHandle dependency); // Chạy nhóm và nối dependency để manager Complete.
}

#endregion

#region Single Result Strategies

// Tìm gần nhất và sweep dùng chung cách cấp dữ liệu/chạy job, tránh lặp phần quản lý buffer.
internal abstract class SingleHitSpatialSearch : ISpatialSearchStrategy
{
    private readonly SpatialQueryType type; // Job chỉ ghi những query thuộc loại này trong batch hỗn hợp.
    public bool ReturnsMany => false; // Mỗi query ghi tối đa một hit vào ô kết quả tương ứng.

    // Cố định loại query mà strategy nhận trong suốt vòng đời scene.
    protected SingleHitSpatialSearch(SpatialQueryType type) => this.type = type;

    // Cùng phép tính nhưng chạy ngay trên main thread cho API tức thời.
    public void Run(SpatialSearchContext context) => CreateJob(context).Run(context.Count);

    // Mỗi strategy chạy một lượt song song; dependency bảo vệ buffer kết quả dùng chung.
    public JobHandle Schedule(SpatialSearchContext context, JobHandle dependency)
        => CreateJob(context).Schedule(context.Count, 64, dependency);

    // Kernel chung giữ phép duyệt grid và so sánh kết quả; loại query chọn phép kiểm tra hình học.
    private SpatialQueryJob CreateJob(SpatialSearchContext context)
        => new SpatialQueryJob
        {
            Entities = context.Entities, Grid = context.Grid, Queries = context.Queries,
            Results = context.Results, CellSize = context.CellSize, MaxHalfSize = context.MaxHalfSize,
            SupportedType = type
        };
}

internal sealed class NearestSpatialSearch : SingleHitSpatialSearch
{
    // Chọn ứng viên giao vùng tròn có tâm gần nhất.
    public NearestSpatialSearch() : base(SpatialQueryType.Nearest) { }
}

internal sealed class SweepSpatialSearch : SingleHitSpatialSearch
{
    // Chọn thời điểm giao đầu tiên giữa đường đạn và hitbox.
    public SweepSpatialSearch() : base(SpatialQueryType.Sweep) { }
}

#endregion

#region Multiple Result Strategy

internal sealed class CircleSpatialSearch : ISpatialSearchStrategy
{
    public bool ReturnsMany => true; // Kết quả ghi vào danh sách, không phải một ô hit.

    // FindAll dùng một buffer nhiều hit riêng và trả view tạm cho caller sao chép.
    public void Run(SpatialSearchContext context) => CreateJob(context).Run();

    // Có thể lên lịch query vùng đơn; batch nhiều vùng cần layout kết quả riêng trước khi hỗ trợ.
    public JobHandle Schedule(SpatialSearchContext context, JobHandle dependency)
        => CreateJob(context).Schedule(dependency);

    // Cấp dữ liệu cho job vùng, không chứa luật nhận diện CharacterView.
    private static SpatialFindAllJob CreateJob(SpatialSearchContext context)
        => new SpatialFindAllJob
        {
            Entities = context.Entities, Grid = context.Grid, Results = context.AllResults,
            Query = context.Queries[0], CellSize = context.CellSize, MaxHalfSize = context.MaxHalfSize
        };
}

#endregion
