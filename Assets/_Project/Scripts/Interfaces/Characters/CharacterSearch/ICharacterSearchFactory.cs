using UnityEngine;

#region Search Strategies

// Cách tìm một nhân vật; hitPosition là vị trí truy vấn chạm mục tiêu.
public interface ISingleCharacterSearch
{
    bool TryFind(CharacterSearchRequest request, out CharacterTarget target, out Vector2 hitPosition);
}

// Cách tìm nhiều nhân vật; mảng thuộc caller và chỉ tăng dung lượng khi thiếu chỗ.
public interface IMultipleCharacterSearch
{
    int FindAll(CharacterSearchRequest request, ref CharacterTarget[] results);
}

#endregion

// Chọn strategy đã tạo sẵn; không chạy truy vấn hoặc giữ kết quả tìm kiếm.
public interface ICharacterSearchFactory
{
    ISingleCharacterSearch GetSingle(CharacterSearchType type);
    IMultipleCharacterSearch GetMultiple(CharacterSearchType type);
}
