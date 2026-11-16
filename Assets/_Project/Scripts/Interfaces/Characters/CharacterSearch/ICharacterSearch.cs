using UnityEngine;

#region CharacterSearchDispatch

/// <summary>Định tuyến tìm một mục tiêu theo loại truy vấn gameplay.</summary>
public interface ICharacterSearchDispatch
{
    bool TryFind(CharacterSearchType type, CharacterSearchRequest request, out CharacterTarget target, out Vector2 hitPosition);
}

#endregion

#region CharacterRegionSearch

/// <summary>Tìm nhiều nhân vật vào buffer do caller sở hữu.</summary>
public interface ICharacterRegionSearch
{
    int FindAll(CharacterSearchType type, CharacterSearchRequest request, ref CharacterTarget[] results);
    int FindAll(Vector2 center, float radius, CharacterTeam targetTeam, ref CharacterTarget[] results, CharacterView exclude = null);
}

#endregion

#region CharacterTargetSearch

/// <summary>Tìm mục tiêu gần nhất hoặc mục tiêu đầu tiên trên đường.</summary>
public interface ICharacterTargetSearch
{
    CharacterTarget FindNearest(Vector2 center, float radius, CharacterTeam targetTeam, CharacterView exclude = null);
    CharacterTarget FindFirstOnPath(Vector2 origin, float radius, Vector2 direction, float distance, CharacterTeam targetTeam, out Vector2 hitPosition);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của CharacterSearch; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ICharacterSearch
{
    ICharacterSearchDispatch GetCharacterSearchDispatch();
    ICharacterRegionSearch GetCharacterRegionSearch();
    ICharacterTargetSearch GetCharacterTargetSearch();
}

#endregion
