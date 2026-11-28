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

    private const int MaxSpareSingles = 16;

    private readonly Dictionary<long, StatModifier[]> sources = new Dictionary<long, StatModifier[]>(8); // Mỗi nguồn có ID riêng.
    // Mảng 1 phần tử của nguồn đã gỡ (slow, buff một chỉ số): dùng lại cho nguồn sau. Chỉ sheet giữ các mảng này.
    private readonly Stack<StatModifier[]> spareSingles = new Stack<StatModifier[]>();
    private StatModifier[] spawnGroup; // Nguồn bonus lúc spawn của sheet; ghi đè mỗi lượt (chỉ sheet giữ mảng này).
    private readonly Action onChanged; // Chủ sở hữu tính lại chỉ số cuối rồi phát Changed; có thể ném lỗi.
    private long nextId; // Không dùng lại ID trong cùng một lượt spawn.
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
        // Nguồn bonus lúc spawn luôn có cùng 8 modifier: ghi đè vào mảng của sheet thay vì tạo mảng mới mỗi lượt spawn.
        if (spawnGroup == null) spawnGroup = new StatModifier[StatBonus.ModifierCount];
        spawnBonus.WriteModifiers(spawnGroup);
        sources.Add(++nextId, spawnGroup);
    }

    /// <summary>Xóa mọi nguồn và vô hiệu hóa mọi lease đang có (khi runtime trả về pool).</summary>
    internal void Clear()
    {
        foreach (StatModifier[] group in sources.Values) Recycle(group);
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
    public StatModifierLease AddSource(params StatModifier[] modifiers) => Add(Snapshot(modifiers));

    /// <summary>
    /// Thêm nguồn một modifier (slow, buff một chỉ số). Không tạo mảng <c>params</c> và dùng lại mảng của nguồn đã gỡ:
    /// mỗi lần chỉ tạo đúng lease trả về.
    /// </summary>
    public StatModifierLease AddSource(StatModifier modifier) => Add(Single(modifier));

    private StatModifierLease Add(StatModifier[] snapshot)
    {
        ulong leaseVersion = version;
        long id = checked(++nextId);
        sources.Add(id, snapshot);
        try { onChanged(); }
        catch
        {
            sources.Remove(id);
            Recycle(snapshot);
            throw;
        }
        return new StatModifierLease(this, id, leaseVersion);
    }

    /// <summary>
    /// Chỉ số sau khi cộng mọi nguồn (chưa gồm HP/giáp được cấp trực tiếp, phần đó do runtime cộng),
    /// và nhóm bonus chiến đấu tính từ cùng các nguồn đó.
    /// </summary>
    internal BaseStats Calculate(out CombatBonuses bonuses) => StatCalculator.Calculate(Base, sources.Values, out bonuses);

    // Thay toàn bộ modifier của cùng nguồn tại chỗ, không có khoảnh khắc mất buff ở giữa.
    internal void Update(long id, ulong leaseVersion, StatModifier[] modifiers)
    {
        if (leaseVersion != version || !sources.ContainsKey(id)) return;
        Replace(id, Snapshot(modifiers));
    }

    internal void Update(long id, ulong leaseVersion, StatModifier modifier)
    {
        if (leaseVersion != version || !sources.ContainsKey(id)) return;
        Replace(id, Single(modifier));
    }

    private void Replace(long id, StatModifier[] next)
    {
        StatModifier[] previous = sources[id];
        sources[id] = next;
        try { onChanged(); }
        catch
        {
            sources[id] = previous;
            Recycle(next);
            throw;
        }
        Recycle(previous);
    }

    // Lease của lượt spawn cũ (version khác) không được gỡ nguồn của lượt spawn mới trên cùng runtime.
    internal void Remove(long id, ulong leaseVersion)
    {
        if (leaseVersion != version || !sources.TryGetValue(id, out StatModifier[] previous)) return;
        sources.Remove(id);
        try { onChanged(); }
        catch { sources.Add(id, previous); throw; }
        Recycle(previous);
    }

    // Mảng 1 phần tử: lấy từ kho nếu có, kiểm tra modifier trước khi nhận.
    private StatModifier[] Single(StatModifier modifier)
    {
        modifier.Validate();
        StatModifier[] single = spareSingles.Count > 0 ? spareSingles.Pop() : new StatModifier[1];
        single[0] = modifier;
        return single;
    }

    // Chỉ cất mảng 1 phần tử (loại tạo nhiều nhất); mảng nhiều phần tử (bonus level, trang bị) hiếm khi đổi.
    private void Recycle(StatModifier[] group)
    {
        if (group != null && group.Length == 1 && spareSingles.Count < MaxSpareSingles) spareSingles.Push(group);
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
