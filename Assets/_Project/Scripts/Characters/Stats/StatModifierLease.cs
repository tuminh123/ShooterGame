using System;

/// <summary>Quyền sở hữu một nhóm modifier; thay nguyên nhóm tại chỗ hoặc gỡ đúng nguồn.</summary>
/// <remarks>
/// Giữ thẳng bảng chỉ số, ID nguồn và version của lượt spawn (không closure), nên mỗi lần thêm nguồn chỉ tạo đúng object này.
/// Lease của lượt spawn cũ (version khác) không làm gì với nguồn của lượt spawn mới trên cùng runtime.
/// </remarks>
public sealed class StatModifierLease : IDisposable
{
    #region Ownership

    private StatSheet sheet; // null sau Dispose: Update/Dispose lặp lại không làm gì.
    private readonly long id;
    private readonly ulong version;

    internal StatModifierLease(StatSheet sheet, long id, ulong version)
    {
        this.sheet = sheet;
        this.id = id;
        this.version = version;
    }

    #endregion

    #region Modifier Lifecycle

    // Một skill/effect có thể tác động nhiều stat; runtime snapshot mảng mới và chỉ phát Changed một lần.
    public void Update(params StatModifier[] modifiers) => sheet?.Update(id, version, modifiers);

    /// <summary>Thay nguồn một modifier (buff một chỉ số): không tạo mảng <c>params</c>.</summary>
    public void Update(StatModifier modifier) => sheet?.Update(id, version, modifier);

    // Bỏ tham chiếu trước khi gỡ để cleanup lồng nhau an toàn.
    public void Dispose()
    {
        StatSheet owner = sheet;
        sheet = null;
        owner?.Remove(id, version);
    }

    #endregion
}
