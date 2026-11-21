using System;
using System.Collections.Generic;

/// <summary>Một lựa chọn là CẤP KẾ TIẾP của một skill, không phải một cấp ngẫu nhiên trong toàn bộ bảng.</summary>
public readonly struct SkillChoiceOffer
{
    #region Offer Snapshot

    /// <summary>Skill được đề xuất.</summary>
    public readonly SkillDataSO Definition;
    /// <summary>Cấp skill lúc roll (0 nếu chưa sở hữu); dùng để từ chối offer đã cũ.</summary>
    public readonly int CurrentLevel;
    /// <summary>Cấp sẽ nhận nếu chọn, luôn bằng CurrentLevel + 1.</summary>
    public readonly int OfferedLevel;
    /// <summary>Trần cấp của offer, dùng để hiển thị số sao.</summary>
    public readonly int MaxLevel;

    /// <summary>Chụp trạng thái skill tại thời điểm roll.</summary>
    internal SkillChoiceOffer(SkillDataSO definition, int current, int offered, int max)
    {
        Definition = definition;
        CurrentLevel = current;
        OfferedLevel = offered;
        MaxLevel = max;
    }

    #endregion

    #region Validation And Application

    /// <summary>Chỉ áp dụng nếu runtime vẫn ở đúng cấp lúc roll; offer cũ không được tiêu phần thưởng.</summary>
    public bool TryApply(ISkillGrant manager, IOwnedSkillReader reader, CharacterView owner)
    {
        if (owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return false;
        int current = reader.TryGetRuntime(Definition, owner, out SkillRuntime runtime) ? runtime.RunLevel : 0;
        // Asset/runtime có thể đổi qua Editor hoặc callback khi bảng đang mở. Không tiêu lượt theo offer cũ.
        return current == CurrentLevel && OfferedLevel == current + 1 && OfferedLevel <= MaxLevel &&
            manager.GrantOrUpgrade(Definition, owner);
    }
    #endregion
}

/// <summary>Roll các lựa chọn skill cho một bảng thưởng, không lặp skill trong cùng bảng.</summary>
public sealed class SkillChoicePool
{
    #region Candidate State

    /// <summary>UI hiện chỉ mở 5 cấp, chưa đưa tiến hóa level 6 vào phần thưởng.</summary>
    public const int StarLimit = 5;
    private readonly List<SkillChoiceOffer> candidates = new List<SkillChoiceOffer>();
    private readonly HashSet<SkillDataSO> unique = new HashSet<SkillDataSO>();
    private readonly HashSet<SkillDataSO> previous = new HashSet<SkillDataSO>();
    private readonly Random random;

    /// <summary>Nhận Random từ bên ngoài để test có thể cố định seed; mặc định tạo Random mới.</summary>
    public SkillChoicePool(Random random = null) => this.random = random ?? new Random();

    #endregion

    #region Offer Generation

    /// <summary>Roll không lặp trong một bảng; bỏ skill max và các asset bị controller chặn sau exception.</summary>
    public void Build(
        IReadOnlyList<SkillDataSO> catalog,
        IOwnedSkillReader manager,
        CharacterView owner,
        List<SkillChoiceOffer> result,
        int count = 3,
        ISet<SkillDataSO> excluded = null,
        IReadOnlyList<SkillChoiceOffer> previousOffers = null)
    {
        previous.Clear();
        // Chụp trước khi xóa result: controller có thể truyền cùng list để roll lại.
        if (previousOffers != null)
            foreach (SkillChoiceOffer offer in previousOffers) previous.Add(offer.Definition);
        result.Clear();
        candidates.Clear();
        unique.Clear();
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
            // Ưu tiên skill chưa có trong bảng trước; thiếu ứng viên thì dùng lại để đủ ô.
            int alternatives = 0;
            if (previous.Count > 0)
                for (int i = 0; i < candidates.Count; i++)
                    if (!previous.Contains(candidates[i].Definition) && random.Next(++alternatives) == 0) index = i;
            result.Add(candidates[index]);
            int last = candidates.Count - 1;
            candidates[index] = candidates[last];
            candidates.RemoveAt(last);
        }
        candidates.Clear();
        unique.Clear();
        previous.Clear();
    }
    #endregion
}

/// <summary>Một lần nhận EXP có thể tăng nhiều cấp: giữ đủ lượt chọn và roll lại SAU mỗi lần người chơi chọn.</summary>
public sealed class SkillChoiceProgress
{
    #region Reward Queue

    /// <summary>Số lượt chưa tiêu; không phụ thuộc trạng thái panel.</summary>
    public int Pending { get; private set; }

    /// <summary>Cộng đúng chênh lệch level, bỏ qua sự kiện không tăng cấp.</summary>
    public void AddLevels(int previous, int current)
    {
        if (current > previous) Pending = checked(Pending + current - previous);
    }
    /// <summary>Chỉ gọi khi nhận/nâng thành công; queue rỗng không được âm.</summary>
    public bool Consume()
    {
        if (Pending == 0) return false;
        Pending--;
        return true;
    }
    /// <summary>Hủy thưởng khi kết thúc/reset vòng đời owner hoặc khi mọi skill đã max.</summary>
    public void Clear() => Pending = 0;

    #endregion
}
