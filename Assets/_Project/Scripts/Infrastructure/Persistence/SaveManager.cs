using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Chỉ đọc/ghi theo key; service sở hữu dữ liệu và quy tắc nghiệp vụ.
public sealed class SaveManager : IInitializable, IDisposable
{
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

    private interface IPendingWrite
    {
        string Key { get; }
        void Write(ES3Settings storage);
    }

    private sealed class PendingWrite<T> : IPendingWrite
    {
        public string Key { get; }
        private readonly T snapshot;
        public PendingWrite(string key, T snapshot) { Key = key; this.snapshot = snapshot; }
        public void Write(ES3Settings storage) => ES3.Save(Key, snapshot, storage);
    }

    private readonly ES3Settings storage;
    private readonly Dictionary<string, IPendingWrite> pending = new Dictionary<string, IPendingWrite>(StringComparer.Ordinal);
    private bool disposed;
    public bool IsLoaded { get; private set; }
    public bool IsDirty => pending.Count > 0;
    public Exception LastError { get; private set; }

    public SaveManager(Settings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        storage = new ES3Settings(settings.FileName, ES3.Location.File, ES3.Directory.PersistentDataPath);
    }

    public void Initialize()
    {
        EnsureNotDisposed();
        try
        {
            // Đọc thử file trước khi cho các service ghi dữ liệu mặc định.
            if (ES3.FileExists(storage)) ES3.GetKeys(storage);
            IsLoaded = true;
            LastError = null;
        }
        catch (Exception exception) { BlockLoading(exception); }
    }

    public bool HasKey(string key)
    {
        EnsureAvailable();
        ValidateKey(key);
        try { return ES3.KeyExists(key, storage); }
        catch (Exception exception) { BlockLoading(exception); throw; }
    }

    public T Load<T>(string key, T defaultData, Action<T> validate = null)
    {
        EnsureAvailable();
        ValidateKey(key);
        if (pending.ContainsKey(key))
            throw new InvalidOperationException("Save pending changes before reloading key: " + key);
        try
        {
            T value = ES3.KeyExists(key, storage) ? ES3.Load<T>(key, storage) : defaultData;
            validate?.Invoke(value);
            return value;
        }
        catch (Exception exception) { BlockLoading(exception); throw; }
    }

    // Caller truyền snapshot độc lập; lỗi ghi giữ bản mới nhất theo key để retry.
    public bool Save<T>(string key, T snapshot)
    {
        EnsureAvailable();
        ValidateKey(key);
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        var write = new PendingWrite<T>(key, snapshot);
        pending[key] = write;
        return TryWrite(write);
    }

    private bool TryWrite(IPendingWrite write)
    {
        try
        {
            write.Write(storage);
            if (pending.TryGetValue(write.Key, out IPendingWrite latest) && ReferenceEquals(latest, write))
                pending.Remove(write.Key);
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            LastError = exception;
            Debug.LogError($"Save write failed for '{write.Key}': {exception.Message}");
            return false;
        }
    }

    // Pause/mất focus/thoát chỉ ghi lại các key còn lỗi.
    public bool SaveIfDirty()
    {
        if (disposed || !IsLoaded) return false;
        Exception failure = null;
        foreach (IPendingWrite write in new List<IPendingWrite>(pending.Values))
            if (!TryWrite(write)) failure = LastError;
        if (failure != null) LastError = failure;
        return !IsDirty;
    }

    // ResetSaveButton reload scene sau khi xóa, các service mới sẽ đọc mặc định.
    public bool ResetData()
    {
        EnsureNotDisposed();
        try { ES3.DeleteFile(storage); }
        catch (Exception exception) { LastError = exception; Debug.LogError($"Save reset failed: {exception.Message}"); return false; }
        pending.Clear();
        IsLoaded = true;
        LastError = null;
        return true;
    }

    internal void BlockLoading(Exception exception)
    {
        IsLoaded = false;
        LastError = exception;
        Debug.LogError($"Save load failed; writing is blocked: {exception.Message}");
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Save key must not be empty.", nameof(key));
    }

    private void EnsureAvailable()
    {
        EnsureNotDisposed();
        if (!IsLoaded) throw new InvalidOperationException("Save storage has not loaded successfully.");
    }

    private void EnsureNotDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(SaveManager));
    }

    public void Dispose()
    {
        if (disposed) return;
        SaveIfDirty();
        pending.Clear();
        disposed = true;
    }
}
