using Unity.Jobs;

#region SpatialSearchStrategy

/// <summary>Chạy thuật toán truy vấn trên buffer do manager cung cấp.</summary>
public interface ISpatialSearchStrategy
{
    bool ReturnsMany { get; } // Phân biệt buffer một hit/query và buffer nhiều hit.
    void Run(SpatialSearchContext context); // Trả kết quả ngay, không tạo worker job cho truy vấn nhỏ.
    JobHandle Schedule(SpatialSearchContext context, JobHandle dependency); // Chạy nhóm và nối dependency để manager Complete.
}

#endregion
