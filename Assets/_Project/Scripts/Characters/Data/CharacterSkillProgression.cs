using System;
using UnityEngine;

/// <summary>
/// Một bước tiến trình skill của Player: "skill X đạt cấp Y khi thỏa điều kiện Z".
/// Mở khóa skill chỉ là trường hợp đạt cấp 1; lên sao/lên level nâng skill là các bước có cấp lớn hơn.
/// </summary>
/// <remarks>
/// Nằm trong <see cref="CharacterDataSO"/> cạnh danh sách skill có sẵn, để mọi luật skill của nhân vật ở một chỗ.
/// Ghi cấp đích thay vì "+1" để đọc Inspector là biết ngay kết quả và không phụ thuộc thứ tự các bước.
/// Không cần lưu riêng: level và sao đã có trong save, cấp skill luôn tính lại từ các bước này.
/// </remarks>
[Serializable]
public struct SkillProgressStep
{
    [Tooltip("Skill nguồn Character được mở/nâng. Skill chưa có trong danh sách Skills thì phải có một bước Skill Level = 1 để mở khóa.")]
    [SerializeField] private SkillDataSO skill;
    [Tooltip("Cấp skill đạt được khi thỏa điều kiện. 1 = mở khóa skill; lớn hơn = nâng cấp. Không vượt số cấp khai báo trong skill.")]
    [SerializeField, Min(1)] private int skillLevel;
    [Tooltip("So điều kiện với level vĩnh viễn hay với số sao của nhân vật.")]
    [SerializeField] private SkillConditionType condition;
    [Tooltip("Level hoặc số sao cần đạt (ví dụ Character Level 20, hoặc Stars 2). Sao nằm trong 1..5.")]
    [SerializeField, Min(1)] private int required;

    /// <summary>Skill được mở/nâng.</summary>
    public SkillDataSO Skill => skill;

    /// <summary>Cấp skill đạt được; 1 nghĩa là bước mở khóa.</summary>
    public int SkillLevel => skillLevel;

    /// <summary>Loại điều kiện so sánh.</summary>
    public SkillConditionType Condition => condition;

    /// <summary>Giá trị level/số sao cần đạt.</summary>
    public int Required => required;

    /// <summary>
    /// Bước đã thỏa với level và sao hiện tại chưa. Một switch duy nhất cho mọi loại điều kiện.
    /// </summary>
    public bool IsMet(int characterLevel, int stars)
    {
        switch (condition)
        {
            case SkillConditionType.CharacterLevel:
                return characterLevel >= required;
            case SkillConditionType.Stars:
                return stars >= required;
            default:
                throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown skill condition.");
        }
    }
}

/// <summary>
/// Kết quả tính: nhân vật vào trận với skill nào, bắt đầu ở cấp mấy. Không serialize, chỉ dùng lúc spawn và để UI hiển thị.
/// </summary>
/// <remarks>Giữ riêng với <see cref="SkillProgressStep"/> vì đây là kết quả đã tính, không mang điều kiện.</remarks>
public readonly struct SkillGrant
{
    /// <summary>Skill được cấp.</summary>
    public readonly SkillDataSO Skill;

    /// <summary>Cấp bắt đầu của skill.</summary>
    public readonly int Level;

    public SkillGrant(SkillDataSO skill, int level)
    {
        Skill = skill;
        Level = level;
    }
}
