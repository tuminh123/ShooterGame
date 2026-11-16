using System;
using UnityEngine;

#region Single Target Searches

// Chung bước xác nhận danh tính; lớp cụ thể quyết định query nào cần gửi xuống Spatial.
public abstract class SingleCharacterSearch : ISingleCharacterSearch
{
    private readonly ISpatialResolution spatialResolution; // Chạy query và tra lại đúng lượt spawn.
    private readonly ISpatialQuery spatialSpatialQuery;
    protected readonly ISpatialQueryBuilder Queries; // Cung cấp dữ liệu hình học cho từng cách tìm.

    protected SingleCharacterSearch(ISpatialResolution spatialResolution, ISpatialQuery spatialSpatialQuery, ISpatialQueryBuilder queryBuilder)
    { this.spatialResolution = spatialResolution;
        this.spatialSpatialQuery = spatialSpatialQuery; Queries = queryBuilder; }

    protected abstract SpatialQuery CreateQuery(CharacterSearchRequest request); // Lớp cụ thể chọn dữ liệu hình học cần tìm.

    // Chạy query rồi xác nhận đúng cá thể; không trả mục tiêu đã chết hoặc tái sử dụng.
    public bool TryFind(CharacterSearchRequest request, out CharacterTarget target, out Vector2 hitPosition)
    {
        SpatialHit hit = spatialSpatialQuery.Query(CreateQuery(request), request.Exclude != null ? request.Exclude.SpatialHandle : default);
        hitPosition = hit.Found != 0 ? new Vector2(hit.Position.x, hit.Position.y) : request.Origin;
        if (spatialResolution.TryResolve(hit, out target)) return true;
        target = default;
        return false;
    }
}

public sealed class FindNearestSearch : SingleCharacterSearch
{
    public FindNearestSearch(ISpatialResolution spatialResolution, ISpatialQuery spatialSpatialQuery, ISpatialQueryBuilder queryBuilder) : base(spatialResolution, spatialSpatialQuery, queryBuilder) { }

    // Xét hitbox giao vòng tròn rồi lấy tâm gần nhất.
    protected override SpatialQuery CreateQuery(CharacterSearchRequest request)
        => Queries.Nearest(request.Origin, request.Radius, request.TargetTeam);
}

public sealed class FindFirstOnPathSearch : SingleCharacterSearch
{
    public FindFirstOnPathSearch(ISpatialResolution spatialResolution, ISpatialQuery spatialSpatialQuery, ISpatialQueryBuilder queryBuilder) : base(spatialResolution, spatialSpatialQuery, queryBuilder) { }

    // Quét cả đoạn và chọn mục tiêu đầu tiên, kể cả khi đã chạm ở điểm đầu.
    protected override SpatialQuery CreateQuery(CharacterSearchRequest request)
        => Queries.Sweep(request.Origin, request.End, request.Radius, request.TargetTeam);
}

#endregion

#region Multiple Target Search

public sealed class FindAllInCircleSearch : IMultipleCharacterSearch
{
    private readonly ISpatialResolution spatialResolution; // Sở hữu native view tạm của kết quả job.
    private readonly ISpatialQuery spatialSpatialQuery;
    private readonly ISpatialQueryBuilder queryBuilder; // Tạo query lấy mọi hitbox trong vòng tròn.

    public FindAllInCircleSearch(ISpatialResolution spatialResolution, ISpatialQuery spatialSpatialQuery, ISpatialQueryBuilder queryBuilder)
    { this.spatialResolution = spatialResolution;
        this.spatialSpatialQuery = spatialSpatialQuery;
        this.queryBuilder = queryBuilder; }

    // Chuyển native hit thành mục tiêu trong buffer của caller; chỉ trả số mục tiêu còn hợp lệ.
    public int FindAll(CharacterSearchRequest request, ref CharacterTarget[] results)
    {
        var hits = spatialSpatialQuery.FindAll(queryBuilder.AllInCircle(request.Origin, request.Radius, request.TargetTeam),
            request.Exclude != null ? request.Exclude.SpatialHandle : default);
        if (results == null) results = Array.Empty<CharacterTarget>();
        // Chỉ cấp phát khi vượt dung lượng; không cắt bớt kết quả để vừa mảng cũ.
        if (results.Length < hits.Length) Array.Resize(ref results, Mathf.NextPowerOfTwo(hits.Length));
        int count = 0;
        for (int index = 0; index < hits.Length; index++)
            if (spatialResolution.TryResolve(hits[index], out CharacterTarget target)) results[count++] = target;
        // Không giữ mục tiêu cũ ở phần đuôi khi lần tìm này có ít kết quả hơn.
        Array.Clear(results, count, results.Length - count);
        return count;
    }
}

#endregion
