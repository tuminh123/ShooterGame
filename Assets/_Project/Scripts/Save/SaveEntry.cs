using System;

/// <summary>
/// Một section cần ghi: key và snapshot độc lập của nó. Dùng để ghi nhiều section trong cùng một lần ghi file
/// (<see cref="ISaveStore.SaveBatch"/>), mỗi section một kiểu dữ liệu.
/// </summary>
/// <remarks>Giữ đúng kiểu T của snapshot để Easy Save ghi đúng kiểu; bên gọi tạo bằng <see cref="Of{T}"/>.</remarks>
public abstract class SaveEntry
{
    /// <summary>Key của section trong file save.</summary>
    public string Key { get; }

    private SaveEntry(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Save key must not be empty.", nameof(key));
        Key = key;
    }

    /// <summary>Tạo mục ghi; <paramref name="snapshot"/> phải là bản sao mà bên gọi không sửa tiếp.</summary>
    public static SaveEntry Of<T>(string key, T snapshot) => new Typed<T>(key, snapshot);

    /// <summary>Ghi snapshot vào file đang gom thay đổi (chỉ SaveStorage gọi).</summary>
    internal abstract void Write(ES3File file);

    private sealed class Typed<T> : SaveEntry
    {
        private readonly T snapshot;

        public Typed(string key, T snapshot) : base(key)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            this.snapshot = snapshot;
        }

        internal override void Write(ES3File file) => file.Save(Key, snapshot);
    }
}
