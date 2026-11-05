using UnityEngine;

#region Search Input

public enum CharacterSearchType { Nearest, AllInCircle, FirstOnPath } // Các cách tìm gameplay, độc lập loại job ở tầng Spatial.

// Yêu cầu trên main thread; strategy tự tạo query hình học, không truyền CharacterView vào job.
public readonly struct CharacterSearchRequest
{
    public readonly Vector2 Origin; // Tâm vùng hoặc điểm bắt đầu đường tìm.
    public readonly Vector2 End; // Điểm cuối đường tìm; tìm tròn dùng cùng Origin.
    public readonly float Radius; // Bán kính vùng hoặc viên đạn.
    public readonly CharacterTeam TargetTeam; // Phe cần tìm.
    public readonly CharacterView Exclude; // Bỏ qua chính mình nếu được chỉ định.

    public CharacterSearchRequest(Vector2 origin, Vector2 end, float radius, CharacterTeam targetTeam,
        CharacterView exclude = null)
    { Origin = origin; End = end; Radius = radius; TargetTeam = targetTeam; Exclude = exclude; }
}

#endregion

#region Strategies & Factory

// Marker để factory lưu chung các cách tìm, còn kết quả đơn/nhiều có hợp đồng riêng.
public interface ICharacterSearchStrategy { }

public interface ISingleCharacterSearch : ICharacterSearchStrategy
{
    bool TryFind(CharacterSearchRequest request, out CharacterTarget target, out Vector2 hitPosition);
}

public interface IMultipleCharacterSearch : ICharacterSearchStrategy
{
    // Caller sở hữu mảng; chỉ đọc [0, count). Mảng tự tăng nếu thiếu, không mượn ArrayPool.
    int FindAll(CharacterSearchRequest request, ref CharacterTarget[] results);
}

public interface ICharacterSearchFactory
{
    ISingleCharacterSearch GetSingle(CharacterSearchType type);
    IMultipleCharacterSearch GetMultiple(CharacterSearchType type);
}

#endregion

#region Character Search API

// Facade tiện dùng cho gameplay; có thể dùng request chung khi bổ sung strategy mới.
public interface ICharacterSearch
{
    bool TryFind(CharacterSearchType type, CharacterSearchRequest request, out CharacterTarget target, out Vector2 hitPosition);
    int FindAll(CharacterSearchType type, CharacterSearchRequest request, ref CharacterTarget[] results);
    CharacterTarget FindNearest(Vector2 center, float radius, CharacterTeam targetTeam, CharacterView exclude = null);
    int FindAll(Vector2 center, float radius, CharacterTeam targetTeam, ref CharacterTarget[] results, CharacterView exclude = null);
    CharacterTarget FindFirstOnPath(Vector2 origin, float radius, Vector2 direction, float distance,
        CharacterTeam targetTeam, out Vector2 hitPosition);
}

#endregion
