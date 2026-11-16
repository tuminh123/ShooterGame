using UnityEngine;

#region SpatialQueryBuilder

/// <summary>Tạo dữ liệu truy vấn hình học, không thực thi tìm kiếm.</summary>
public interface ISpatialQueryBuilder
{
    SpatialQuery Nearest(Vector2 center, float radius, CharacterTeam team);
    SpatialQuery AllInCircle(Vector2 center, float radius, CharacterTeam team);
    SpatialQuery Sweep(Vector2 start, Vector2 end, float radius, CharacterTeam team);
}

#endregion

#region SpatialStrategyProvider

/// <summary>Chọn thuật toán chạy query theo loại hình học.</summary>
public interface ISpatialStrategyProvider
{
    ISpatialSearchStrategy Get(SpatialQueryType type);
}

#endregion

#region SpatialQueryValidator

/// <summary>Kiểm tra query trước khi đưa vào buffer native.</summary>
public interface ISpatialQueryValidator
{
    void Validate(SpatialQuery query);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của SpatialSearchFactory; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ISpatialSearchFactory
{
    ISpatialQueryBuilder GetQueryBuilder();
    ISpatialStrategyProvider GetStrategyProvider();
    ISpatialQueryValidator GetQueryValidator();
}

#endregion
