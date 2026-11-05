using System;
using System.Collections.Generic;
using UnityEngine;

// Factory chọn cách tìm nhân vật, khác với factory chọn job hình học ở tầng Spatial.
public sealed class CharacterSearchFactory : ICharacterSearchFactory
{
    #region Strategy Registry

    private readonly Dictionary<CharacterSearchType, ICharacterSearchStrategy> strategies; // Strategy dùng chung, không giữ buffer của caller.

    // Khởi tạo mỗi strategy một lần và chia sẻ các hợp đồng Spatial qua constructor.
    public CharacterSearchFactory(ISpatialManager spatial, ISpatialSearchFactory queries)
    {
        strategies = new Dictionary<CharacterSearchType, ICharacterSearchStrategy>
        {
            { CharacterSearchType.Nearest, new FindNearestSearch(spatial, queries) },
            { CharacterSearchType.FirstOnPath, new FindFirstOnPathSearch(spatial, queries) },
            { CharacterSearchType.AllInCircle, new FindAllInCircleSearch(spatial, queries) }
        };
    }

    // Chỉ cấp strategy có hợp đồng trả một mục tiêu.
    public ISingleCharacterSearch GetSingle(CharacterSearchType type)
    {
        if (strategies.TryGetValue(type, out ICharacterSearchStrategy strategy) && strategy is ISingleCharacterSearch single)
            return single;
        throw new ArgumentException("Register a single-target character search for this type.", nameof(type));
    }

    // Chỉ cấp strategy ghi nhiều mục tiêu vào buffer của caller.
    public IMultipleCharacterSearch GetMultiple(CharacterSearchType type)
    {
        if (strategies.TryGetValue(type, out ICharacterSearchStrategy strategy) && strategy is IMultipleCharacterSearch multiple)
            return multiple;
        throw new ArgumentException("Register a multiple-target character search for this type.", nameof(type));
    }

    #endregion
}

#region Single Target Searches

// Chung bước xác nhận danh tính; lớp cụ thể quyết định query nào cần gửi xuống Spatial.
public abstract class SingleCharacterSearch : ISingleCharacterSearch
{
    private readonly ISpatialManager spatial; // Chạy query và tra lại đúng lượt spawn.
    protected readonly ISpatialSearchFactory Queries; // Cung cấp dữ liệu hình học cho từng cách tìm.

    protected SingleCharacterSearch(ISpatialManager spatial, ISpatialSearchFactory queries)
    { this.spatial = spatial; Queries = queries; }

    protected abstract SpatialQuery CreateQuery(CharacterSearchRequest request); // Lớp cụ thể chọn dữ liệu hình học cần tìm.

    // Chạy query rồi xác nhận đúng cá thể; không trả mục tiêu đã chết hoặc tái sử dụng.
    public bool TryFind(CharacterSearchRequest request, out CharacterTarget target, out Vector2 hitPosition)
    {
        SpatialHit hit = spatial.Query(CreateQuery(request), request.Exclude != null ? request.Exclude.SpatialHandle : default);
        hitPosition = hit.Found != 0 ? new Vector2(hit.Position.x, hit.Position.y) : request.Origin;
        if (spatial.TryResolve(hit, out target)) return true;
        target = default;
        return false;
    }
}

public sealed class FindNearestSearch : SingleCharacterSearch
{
    public FindNearestSearch(ISpatialManager spatial, ISpatialSearchFactory queries) : base(spatial, queries) { }

    // Xét hitbox giao vòng tròn rồi lấy tâm gần nhất.
    protected override SpatialQuery CreateQuery(CharacterSearchRequest request)
        => Queries.Nearest(request.Origin, request.Radius, request.TargetTeam);
}

public sealed class FindFirstOnPathSearch : SingleCharacterSearch
{
    public FindFirstOnPathSearch(ISpatialManager spatial, ISpatialSearchFactory queries) : base(spatial, queries) { }

    // Quét cả đoạn và chọn mục tiêu đầu tiên, kể cả khi đã chạm ở điểm đầu.
    protected override SpatialQuery CreateQuery(CharacterSearchRequest request)
        => Queries.Sweep(request.Origin, request.End, request.Radius, request.TargetTeam);
}

#endregion

#region Multiple Target Search

public sealed class FindAllInCircleSearch : IMultipleCharacterSearch
{
    private readonly ISpatialManager spatial; // Sở hữu native view tạm của kết quả job.
    private readonly ISpatialSearchFactory queries; // Tạo query lấy mọi hitbox trong vòng tròn.

    public FindAllInCircleSearch(ISpatialManager spatial, ISpatialSearchFactory queries)
    { this.spatial = spatial; this.queries = queries; }

    // Chuyển native hit thành mục tiêu trong buffer của caller; chỉ trả số mục tiêu còn hợp lệ.
    public int FindAll(CharacterSearchRequest request, ref CharacterTarget[] results)
    {
        var hits = spatial.FindAll(queries.AllInCircle(request.Origin, request.Radius, request.TargetTeam),
            request.Exclude != null ? request.Exclude.SpatialHandle : default);
        if (results == null) results = Array.Empty<CharacterTarget>();
        // Chỉ cấp phát khi vượt dung lượng; không cắt bớt kết quả để vừa mảng cũ.
        if (results.Length < hits.Length) Array.Resize(ref results, Mathf.NextPowerOfTwo(hits.Length));
        int count = 0;
        for (int index = 0; index < hits.Length; index++)
            if (spatial.TryResolve(hits[index], out CharacterTarget target)) results[count++] = target;
        // Không giữ mục tiêu cũ ở phần đuôi khi lần tìm này có ít kết quả hơn.
        Array.Clear(results, count, results.Length - count);
        return count;
    }
}

#endregion
