using System;
using System.Collections.Generic;
using UnityEngine;

// Một definition độc lập; skill nhân vật không dùng chung asset với skill nhận trong màn.
[CreateAssetMenu(menuName = "Shooter Game/Skills/Skill")]
public sealed class SkillDataSO : ScriptableObject
{
    #region Display And Source

    [SerializeField] private SkillSource source; // Chỉ Gameplay được nhận/nâng qua bảng thưởng.
    [SerializeField, Min(1)] private int level = 1; // Cấp ban đầu của Character/Equipment; Gameplay luôn bắt đầu cấp một.
    [SerializeField] private string displayName; // Tên hiển thị.
    [SerializeField, TextArea] private string description; // Mô tả dự phòng khi cấp chưa có mô tả riêng.
    [SerializeField] private Sprite icon; // Icon thẻ và slot sở hữu.

    #endregion

    #region Level Data And Behaviour Templates

    [SerializeField] private SkillLevelData[] levels = Array.Empty<SkillLevelData>(); // Index 0 là cấp 1; mỗi cấp chứa đủ cấu hình.
    [SerializeReference] private ISkill[] behaviours = Array.Empty<ISkill>(); // Mẫu hành vi không chứa thông số theo cấp.
    public SkillSource Source => source;
    public int Level => level;
    public int MaxLevel => levels.Length;
    public int MaxRunLevel => MaxLevel;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public IReadOnlyList<SkillLevelData> Levels => Array.AsReadOnly(levels);
    public IReadOnlyList<ISkill> Behaviours => Array.AsReadOnly(behaviours);

    // Không fallback về cấp trước: mỗi cấp phải khai báo đầy đủ data cần hoạt động.
    public SkillLevelData GetLevel(int currentLevel)
    {
        if (currentLevel < 1 || currentLevel > levels.Length) throw new ArgumentOutOfRangeException(nameof(currentLevel));
        return levels[currentLevel - 1];
    }

    // Một loại hành vi có tối đa một mẫu trong definition; enum được khai báo cố định trong class.
    internal ISkill GetBehaviour(SkillBehaviourType type)
    {
        foreach (ISkill behaviour in behaviours) if (behaviour.Type == type) return behaviour;
        throw new InvalidOperationException($"No {type} behaviour in {name}.");
    }

    // UI đọc mô tả từ cùng cấp dùng để cấu hình runtime.
    public string GetRunLevelDescription(int currentLevel)
    {
        string text = GetLevel(currentLevel).Description;
        return string.IsNullOrWhiteSpace(text) ? description : text;
    }

    #endregion

    #region Validation

    // Chặn thiếu mẫu, trùng enum và data không khớp trước khi thay đổi bất kỳ runtime nào.
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(SkillSource), source) || levels == null || levels.Length == 0 ||
            level < 1 || level > levels.Length || behaviours == null || behaviours.Length == 0)
            throw new InvalidOperationException($"Invalid skill definition: {name}.");
        var templates = new HashSet<SkillBehaviourType>();
        foreach (ISkill behaviour in behaviours)
            if (!(behaviour is Skill) || !Enum.IsDefined(typeof(SkillBehaviourType), behaviour.Type) || !templates.Add(behaviour.Type))
                throw new InvalidOperationException($"Missing/duplicate behaviour in {name}.");
        foreach (SkillLevelData entry in levels)
        {
            if (entry == null || entry.Behaviours == null || entry.Behaviours.Count == 0)
                throw new InvalidOperationException($"Missing level data in {name}.");
            var types = new HashSet<SkillBehaviourType>();
            foreach (SkillBehaviourData data in entry.Behaviours)
            {
                if (data == null || !templates.Contains(data.Type) || !types.Add(data.Type))
                    throw new InvalidOperationException($"Missing/duplicate/mismatched level behaviour in {name}.");
                data.Validate();
            }
        }
    }

    #endregion
}

// Nguồn quyết định quyền nâng; không cần tạo thêm hệ kế thừa runtime theo nguồn.
public enum SkillSource { Gameplay, Character, Equipment }
