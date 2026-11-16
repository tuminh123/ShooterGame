using Unity.Collections;

#region SpatialRegistration

/// <summary>Đăng ký hitbox và kiểm tra lượt đăng ký còn hiệu lực.</summary>
public interface ISpatialRegistration
{
    SpatialHandle Register(CharacterView view);
    bool IsRegistered(SpatialHandle handle);
    void Unregister(SpatialHandle handle);
}

#endregion

#region SpatialInvalidation

/// <summary>Yêu cầu dựng lại snapshot khi hình học hoặc vị trí thay đổi.</summary>
public interface ISpatialInvalidation
{
    void Invalidate();
}

#endregion

#region SpatialResolution

/// <summary>Chuyển handle hoặc kết quả truy vấn thành mục tiêu còn hợp lệ.</summary>
public interface ISpatialResolution
{
    bool TryResolve(SpatialHandle handle, out CharacterTarget target);
    bool TryResolve(SpatialHit hit, out CharacterTarget target);
}

#endregion

#region SpatialQuery

/// <summary>Thực hiện truy vấn tức thời, theo batch hoặc lấy nhiều kết quả.</summary>
public interface ISpatialQuery
{
    SpatialHit Query(SpatialQuery query, SpatialHandle exclude = default);
    void ExecuteQueries(NativeArray<SpatialQuery> queries, NativeArray<SpatialHit> results, int count);
    NativeArray<SpatialHit> FindAll(SpatialQuery query, SpatialHandle exclude = default);
}

#endregion

#region SpatialCapacity

/// <summary>Chuẩn bị dung lượng trước gameplay.</summary>
public interface ISpatialCapacity
{
    void PrewarmCapacity(int count);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của SpatialManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ISpatialManager
{
    ISpatialRegistration GetRegistration();
    ISpatialInvalidation GetInvalidation();
    ISpatialResolution GetResolution();
    ISpatialQuery GetQueries();
    ISpatialCapacity GetCapacity();
}

#endregion
