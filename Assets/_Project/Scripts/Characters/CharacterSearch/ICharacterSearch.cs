using UnityEngine;

// Đầu vào tìm kiếm cho gameplay; caller không cần biết manager hoặc strategy cụ thể.
public interface ICharacterSearch
{
    #region Strategy Dispatch

    // Trả true khi tìm được một nhân vật còn hợp lệ.
    bool TryFind(CharacterSearchType type, CharacterSearchRequest request,
        out CharacterTarget target, out Vector2 hitPosition);

    // Caller giữ mảng để dùng lại; chỉ đọc từ 0 đến số lượng trả về, không đọc toàn bộ Length.
    int FindAll(CharacterSearchType type, CharacterSearchRequest request, ref CharacterTarget[] results);

    #endregion

    #region Convenience Queries

    CharacterTarget FindNearest(Vector2 center, float radius, CharacterTeam targetTeam, CharacterView exclude = null);
    int FindAll(Vector2 center, float radius, CharacterTeam targetTeam,
        ref CharacterTarget[] results, CharacterView exclude = null);
    CharacterTarget FindFirstOnPath(Vector2 origin, float radius, Vector2 direction,
        float distance, CharacterTeam targetTeam, out Vector2 hitPosition);

    #endregion
}
