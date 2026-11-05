using System;
using System.Collections.Generic;
using UnityEngine;

// Chỉ đăng ký và chọn cách tìm nhân vật; thuật toán nằm trong CharacterSearchStrategies.
public sealed class CharacterSearchFactory : ICharacterSearchFactory
{
    #region Strategy Registry

    private readonly Dictionary<CharacterSearchType, ISingleCharacterSearch> singleSearches; // Các cách trả một mục tiêu.
    private readonly Dictionary<CharacterSearchType, IMultipleCharacterSearch> multipleSearches; // Các cách trả nhiều mục tiêu.

    // Mỗi strategy được tạo một lần; tất cả truy cập Spatial qua interface.
    public CharacterSearchFactory(ISpatialManager spatial, ISpatialSearchFactory queries)
    {
        singleSearches = new Dictionary<CharacterSearchType, ISingleCharacterSearch>
        {
            { CharacterSearchType.Nearest, new FindNearestSearch(spatial, queries) },
            { CharacterSearchType.FirstOnPath, new FindFirstOnPathSearch(spatial, queries) }
        };
        multipleSearches = new Dictionary<CharacterSearchType, IMultipleCharacterSearch>
        {
            { CharacterSearchType.AllInCircle, new FindAllInCircleSearch(spatial, queries) }
        };
    }

    #endregion

    #region Strategy Selection

    // Trả đúng hợp đồng một mục tiêu, không cần ép kiểu từ interface rỗng.
    public ISingleCharacterSearch GetSingle(CharacterSearchType type)
    {
        if (singleSearches.TryGetValue(type, out ISingleCharacterSearch strategy)) return strategy;
        throw new ArgumentOutOfRangeException(nameof(type), type, "No single-target search is registered for this type.");
    }

    // Chặn gọi nhầm kiểu tìm một mục tiêu bằng API nhiều mục tiêu.
    public IMultipleCharacterSearch GetMultiple(CharacterSearchType type)
    {
        if (multipleSearches.TryGetValue(type, out IMultipleCharacterSearch strategy)) return strategy;
        throw new ArgumentOutOfRangeException(nameof(type), type, "No multiple-target search is registered for this type.");
    }

    #endregion
}
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
