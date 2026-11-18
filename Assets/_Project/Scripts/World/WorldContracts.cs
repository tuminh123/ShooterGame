// Ranh giới của miền World (map nền). Mọi interface trong file này do WorldManager implement.
// World nằm trên Character (đọc vị trí Player để dựng nền quanh nhân vật) và dưới Stage (Stage chọn map của màn).
// World không biết wave, boss hay điều kiện thắng; Stage gọi xuống, World không gọi ngược lên.

#region Map

/// <summary>Chọn map nền theo key cấu hình. Dùng bởi StageManager khi bắt đầu một lượt chơi.</summary>
public interface IWorldMap
{
    /// <summary>Key của map đang dùng; <c>null</c> khi chưa đặt map nào.</summary>
    string CurrentMapKey { get; }

    /// <summary>Đổi nền sang map của <paramref name="key"/>. Key không hợp lệ thì giữ map hiện tại và trả về false.</summary>
    bool SetMap(string key);
}

#endregion
