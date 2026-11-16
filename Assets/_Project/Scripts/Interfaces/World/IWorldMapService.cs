#region WorldMap

/// <summary>Đọc và áp dụng map nền theo khóa cấu hình.</summary>
public interface IWorldMap
{
    string CurrentMapKey { get; }
    bool SetMap(string key);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của GroundChunkManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IWorldMapService
{
    IWorldMap GetWorldMap();
}

#endregion
