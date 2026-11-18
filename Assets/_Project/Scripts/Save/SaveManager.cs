using System;
using Zenject;

/// <summary>
/// Manager của miền Save: mở file save, chạy migration, rồi cho các miền khác đọc/ghi snapshot theo key.
/// Đây là class <b>duy nhất</b> của Save được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// SaveManager
/// ├── SaveStorage     Easy Save 3: đọc/ghi theo key, giữ snapshot lỗi để thử lại
/// └── SaveMigration   schema cũ → schema 4 (chạy một lần trong Initialize)
/// </code>
/// Initialize chạy đầu tiên (-200): mở file rồi migration ngay, nên khi Progression đọc dữ liệu (-160)
/// file đã ở schema mới. Trước đây SaveMigration là binding riêng (-180).
/// Miền khác chỉ thấy <c>SaveContracts.cs</c>.
/// </remarks>
public sealed class SaveManager : ISaveStore, ISaveMaintenance, IInitializable, IDisposable
{
    #region Settings

    /// <summary>Tên file save (không chứa thư mục); installer truyền vào.</summary>
    public sealed class Settings
    {
        public readonly string FileName;

        public Settings(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || System.IO.Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("Provide a save file name without directories.", nameof(fileName));
            FileName = fileName;
        }
    }

    #endregion

    #region Fields / Properties

    private readonly SaveStorage storage;
    private readonly SaveMigration migration;
    private bool disposed;

    public bool IsLoaded => !disposed && storage.IsLoaded;
    public Exception LastError => storage.LastError;

    #endregion

    #region Constructor / Lifecycle

    public SaveManager(Settings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        storage = new SaveStorage(settings.FileName);
        migration = new SaveMigration(storage);
    }

    /// <summary>Mở file và migration. Lỗi ở bước nào cũng khóa ghi; GameManager sẽ báo lỗi khi thấy IsLoaded = false.</summary>
    public void Initialize()
    {
        EnsureNotDisposed();
        storage.Open();
        migration.Run();
    }

    /// <summary>Thử ghi lần cuối phần đang chờ rồi khóa.</summary>
    public void Dispose()
    {
        if (disposed) return;
        storage.SaveIfDirty();
        storage.ClearPending();
        disposed = true;
    }

    #endregion

    #region ISaveStore

    /// <inheritdoc/>
    public T Load<T>(string key, T defaultData, Action<T> validate = null)
    {
        EnsureNotDisposed();
        return storage.Load(key, defaultData, validate);
    }

    /// <inheritdoc/>
    public bool Save<T>(string key, T snapshot)
    {
        EnsureNotDisposed();
        return storage.Save(key, snapshot);
    }

    /// <inheritdoc/>
    public bool SaveBatch<TFirst, TSecond>(string firstKey, TFirst first, string secondKey, TSecond second)
    {
        EnsureNotDisposed();
        return storage.SaveBatch(firstKey, first, secondKey, second);
    }

    #endregion

    #region ISaveMaintenance

    /// <inheritdoc/>
    public bool SaveIfDirty() => !disposed && storage.SaveIfDirty();

    /// <inheritdoc/>
    public bool ResetData()
    {
        EnsureNotDisposed();
        return storage.ResetData();
    }

    #endregion

    #region Private Helpers

    private void EnsureNotDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(SaveManager));
    }

    #endregion
}
