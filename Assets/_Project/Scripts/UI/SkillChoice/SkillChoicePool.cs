using System;
using System.Collections.Generic;

// Một lựa chọn là CẤP KẾ TIẾP của một skill, không phải một cấp ngẫu nhiên trong toàn bộ bảng.
public readonly struct SkillChoiceOffer
{
    #region Offer Snapshot

    public readonly SkillDataSO Definition;
    public readonly int CurrentLevel;
    public readonly int OfferedLevel;
    public readonly int MaxLevel;

    internal SkillChoiceOffer(SkillDataSO definition, int current, int offered, int max)
    { Definition = definition; CurrentLevel = current; OfferedLevel = offered; MaxLevel = max; }

    #endregion

    #region Validation And Application

    // Chỉ áp dụng nếu runtime vẫn ở đúng cấp lúc roll; offer cũ không được tiêu phần thưởng.
    public bool TryApply(SkillManager manager, CharacterView owner)
    {
        if (owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return false;
        int current = manager.TryGetRuntime(Definition, owner, out SkillRuntime runtime) ? runtime.RunLevel : 0;
        // Asset/runtime có thể đổi qua Editor hoặc callback khi bảng đang mở. Không tiêu lượt theo offer cũ.
        return current == CurrentLevel && OfferedLevel == current + 1 && OfferedLevel <= MaxLevel &&
            manager.GrantOrUpgrade(Definition, owner);
    }
    #endregion
}

public sealed class SkillChoicePool
{
    #region Candidate State

    public const int StarLimit = 5; // UI hiện chỉ mở 5 cấp, chưa đưa tiến hóa level 6 vào phần thưởng.
    private readonly List<SkillChoiceOffer> candidates = new List<SkillChoiceOffer>();
    private readonly HashSet<SkillDataSO> unique = new HashSet<SkillDataSO>();
    private readonly Random random;

    public SkillChoicePool(Random random = null) => this.random = random ?? new Random();

    #endregion

    #region Offer Generation

    // Roll không lặp trong một bảng; bỏ skill max và các asset bị controller chặn sau exception.
    public void Build(IReadOnlyList<SkillDataSO> catalog, SkillManager manager, CharacterView owner,
        List<SkillChoiceOffer> result, int count = 3, ISet<SkillDataSO> excluded = null)
    {
        result.Clear(); candidates.Clear(); unique.Clear();
        if (catalog == null || owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return;
        foreach (SkillDataSO definition in catalog)
        {
            if (definition == null || definition.Source != SkillSource.Gameplay || !unique.Add(definition) || (excluded != null && excluded.Contains(definition))) continue;
            int max = Math.Min(StarLimit, definition.MaxRunLevel);
            int current = manager.TryGetRuntime(definition, owner, out SkillRuntime runtime) ? runtime.RunLevel : 0;
            if (current >= max) continue;
            candidates.Add(new SkillChoiceOffer(definition, current, current + 1, max));
        }
        // Chỉ chống trùng TRONG MỘT BẢNG. Lần lên cấp sau skill cũ có thể xuất hiện lại ở cấp kế tiếp.
        while (result.Count < count && candidates.Count > 0)
        {
            int index = random.Next(candidates.Count);
            result.Add(candidates[index]);
            int last = candidates.Count - 1;
            candidates[index] = candidates[last];
            candidates.RemoveAt(last);
        }
        candidates.Clear(); unique.Clear();
    }
    #endregion
}

// Một lần nhận EXP có thể tăng nhiều cấp: giữ đủ lượt chọn và roll lại SAU mỗi lần người chơi chọn.
public sealed class SkillChoiceProgress
{
    #region Reward Queue

    public int Pending { get; private set; } // Số lượt chưa tiêu; không phụ thuộc trạng thái panel.

    // Cộng đúng chênh lệch level, bỏ qua sự kiện không tăng cấp.
    public void AddLevels(int previous, int current)
    {
        if (current > previous) Pending = checked(Pending + current - previous);
    }
    // Chỉ gọi khi nhận/nâng thành công; queue rỗng không được âm.
    public bool Consume()
    {
        if (Pending == 0) return false;
        Pending--;
        return true;
    }
    // Hủy thưởng khi kết thúc/reset vòng đời owner hoặc khi mọi skill đã max.
    public void Clear() => Pending = 0;

    #endregion
}
