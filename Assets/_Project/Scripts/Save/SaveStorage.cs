using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đọc/ghi Easy Save 3 theo key, giữ snapshot ghi lỗi để thử lại. Không biết dữ liệu thuộc nghiệp vụ nào.
/// </summary>
/// <remarks>Do <see cref="SaveManager"/> tạo; không bind vào Zenject.</remarks>
public sealed class SaveStorage
{
    #region Pending Writes

    // Snapshot đã nhận nhưng chưa ghi được. Chỉ giữ bản mới nhất theo key: retry ghi trạng thái cuối, không phát lại từng thao tác.
    private readonly Dictionary<string, SaveEntry> pending = new Dictionary<string, SaveEntry>(StringComparer.Ordinal);

    #endregion

    #region Fields / Properties

    private readonly ES3Settings settings;

    /// <summary>File đã đọc được; false thì mọi lệnh đọc/ghi bị khóa.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Còn snapshot chưa ghi được.</summary>
    public bool IsDirty => pending.Count > 0;

    /// <summary>Lỗi đọc/ghi gần nhất.</summary>
    public Exception LastError { get; private set; }

    #endregion

    #region Constructor / Open

    /// <summary>Cấu hình file trong PersistentDataPath; tên file không được chứa thư mục.</summary>
    public SaveStorage(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || System.IO.Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Provide a save file name without directories.", nameof(fileName));
        settings = new ES3Settings(fileName, ES3.Location.File, ES3.Directory.PersistentDataPath);
    }

    /// <summary>Đọc thử danh sách key trước khi cho ghi dữ liệu mặc định; file hỏng thì khóa ghi.</summary>
    public void Open()
    {
        try
        {
            if (ES3.FileExists(settings)) ES3.GetKeys(settings);
            IsLoaded = true;
            LastError = null;
        }
        catch (Exception exception) { BlockLoading(exception); }
    }

    #endregion

    #region Read

    /// <summary>Key đã có trong file chưa (dùng cho migration).</summary>
    public bool HasKey(string key)
    {
        EnsureLoaded();
        ValidateKey(key);
        try { return ES3.KeyExists(key, settings); }
        catch (Exception exception) { BlockLoading(exception); throw; }
    }

    /// <summary>Đọc key hoặc trả mặc định; dữ liệu sai thì khóa ghi để không đè lên file đang lỗi.</summary>
    public T Load<T>(string key, T defaultData, Action<T> validate = null)
    {
        EnsureLoaded();
        ValidateKey(key);
        // Đọc lại key đang chờ ghi sẽ trả về dữ liệu cũ trên đĩa, lệch với RAM.
        if (pending.ContainsKey(key))
            throw new InvalidOperationException("Save pending changes before reloading key: " + key);
        try
        {
            T value = ES3.KeyExists(key, settings) ? ES3.Load<T>(key, settings) : defaultData;
            validate?.Invoke(value);
            return value;
        }
        catch (Exception exception) { BlockLoading(exception); throw; }
    }

    #endregion

    #region Write

    /// <summary>Xếp snapshot vào hàng chờ rồi ghi; lỗi thì giữ để thử lại.</summary>
    public bool Save<T>(string key, T snapshot)
    {
        EnsureLoaded();
        pending[key] = SaveEntry.Of(key, snapshot);
        return TryWritePending();
    }

    /// <summary>Xếp mọi section trước khi chạm đĩa; lần thử lại sau lỗi cũng ghi đủ.</summary>
    public bool SaveBatch(IReadOnlyList<SaveEntry> entries)
    {
        EnsureLoaded();
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (SaveEntry entry in entries)
            if (entry == null || !keys.Add(entry.Key))
                throw new ArgumentException("Batch entries must not be null and keys must be distinct.", nameof(entries));
        foreach (SaveEntry entry in entries) pending[entry.Key] = entry;
        return TryWritePending();
    }

    /// <summary>Thử ghi lại phần đang chờ; không làm gì nếu file chưa đọc được.</summary>
    public bool SaveIfDirty() => IsLoaded && TryWritePending();

    /// <summary>Xóa file save; bên gọi reload scene để các system đọc lại mặc định.</summary>
    public bool ResetData()
    {
        try { ES3.DeleteFile(settings); }
        catch (Exception exception)
        {
            LastError = exception;
            Debug.LogError($"Save reset failed: {exception.Message}");
            return false;
        }
        pending.Clear();
        IsLoaded = true;
        LastError = null;
        return true;
    }

    /// <summary>Khóa ghi sau khi đọc hoặc migration lỗi, để không đè lên file đang hỏng.</summary>
    public void BlockLoading(Exception exception)
    {
        IsLoaded = false;
        LastError = exception;
        Debug.LogError($"Save load failed; writing is blocked: {exception.Message}");
    }

    /// <summary>Bỏ hàng chờ khi scene kết thúc (sau lần thử ghi cuối của SaveManager).</summary>
    public void ClearPending() => pending.Clear();

    #endregion

    #region Private Helpers

    private bool TryWritePending()
    {
        if (pending.Count == 0) return true;
        var writes = new List<SaveEntry>(pending.Values);
        try
        {
            // Giữ các key đã có, gom thay đổi trong RAM rồi ghi file một lần.
            var file = new ES3File(settings);
            foreach (SaveEntry write in writes) write.Write(file);
            file.Sync();
            // Chỉ gỡ đúng snapshot vừa ghi: callback có thể đã xếp bản mới hơn cho cùng key.
            foreach (SaveEntry write in writes)
                if (pending.TryGetValue(write.Key, out SaveEntry latest) && ReferenceEquals(latest, write))
                    pending.Remove(write.Key);
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            LastError = exception;
            Debug.LogError($"Save write failed: {exception.Message}");
            return false;
        }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Save key must not be empty.", nameof(key));
    }

    private void EnsureLoaded()
    {
        if (!IsLoaded) throw new InvalidOperationException("Save storage has not loaded successfully.");
    }

    #endregion
}
