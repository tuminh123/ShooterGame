using System;

// Ranh giới của miền Save: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do SaveManager implement. SaveStorage (Easy Save 3) và SaveMigration
// nằm bên trong miền, không được bind vào Zenject.
// Save là tầng thấp nhất: chỉ đọc/ghi snapshot theo key, không biết dữ liệu nào thuộc nghiệp vụ nào.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region Store (Progression, PlayerSystem, GameManager)

/// <summary>
/// Đọc/ghi snapshot theo key. Dùng bởi các system của Progression, PlayerSystem (nhân vật đang chọn)
/// và GameManager (chỉ kiểm tra <see cref="IsLoaded"/> trước khi vào game).
/// </summary>
public interface ISaveStore
{
    /// <summary>File save đã đọc được và migration (nếu có) đã xong. false thì mọi lệnh ghi bị khóa để bảo vệ file.</summary>
    bool IsLoaded { get; }

    /// <summary>Lỗi đọc/ghi gần nhất; null nếu lần gần nhất thành công.</summary>
    Exception LastError { get; }

    /// <summary>Đọc key; chưa có thì trả <paramref name="defaultData"/>. <paramref name="validate"/> ném lỗi nếu dữ liệu sai.</summary>
    T Load<T>(string key, T defaultData, Action<T> validate = null);

    /// <summary>
    /// Ghi một snapshot độc lập (bên gọi tự Clone). false nghĩa là IO lỗi: snapshot được giữ để thử lại,
    /// bên gọi không được áp dụng lại nghiệp vụ (cộng tiền, tăng cấp…).
    /// </summary>
    bool Save<T>(string key, T snapshot);

    /// <summary>Ghi hai section trong cùng một lần ghi file (ví dụ ví + mốc vượt màn), để không lệch nhau khi lỗi.</summary>
    bool SaveBatch<TFirst, TSecond>(string firstKey, TFirst first, string secondKey, TSecond second);
}

#endregion

#region Maintenance (installer, nút reset debug)

/// <summary>Thử ghi lại phần đang chờ, hoặc xóa hồ sơ. Dùng bởi GameSceneInstaller (pause/mất focus/thoát) và ResetSaveButton.</summary>
public interface ISaveMaintenance
{
    /// <summary>Ghi lại snapshot đang chờ (nếu có) mà không áp dụng lại nghiệp vụ.</summary>
    bool SaveIfDirty();

    /// <summary>Xóa file save. Bên gọi reload scene sau đó để các system đọc lại dữ liệu mặc định.</summary>
    bool ResetData();
}

#endregion
