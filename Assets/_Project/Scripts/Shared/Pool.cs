using System;
using System.Collections.Generic;

#region IPoolable

/// <summary>
/// Object thuần (không phải GameObject) được dùng lại qua <see cref="Pool{T}"/>.
/// </summary>
/// <remarks>
/// Chỉ class <b>thật sự được pool</b> mới implement: object tạo đi tạo lại trong lúc chơi (theo quái, theo đòn, theo effect)
/// và Profiler cho thấy đáng kể. Manager, System và runtime tạo một lần không implement.
/// Không gộp với <see cref="IDisposable"/>: Dispose là hết đời, còn trả về pool là cất để dùng lại.
/// Khởi tạo không nằm ở đây: mỗi loại có hàm khởi tạo riêng với tham số đúng kiểu, Manager gọi ngay sau <see cref="Pool{T}.Rent"/>.
/// </remarks>
public interface IPoolable
{
    /// <summary>Dọn sạch trạng thái của lượt dùng trước (gỡ event, xóa list, trả tham chiếu về null). Pool gọi khi nhận lại.</summary>
    void ResetForPool();
}

#endregion

#region Pool

/// <summary>
/// Pool cho object thuần: <see cref="Rent"/> lấy từ Stack (hết thì tạo mới), <see cref="Return"/> gọi
/// <see cref="IPoolable.ResetForPool"/> rồi cất lại. Do Manager sở hữu tạo bằng <c>new</c>; không bind vào Zenject.
/// </summary>
/// <remarks>
/// Một pool chỉ chứa đúng một kiểu: họ có nhiều lớp con thì mỗi lớp con một pool (một object tạo ra là lớp con nào thì mãi là lớp đó).
/// Pool biết object nào đang cho mượn, nên trả hai lần hoặc trả object không phải của pool đều bị từ chối (O(1)).
/// </remarks>
public sealed class Pool<T> where T : class, IPoolable
{
    #region Fields

    private readonly Func<T> create;
    private readonly Stack<T> available = new Stack<T>();
    private readonly HashSet<T> leased = new HashSet<T>();
    private int created;

    #endregion

    #region Constructor

    /// <summary><paramref name="create"/> tạo một object mới khi pool hết (gọi lúc prewarm hoặc thiếu).</summary>
    public Pool(Func<T> create) => this.create = create ?? throw new ArgumentNullException(nameof(create));

    #endregion

    #region State

    /// <summary>Số object đang rảnh trong pool.</summary>
    public int AvailableCount => available.Count;

    /// <summary>Số object đang cho mượn.</summary>
    public int LeasedCount => leased.Count;

    /// <summary>Tổng số object pool đã tạo (rảnh + đang mượn).</summary>
    public int CreatedCount => created;

    /// <summary>Object có đang được cho mượn từ pool này không.</summary>
    public bool IsLeased(T item) => item != null && leased.Contains(item);

    #endregion

    #region Rent And Return

    /// <summary>Bảo đảm có sẵn ít nhất <paramref name="count"/> object rảnh; chỉ tạo phần còn thiếu.</summary>
    public void Prewarm(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        leased.EnsureCapacity(checked(leased.Count + count)); // Thuê hết số đã prewarm cũng không phải nới bảng.
        while (available.Count < count) available.Push(CreateNew());
    }

    /// <summary>Lấy một object rảnh (hết thì tạo mới). Bên gọi khởi tạo nó bằng hàm riêng của kiểu đó.</summary>
    public T Rent()
    {
        T item = available.Count > 0 ? available.Pop() : CreateNew();
        leased.Add(item);
        return item;
    }

    /// <summary>
    /// Dọn object (<see cref="IPoolable.ResetForPool"/>) rồi cất lại. Trả false và không làm gì nếu object không đang
    /// được pool này cho mượn (trả hai lần, hoặc object tạo ngoài pool): bên gọi quyết định đó là lỗi hay bỏ qua.
    /// </summary>
    public bool Return(T item)
    {
        if (item == null || !leased.Remove(item)) return false;
        try { item.ResetForPool(); }
        finally { available.Push(item); } // Reset lỗi vẫn giữ object trong pool, không làm lệch số đếm.
        return true;
    }

    /// <summary>Dọn mọi object (cả đang mượn) rồi bỏ hết; dùng khi Manager sở hữu Dispose.</summary>
    public void Clear()
    {
        foreach (T item in leased) item.ResetForPool();
        foreach (T item in available) item.ResetForPool();
        leased.Clear();
        available.Clear();
        created = 0;
    }

    private T CreateNew()
    {
        T item = create() ?? throw new InvalidOperationException("Pool factory returned null for " + typeof(T).Name + ".");
        created++;
        return item;
    }

    #endregion
}

#endregion
