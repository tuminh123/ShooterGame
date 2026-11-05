using UnityEngine;

// Tạo yêu cầu hình học và cung cấp strategy chạy job; không nhận diện nhân vật cụ thể.
public interface ISpatialSearchFactory
{
    #region Strategy Selection

    // Lấy strategy dùng chung cho loại query, không tạo mới mỗi lần tìm.
    ISpatialSearchStrategy Get(SpatialQueryType type);

    #endregion

    #region Query Data

    SpatialQuery Nearest(Vector2 center, float radius, CharacterTeam team);
    SpatialQuery AllInCircle(Vector2 center, float radius, CharacterTeam team);
    SpatialQuery Sweep(Vector2 start, Vector2 end, float radius, CharacterTeam team);
    // Kiểm tra query tự tạo trước khi đưa vào job.
    void Validate(SpatialQuery query);

    #endregion
}
