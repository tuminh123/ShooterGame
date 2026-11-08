using System;
using System.Collections.Generic;

// Một lựa chọn là CẤP KẾ TIẾP của một skill, không phải một cấp ngẫu nhiên trong toàn bộ bảng.
public readonly struct SkillChoiceOffer
{
    public readonly SkillDataSO Definition;
    public readonly int CurrentLevel;
    public readonly int OfferedLevel;
    public readonly int MaxLevel;

    internal SkillChoiceOffer(SkillDataSO definition, int current, int offered, int max)
    { Definition = definition; CurrentLevel = current; OfferedLevel = offered; MaxLevel = max; }

    public bool TryApply(SkillManager manager, CharacterView owner)
    {
        if (owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return false;
        int current = manager.TryGetRuntime(Definition, owner, out SkillRuntime runtime) ? runtime.RunLevel : 0;
        // Asset/runtime có thể đổi qua Editor hoặc callback khi bảng đang mở. Không tiêu lượt theo offer cũ.
        return current == CurrentLevel && OfferedLevel == current + 1 && OfferedLevel <= MaxLevel &&
            manager.GrantOrUpgrade(Definition, owner);
    }
}

public sealed class SkillChoicePool
{
    public const int StarLimit = 5; // UI hiện chỉ mở 5 cấp, chưa đưa tiến hóa level 6 vào phần thưởng.
    private readonly List<SkillChoiceOffer> candidates = new List<SkillChoiceOffer>();
    private readonly HashSet<SkillDataSO> unique = new HashSet<SkillDataSO>();
    private readonly Random random;

    public SkillChoicePool(Random random = null) => this.random = random ?? new Random();

    public void Build(IReadOnlyList<SkillDataSO> catalog, SkillManager manager, CharacterView owner,
        List<SkillChoiceOffer> result, int count = 3)
    {
        result.Clear(); candidates.Clear(); unique.Clear();
        if (catalog == null || owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return;
        foreach (SkillDataSO definition in catalog)
        {
            if (definition == null || !unique.Add(definition)) continue;
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
}

// Một lần nhận EXP có thể tăng nhiều cấp: giữ đủ lượt chọn và roll lại SAU mỗi lần người chơi chọn.
public sealed class SkillChoiceProgress
{
    public int Pending { get; private set; }
    public void AddLevels(int previous, int current)
    {
        if (current > previous) Pending = checked(Pending + current - previous);
    }
    public bool Consume()
    {
        if (Pending == 0) return false;
        Pending--;
        return true;
    }
    public void Clear() => Pending = 0;
}
