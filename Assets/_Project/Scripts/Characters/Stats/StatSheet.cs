using System;
using System.Collections.Generic;

/// <summary>
/// Bảng chỉ số của một lượt spawn: chỉ số gốc từ template, cộng mọi <b>nguồn bonus</b> (bonus lúc spawn, level,
/// trang bị, buff skill, slow của effect, tăng HP theo wave…). Công thức nằm ở một chỗ duy nhất: <see cref="StatCalculator"/>.
/// </summary>
/// <remarks>
/// Mọi nguồn bonus, dù vĩnh viễn hay tạm thời, đều được thêm theo <b>một cách duy nhất</b>: <see cref="AddSource"/>,
/// và nhận về một <see cref="StatModifierLease"/> để cập nhật hoặc gỡ đúng nguồn đó.
/// <para>
/// Sheet không giữ HP/giáp. Mỗi khi nguồn đổi, nó gọi lại chủ sở hữu (<see cref="CharacterRuntimeData"/>) để tính
/// chỉ số cuối và phát Changed. Nếu bước tính lỗi (ví dụ chỉ số ra NaN), thay đổi vừa rồi được hoàn tác và lỗi được ném lại.
/// </para>
/// </remarks>
public sealed class StatSheet
{
    #region State

    private readonly Dictionary<long, StatModifier[]> sources = new Dictionary<long, StatModifier[]>(8); // Mỗi nguồn có ID riêng.
    private readonly Action onChanged; // Chủ sở hữu tính lại chỉ số cuối rồi phát Changed; có thể ném lỗi.
    private long nextId;   // Không dùng lại ID trong cùng một lượt spawn.
    private ulong version; // Tăng khi bảng được reset; lease của lượt spawn cũ thấy version khác và không làm gì.

    /// <summary>Chỉ số gốc lấy từ template của lượt spawn.</summary>
    public BaseStats Base { get; private set; }

    #endregion

    #region Lifecycle

    internal StatSheet(Action onChanged) => this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

    /// <summary>
    /// Bắt đầu lượt spawn mới với chỉ số gốc và bonus lúc spawn. Bonus lúc spawn là một nguồn như mọi nguồn khác,
    /// tồn tại suốt lượt spawn. Không gọi lại chủ sở hữu; bên gọi tự tính chỉ số sau khi khởi tạo xong.
    /// </summary>
    internal void Reset(BaseStats baseStats, StatBonus spawnBonus)
    {
        Clear();
        Base = baseStats;
        sources.Add(++nextId, spawnBonus.ToModifiers());
    }

    /// <summary>Xóa mọi nguồn và vô hiệu hóa mọi lease đang có (khi runtime trả về pool).</summary>
    internal void Clear()
    {
        sources.Clear();
        nextId = 0;
        version = checked(version + 1);
        Base = default;
    }

    #endregion

    #region Sources

    /// <summary>
    /// Thêm một nguồn bonus (một hoặc nhiều chỉ số trong cùng thao tác, chỉ tính lại một lần).
    /// Giữ lease để cập nhật (<see cref="StatModifierLease.Update"/>) hoặc gỡ (<see cref="StatModifierLease.Dispose"/>) đúng nguồn này.
    /// </summary>
    public StatModifierLease AddSource(params StatModifier[] modifiers)
    {
        StatModifier[] snapshot = Snapshot(modifiers);
        ulong leaseVersion = version;
        long id = checked(++nextId);
        sources.Add(id, snapshot);
        try { onChanged(); }
        catch { sources.Remove(id); throw; }
        return new StatModifierLease(
            () => Remove(id, leaseVersion),
            next => Update(id, leaseVersion, next));
    }

    /// <summary>Chỉ số sau khi cộng mọi nguồn (chưa gồm HP/giáp được cấp trực tiếp, phần đó do runtime cộng).</summary>
    internal BaseStats Calculate() => StatCalculator.Calculate(Base, sources.Values);

    // Thay toàn bộ modifier của cùng nguồn tại chỗ, không có khoảnh khắc mất buff ở giữa.
    private void Update(long id, ulong leaseVersion, StatModifier[] modifiers)
    {
        if (leaseVersion != version || !sources.TryGetValue(id, out StatModifier[] previous)) return;
        sources[id] = Snapshot(modifiers);
        try { onChanged(); }
        catch { sources[id] = previous; throw; }
    }

    // Lease của lượt spawn cũ (version khác) không được gỡ nguồn của lượt spawn mới trên cùng runtime.
    private void Remove(long id, ulong leaseVersion)
    {
        if (leaseVersion != version || !sources.TryGetValue(id, out StatModifier[] previous)) return;
        sources.Remove(id);
        try { onChanged(); }
        catch { sources.Add(id, previous); throw; }
    }

    // Bên gọi có thể sửa mảng của nó sau đó (ví dụ config trong Inspector); sheet giữ bản sao đã kiểm tra.
    private static StatModifier[] Snapshot(StatModifier[] modifiers)
    {
        if (modifiers == null || modifiers.Length == 0) throw new ArgumentException("A stat source must contain at least one modifier.");
        var snapshot = (StatModifier[])modifiers.Clone();
        foreach (StatModifier modifier in snapshot) modifier.Validate();
        return snapshot;
    }

    #endregion
}
