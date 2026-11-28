using System;
using System.Collections.Generic;
using UnityEngine;

// Các luật chọn mục tiêu (strategy của ITargetSelector). Asset chọn luật qua [SerializeReference].
// KHÔNG đổi tên class hoặc tên field [SerializeField] trong file này: Unity lưu tên class và tên field trong asset skill.

#region Shared Helpers

/// <summary>Phần dùng chung của các luật: buffer tạm và cách dựng điểm nhắm từ một mục tiêu.</summary>
internal static class TargetSelection
{
    // Buffer tạm dùng chung cho mọi luật cần "mọi quái trong tầm". Chỉ sống trong một lời gọi Select (main thread,
    // không callback), nên không cần buffer riêng theo nhân vật. FindAll tự nới mảng khi không đủ chỗ (ref).
    internal static CharacterHandle[] Candidates = new CharacterHandle[64];

    /// <summary>Điểm nhắm tới một mục tiêu; mục tiêu trùng vị trí nguồn thì giữ hướng nhìn.</summary>
    internal static bool TryAimAt(in SkillOrigin origin, CharacterHandle target, out SkillAim aim)
    {
        aim = default;
        if (!target.TryGetCharacter(out CharacterView view)) return false;
        Vector2 point = view.HitPosition;
        Vector2 offset = point - origin.Position;
        Vector2 direction = offset.sqrMagnitude > 0.000001f ? offset.normalized : origin.Facing;
        aim = new SkillAim(point, direction, target);
        return true;
    }

    /// <summary>Ghi mọi mục tiêu đối địch trong tầm chiến đấu của nguồn vào <see cref="Candidates"/>; trả số phần tử hợp lệ.</summary>
    internal static int FindCandidates(in SkillOrigin origin, ICharacterSearch search)
        => search.FindAll(origin.Position, origin.Range, CharacterSearch.OpponentOf(origin.Team), ref Candidates, origin.Self);
}

#endregion

/// <summary>Mục tiêu đối địch gần nhất trong tầm chiến đấu. Mặc định của skill bắn.</summary>
[Serializable]
public sealed class NearestTarget : ITargetSelector
{
    /// <inheritdoc/>
    public bool ProvidesTarget => true;

    /// <inheritdoc/>
    public int Select(in SkillOrigin origin, ICharacterSearch search, int count, List<SkillAim> results)
    {
        results.Clear();
        if (count < 1) return 0;
        CharacterHandle target = search.FindNearest(origin.Position, origin.Range, CharacterSearch.OpponentOf(origin.Team), origin.Self);
        if (!TargetSelection.TryAimAt(origin, target, out SkillAim aim)) return 0;
        results.Add(aim);
        return 1;
    }

    /// <inheritdoc/>
    public void Validate() { } // Không có thông số.
}

/// <summary>Bốc ngẫu nhiên tối đa N mục tiêu khác nhau trong tầm chiến đấu (sét đánh ngẫu nhiên, hào quang giật N quái…).</summary>
[Serializable]
public sealed class RandomInRange : ITargetSelector
{
    /// <inheritdoc/>
    public bool ProvidesTarget => true;

    /// <inheritdoc/>
    public int Select(in SkillOrigin origin, ICharacterSearch search, int count, List<SkillAim> results)
    {
        results.Clear();
        if (count < 1) return 0;
        int found = TargetSelection.FindCandidates(origin, search);
        CharacterHandle[] candidates = TargetSelection.Candidates;
        // Fisher–Yates từng phần: chỉ xáo đủ số cần lấy, không trùng mục tiêu, không cấp phát.
        for (int index = 0; index < found && results.Count < count; index++)
        {
            int pick = UnityEngine.Random.Range(index, found);
            CharacterHandle chosen = candidates[pick];
            candidates[pick] = candidates[index];
            candidates[index] = chosen;
            if (TargetSelection.TryAimAt(origin, chosen, out SkillAim aim)) results.Add(aim);
        }
        Array.Clear(candidates, 0, found); // Không giữ handle quá một lời gọi.
        return results.Count;
    }

    /// <inheritdoc/>
    public void Validate() { } // Không có thông số.
}

/// <summary>
/// Mục tiêu có nhiều quái quanh nó nhất trong tầm chiến đấu (thiên thạch, cột lửa, mưa axit rơi vào chỗ đông).
/// Trả một điểm nhắm: vị trí của quái đứng giữa cụm đông nhất.
/// </summary>
[Serializable]
public sealed class DensestCluster : ITargetSelector
{
    #region Configuration

    [Tooltip("Bán kính đếm hàng xóm quanh mỗi quái ứng viên (world unit). Nên gần bằng vùng nổ của skill.")]
    [SerializeField, Min(0.01f)] private float clusterRadius = 0.4f;
    [Tooltip("Số quái ứng viên tối đa được xét làm tâm cụm, để chi phí không tăng theo bình phương khi quá đông.")]
    [SerializeField, Range(1, 64)] private int maxCandidates = 32;

    #endregion

    #region ITargetSelector

    /// <inheritdoc/>
    public bool ProvidesTarget => true;

    /// <inheritdoc/>
    public int Select(in SkillOrigin origin, ICharacterSearch search, int count, List<SkillAim> results)
    {
        results.Clear();
        if (count < 1) return 0;
        int found = TargetSelection.FindCandidates(origin, search);
        CharacterHandle[] candidates = TargetSelection.Candidates;
        float radiusSquared = clusterRadius * clusterRadius;
        int centers = Mathf.Min(found, maxCandidates);
        int best = -1, bestNeighbours = -1;
        for (int center = 0; center < centers; center++)
        {
            if (!candidates[center].TryGetCharacter(out CharacterView centerView)) continue;
            Vector2 centerPoint = centerView.HitPosition;
            int neighbours = 0;
            for (int other = 0; other < found; other++)
            {
                if (other == center || !candidates[other].TryGetCharacter(out CharacterView otherView)) continue;
                if (((Vector2)otherView.HitPosition - centerPoint).sqrMagnitude <= radiusSquared) neighbours++;
            }
            if (neighbours <= bestNeighbours) continue; // Bằng nhau thì giữ ứng viên tìm thấy trước.
            best = center;
            bestNeighbours = neighbours;
        }
        if (best >= 0 && TargetSelection.TryAimAt(origin, candidates[best], out SkillAim aim)) results.Add(aim);
        Array.Clear(candidates, 0, found);
        return results.Count;
    }

    /// <inheritdoc/>
    public void Validate()
    {
        if (!(clusterRadius > 0f) || float.IsInfinity(clusterRadius) || maxCandidates < 1)
            throw new InvalidOperationException("Densest cluster needs a positive radius and at least one candidate.");
    }

    #endregion
}

/// <summary>
/// Theo hướng nhìn (hướng di chuyển gần nhất) của nguồn, không tìm mục tiêu (dao, chém, tia).
/// Luôn trả một điểm nhắm ở mép tầm chiến đấu phía trước.
/// </summary>
[Serializable]
public sealed class FacingDirection : ITargetSelector
{
    /// <inheritdoc/>
    public bool ProvidesTarget => false;

    /// <inheritdoc/>
    public int Select(in SkillOrigin origin, ICharacterSearch search, int count, List<SkillAim> results)
    {
        results.Clear();
        if (count < 1) return 0;
        results.Add(new SkillAim(origin.Position + origin.Facing * origin.Range, origin.Facing, default));
        return 1;
    }

    /// <inheritdoc/>
    public void Validate() { } // Không có thông số.
}

/// <summary>
/// Hướng cố định trong world, không tìm mục tiêu và không theo hướng nhìn (kiểu bắn vòng/xoắn ốc của boss).
/// Giữ đúng hành vi cũ của skill bắn tắt Requires Target.
/// </summary>
[Serializable]
public sealed class FixedDirection : ITargetSelector
{
    #region Configuration

    [Tooltip("Hướng bắn trong world; được chuẩn hóa khi dùng.")]
    [SerializeField] private Vector2 direction = Vector2.right;

    #endregion

    #region ITargetSelector

    /// <inheritdoc/>
    public bool ProvidesTarget => false;

    /// <inheritdoc/>
    public int Select(in SkillOrigin origin, ICharacterSearch search, int count, List<SkillAim> results)
    {
        results.Clear();
        if (count < 1) return 0;
        Vector2 aim = direction.normalized;
        results.Add(new SkillAim(origin.Position + aim * origin.Range, aim, default));
        return 1;
    }

    /// <inheritdoc/>
    public void Validate()
    {
        if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.x) ||
            float.IsInfinity(direction.y) || direction.sqrMagnitude < 0.000001f)
            throw new InvalidOperationException("Fixed direction must be finite and nonzero.");
    }

    #endregion
}
