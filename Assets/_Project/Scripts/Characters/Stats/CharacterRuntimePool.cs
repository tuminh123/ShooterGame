using System;

/// <summary>Pool runtime của nhân vật: cho thuê <see cref="CharacterRuntimeData"/> mỗi lượt spawn và nhận lại khi lượt đó kết thúc.</summary>
/// <remarks>
/// Runtime giữ lại dictionary/collection để dùng lại giữa các lượt. Do <see cref="CharacterManager"/> tạo; không bind vào Zenject.
/// Phần cất/lấy/chặn trả hai lần dùng <see cref="Pool{T}"/> chung; lớp này thêm khởi tạo theo template và kiểm tra Generation.
/// </remarks>
public sealed class CharacterRuntimePool : IDisposable
{
    #region Dependencies And State

    private readonly Pool<CharacterRuntimeData> pool = new Pool<CharacterRuntimeData>(() => new CharacterRuntimeData());
    private bool disposed;

    /// <summary>Số runtime đang rảnh.</summary>
    public int AvailableCount => pool.AvailableCount;

    /// <summary>Số runtime đang cho thuê (nhân vật đang sống trên sân).</summary>
    public int ActiveCount => pool.LeasedCount;

    /// <summary>Tổng số runtime đã tạo.</summary>
    public int CreatedCount => pool.CreatedCount;

    #endregion

    #region Creation

    /// <summary>Bảo đảm có sẵn ít nhất <paramref name="count"/> runtime rảnh (GamePrewarm gọi trước trận).</summary>
    public void Prewarm(int count)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterRuntimePool));
        pool.Prewarm(count);
    }

    /// <summary>Thuê một runtime và khởi tạo theo template; thiếu thì pool tự tạo thêm, không bỏ mất spawn.</summary>
    public CharacterRuntimeData Rent(CharacterDataSO template, StatBonus bonus)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterRuntimePool));
        CharacterRuntimeData runtime = pool.Rent();
        try { runtime.InitializeSpawn(template, bonus); }
        catch
        {
            pool.Return(runtime); // Khởi tạo lỗi: dọn và cất lại ngay, không để lọt runtime dở dang.
            throw;
        }
        return runtime;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Trả runtime của đúng lượt spawn <paramref name="generation"/>. False nếu đã trả, sai lượt, hoặc runtime do caller tự new
    /// (factory không nhận quyền sở hữu object bên ngoài).
    /// </summary>
    public bool TryReturn(CharacterRuntimeData runtime, ulong generation)
    {
        if (disposed || runtime == null || runtime.Generation != generation) return false;
        return pool.Return(runtime);
    }

    /// <summary>Dọn mọi runtime (cả đang thuê) khi đóng scene.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pool.Clear();
    }

    #endregion
}
