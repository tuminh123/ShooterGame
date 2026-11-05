using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// Tạo dữ liệu query cho CharacterSearch/đạn và chọn strategy cho SpatialManager.
// Strategy được tạo một lần, không giữ snapshot hay trạng thái của từng lượt tìm.
public sealed class SpatialSearchFactory : ISpatialSearchFactory
{
    #region Strategy Registry

    private readonly Dictionary<SpatialQueryType, ISpatialSearchStrategy> strategies =
        new Dictionary<SpatialQueryType, ISpatialSearchStrategy>
        {
            { SpatialQueryType.Nearest, new NearestSpatialSearch() },
            { SpatialQueryType.Sweep, new SweepSpatialSearch() },
            { SpatialQueryType.AllInCircle, new CircleSpatialSearch() }
        };

    // Tra loại thực thi trên main thread; không truyền dictionary/interface vào Burst job.
    public ISpatialSearchStrategy Get(SpatialQueryType type)
    {
        if (!strategies.TryGetValue(type, out ISpatialSearchStrategy strategy))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Register a strategy for this spatial query type.");
        return strategy;
    }

    #endregion

    #region Query Creation

    // Tìm một hitbox giao vùng tròn, xếp hạng theo khoảng cách tâm.
    public SpatialQuery Nearest(Vector2 center, float radius, CharacterTeam team)
        => CreateCircle(SpatialQueryType.Nearest, center, radius, team);

    // Tìm mọi hitbox giao vùng tròn; không cắt bớt số kết quả.
    public SpatialQuery AllInCircle(Vector2 center, float radius, CharacterTeam team)
        => CreateCircle(SpatialQueryType.AllInCircle, center, radius, team);

    // Quét viên đạn từ start tới end và lấy mục tiêu trúng đầu tiên.
    public SpatialQuery Sweep(Vector2 start, Vector2 end, float radius, CharacterTeam team)
    {
        SpatialQuery query = CreateCircle(SpatialQueryType.Sweep, start, radius, team);
        query.End = new float2(end.x, end.y);
        Validate(query);
        return query;
    }

    // Hai kiểu tìm tròn dùng chung cách tạo hình học, chỉ khác loại strategy.
    private SpatialQuery CreateCircle(SpatialQueryType type, Vector2 center, float radius, CharacterTeam team)
    {
        var query = new SpatialQuery
        {
            Type = type, Start = new float2(center.x, center.y), End = new float2(center.x, center.y),
            Radius = radius, TargetTeam = (int)team, ExcludeIndex = -1
        };
        Validate(query);
        return query;
    }

    #endregion

    #region Validation

    // Kiểm tra cả query do bên ngoài tự tạo trước khi job tính tọa độ ô.
    public void Validate(SpatialQuery query)
    {
        if (!math.all(math.isfinite(query.Start)) || !math.all(math.isfinite(query.End)) ||
            !math.isfinite(query.Radius) || query.Radius < 0f ||
            (query.TargetTeam != (int)CharacterTeam.Player && query.TargetTeam != (int)CharacterTeam.Enemy))
            throw new ArgumentException("Spatial queries require finite geometry, a nonnegative radius and a valid team.");
    }

    #endregion
}
