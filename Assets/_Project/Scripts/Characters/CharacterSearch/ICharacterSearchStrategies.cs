using UnityEngine;

// Strategy tìm nhân vật (họ có nhiều implementation trong CharacterSearchStrategies.cs).
// CharacterSearchFactory chọn strategy theo CharacterSearchType; nơi dùng không biết strategy cụ thể.

/// <summary>Strategy tìm <b>một</b> nhân vật (gần nhất, trúng đầu tiên trên đường quét).</summary>
public interface ISingleCharacterSearch
{
    /// <param name="hitPosition">Vị trí mà truy vấn chạm mục tiêu (với đường quét là tâm đạn tại điểm trúng).</param>
    bool TryFind(CharacterSearchRequest request, out CharacterHandle target, out Vector2 hitPosition);
}

/// <summary>Strategy tìm <b>nhiều</b> nhân vật (mọi nhân vật trong vòng tròn).</summary>
public interface IMultipleCharacterSearch
{
    /// <summary>Ghi kết quả vào mảng của bên gọi; mảng chỉ được cấp phát lại khi không đủ chỗ. Trả về số phần tử hợp lệ.</summary>
    int FindAll(CharacterSearchRequest request, ref CharacterHandle[] results);
}
