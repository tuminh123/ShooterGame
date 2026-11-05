using System;
using Unity.Mathematics;

#region Spatial Handle

// Không giữ CharacterView trong dữ liệu job. Owner chỉ tồn tại trong handle dùng trên main thread.
public readonly struct SpatialHandle : IEquatable<SpatialHandle>
{
    internal readonly ISpatialManager Owner; // Manager sở hữu đăng ký.
    internal readonly int Index; // Ô trong bảng tra nhân vật.
    internal readonly uint Version; // Phiên bản để không nhận nhầm lượt đăng ký mới.
    // Manager cấp handle sau khi đăng ký nhân vật.
    internal SpatialHandle(ISpatialManager owner, int index, uint version)
    { Owner = owner; Index = index; Version = version; }
    // Cùng index nhưng khác manager/phiên bản vẫn là đăng ký khác.
    public bool Equals(SpatialHandle other)
        => ReferenceEquals(Owner, other.Owner) && Index == other.Index && Version == other.Version;
    public override bool Equals(object obj) => obj is SpatialHandle other && Equals(other); // So sánh khi được truyền qua object.
    public override int GetHashCode() => HashCode.Combine(Index, Version); // Hỗ trợ dùng handle làm khóa.
}

#endregion

#region Native Hitbox Data

// Snapshot hình hộp world XY; không chứa tham chiếu Unity.
public struct SpatialEntity
{
    public int Index; // Ô đăng ký để tra lại nhân vật sau job.
    public uint Version; // Phiên bản đã chụp cùng hình học.
    public int Team; // Phe để lọc đối tượng.
    public byte Active; // 1: đối tượng hợp lệ trong snapshot.
    public float2 Center; // Tâm hitbox trong world space.
    public float2 HalfSize; // Nửa kích thước theo hai trục của hộp.
    public float2 AxisX; // Trục hộp trong world XY; hỗ trợ xoay quanh Z.
    public float2 AxisY; // Trục Y vuông góc với AxisX.

    public float2 BoundsHalfSize => math.abs(AxisX) * HalfSize.x + math.abs(AxisY) * HalfSize.y; // Nửa AABB bao hộp xoay, dùng chọn ô ứng viên.
}

#endregion

#region Query Input & Result

public enum SpatialQueryType : byte { None, Nearest, Sweep, AllInCircle } // Bỏ qua, gần nhất, quét đường bay, lấy mọi hitbox trong vòng tròn.

public struct SpatialQuery
{
    public SpatialQueryType Type; // Chọn phép kiểm tra cần thực hiện.
    public float2 Start; // Tâm vùng tìm hoặc điểm bắt đầu đường quét.
    public float2 End; // Điểm cuối; truy vấn tròn dùng cùng vị trí với Start.
    public float Radius; // Bán kính vùng tròn hoặc viên đạn.
    public int TargetTeam; // Chỉ nhận đối tượng thuộc phe này.
    internal int ExcludeIndex; // Bỏ qua đăng ký này, thường là bản thân người tìm.
    internal uint ExcludeVersion; // Chỉ bỏ qua đúng lượt đăng ký, không loại nhầm slot dùng lại.
}

public struct SpatialHit
{
    internal int Index; // Slot của đối tượng được tìm thấy.
    internal uint Version; // Phiên bản để main thread xác nhận kết quả còn hợp lệ.
    public byte Found; // 0: không có kết quả; 1: có ứng viên đạt điều kiện.
    public float Fraction; // Sweep: phần đường đã đi, từ 0 đến 1.
    public float2 Position; // Tâm viên đạn tại thời điểm trúng, giống centroid của CircleCast cũ.
}

#endregion
