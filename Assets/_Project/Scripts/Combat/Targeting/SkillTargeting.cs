using UnityEngine;

/// <summary>
/// Đòn phát ra từ đâu: vị trí, hướng nhìn và phe của nguồn. Hiện luôn tạo từ nhân vật;
/// sau này pet/trụ tạo từ vị trí của chính nó để dùng lại nguyên cách nhắm của skill.
/// </summary>
public readonly struct SkillOrigin
{
    /// <summary>Điểm phát đòn (world).</summary>
    public readonly Vector2 Position;
    /// <summary>Hướng nhìn đã chuẩn hóa: hướng di chuyển gần nhất của nhân vật.</summary>
    public readonly Vector2 Facing;
    /// <summary>Phe của nguồn; luật chọn tìm phe đối địch.</summary>
    public readonly CharacterTeam Team;
    /// <summary>Nhân vật nguồn, để không tự nhắm chính mình; null nếu nguồn không phải nhân vật.</summary>
    public readonly CharacterView Self;
    /// <summary>
    /// Tầm chiến đấu của nguồn (world unit): luật chọn chỉ nhắm/đặt trong bán kính này.
    /// Với nhân vật là <see cref="CharacterRuntimeData.CombatRange"/>; skill không tự giữ tầm.
    /// </summary>
    public readonly float Range;

    public SkillOrigin(Vector2 position, Vector2 facing, CharacterTeam team, CharacterView self, float range)
    {
        Position = position;
        Facing = facing;
        Team = team;
        Self = self;
        Range = range;
    }
}

/// <summary>
/// Một điểm nhắm do <see cref="ITargetSelector"/> trả về. Skill lấy phần mình cần:
/// đạn lấy hướng (hoặc mục tiêu để đuổi), vùng lấy điểm, sét nảy lấy mục tiêu.
/// </summary>
public readonly struct SkillAim
{
    /// <summary>Điểm nhắm (world): vị trí mục tiêu, tâm cụm quái hoặc điểm phía trước theo hướng nhìn.</summary>
    public readonly Vector2 Point;
    /// <summary>Hướng từ nguồn tới điểm nhắm, đã chuẩn hóa.</summary>
    public readonly Vector2 Direction;
    /// <summary>Mục tiêu (đúng lượt spawn); rỗng khi luật chỉ trả hướng/điểm.</summary>
    public readonly CharacterHandle Target;

    public SkillAim(Vector2 point, Vector2 direction, CharacterHandle target)
    {
        Point = point;
        Direction = direction;
        Target = target;
    }
}

/// <summary>
/// Luật "skill đánh vào đâu", chọn trong data bằng <c>[SerializeReference]</c> (cùng kiểu <see cref="IShooter"/>).
/// Luật chỉ dùng bộ tìm kiếm chung <see cref="ICharacterSearch"/>, không tự giữ danh sách nhân vật.
/// </summary>
/// <remarks>
/// Instance nằm trong asset và dùng chung cho mọi nhân vật có skill đó: luật không giữ state theo nhân vật.
/// Buffer tạm (nếu có) là static và chỉ dùng trong một lời gọi, vì mọi skill chạy trên main thread và
/// <see cref="Select"/> không gọi ra callback nào.
/// </remarks>
public interface ITargetSelector
{
    /// <summary>Luật có trả mục tiêu (handle) hay không; đạn đuổi cần luật trả mục tiêu.</summary>
    bool ProvidesTarget { get; }

    /// <summary>
    /// Xóa <paramref name="results"/> rồi ghi tối đa <paramref name="count"/> điểm nhắm; trả số điểm đã ghi.
    /// 0 = không có gì để đánh trong tầm chiến đấu của nguồn (<see cref="SkillOrigin.Range"/>).
    /// </summary>
    int Select(in SkillOrigin origin, ICharacterSearch search, int count, System.Collections.Generic.List<SkillAim> results);

    /// <summary>Ném lỗi nếu tham số của luật sai.</summary>
    void Validate();
}
