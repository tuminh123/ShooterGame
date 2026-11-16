using System;

#region SaveReader

/// <summary>Đọc dữ liệu theo key và trạng thái nạp bộ lưu.</summary>
public interface ISaveReader
{
    bool IsLoaded { get; }
    Exception LastError { get; }
    bool HasKey(string key);
    T Load<T>(string key, T defaultData, Action<T> validate = null);
}

#endregion

#region SaveWriter

/// <summary>Ghi snapshot độc lập; false nghĩa là cần thử ghi lại, không áp dụng lại nghiệp vụ.</summary>
public interface ISaveWriter
{
    bool Save<T>(string key, T snapshot);
    bool SaveBatch<TFirst, TSecond>(string firstKey, TFirst first, string secondKey, TSecond second);
}

#endregion

#region SaveFlush

/// <summary>Thử ghi lại snapshot đang chờ mà không thay đổi nghiệp vụ.</summary>
public interface ISaveFlush
{
    bool IsDirty { get; }
    bool SaveIfDirty();
}

#endregion

#region SaveReset

/// <summary>Xóa bộ lưu theo yêu cầu reset hồ sơ.</summary>
public interface ISaveReset
{
    bool ResetData();
}

#endregion

#region SaveLoadFailure

/// <summary>Khóa ghi dữ liệu sau khi nạp hoặc chuyển phiên bản thất bại.</summary>
public interface ISaveLoadFailure
{
    void BlockLoading(Exception exception);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của SaveManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ISaveService
{
    ISaveReader GetSaveReader();
    ISaveWriter GetSaveWriter();
    ISaveFlush GetSaveFlush();
    ISaveReset GetSaveReset();
    ISaveLoadFailure GetSaveLoadFailure();
}

#endregion
