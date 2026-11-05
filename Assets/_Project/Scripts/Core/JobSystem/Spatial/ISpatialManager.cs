using Unity.Collections;

// Các bên sử dụng chỉ phụ thuộc đăng ký/truy vấn, không phụ thuộc cách dựng grid hoặc quản lý bộ nhớ.
public interface ISpatialManager
{
    #region Registration

    SpatialHandle Register(CharacterView view); // Cấp danh tính cho lượt đăng ký hiện tại.
    bool IsRegistered(SpatialHandle handle); // Kiểm tra phiên bản và manager sở hữu.
    void Unregister(SpatialHandle handle); // Gỡ tham chiếu và cho phép tái sử dụng slot.
    void Invalidate(); // Yêu cầu chụp lại hình học ở truy vấn tiếp theo.
    bool TryResolve(SpatialHandle handle, out CharacterTarget target);
    bool TryResolve(SpatialHit hit, out CharacterTarget target);

    #endregion

    #region Queries

    SpatialHit Query(SpatialQuery query, SpatialHandle exclude = default); // Một kết quả tức thời.
    void ExecuteQueries(NativeArray<SpatialQuery> queries, NativeArray<SpatialHit> results, int count); // Nhóm query một hit chạy bằng worker.

    // View mượn từ manager; phải sao chép trước query tiếp theo, không Dispose view này.
    NativeArray<SpatialHit> FindAll(SpatialQuery query, SpatialHandle exclude = default);

    #endregion
}
