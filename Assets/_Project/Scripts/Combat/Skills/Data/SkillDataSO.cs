using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Định nghĩa một skill: thông tin hiển thị, danh sách cấp (mỗi cấp chứa đủ data của các hành vi được bật),
/// và loại các hành vi skill chạy.
/// </summary>
/// <remarks>
/// Asset chỉ lưu LOẠI hành vi (<see cref="SkillBehaviourType"/>); <see cref="SkillFactory"/> tạo class hành vi bằng
/// <c>new</c> qua một switch. Thông số theo cấp (<see cref="SkillBehaviourData"/>) lưu bằng <c>[SerializeReference]</c>
/// và ghép với hành vi qua cùng enum. Asset chỉ đọc khi chạy.
/// Skill vốn có của nhân vật và skill nhận trong trận là hai asset riêng, không dùng chung.
/// </remarks>
[CreateAssetMenu(menuName = "Shooter Game/Skills/Skill")]
public sealed class SkillDataSO : ScriptableObject
{
    #region Display And Source

    [Tooltip("Gameplay: skill có thể xuất hiện trong lựa chọn nâng cấp. Character/Equipment: skill vốn có, lấy cấp ban đầu từ Level.")]
    [SerializeField] private SkillSource source;
    [Tooltip("Cấp ban đầu của skill Character/Equipment. Skill Gameplay luôn bắt đầu ở cấp 1 trong trận.")]
    [SerializeField, Min(1)] private int level = 1; // Cấp ban đầu của Character/Equipment; Gameplay luôn bắt đầu cấp một.
    [Tooltip("Tên skill hiển thị trên UI.")]
    [SerializeField] private string displayName;
    [Tooltip("Mô tả chung, dùng khi cấp hiện tại không có mô tả riêng.")]
    [SerializeField, TextArea] private string description; // Mô tả dự phòng khi cấp chưa có mô tả riêng.
    [Tooltip("Icon hiển thị trên thẻ lựa chọn và ô skill.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Chỉ dùng cho boss: animation khi Shooting thực thi thành công. Chọn Idle để không yêu cầu animation; NormalAttack của boss dùng Attack.")]
    [SerializeField] private CharacterVisualState shootingVisualState = CharacterVisualState.Cast;

    #endregion

    #region Level Data And Behaviour Templates

    [Tooltip("Danh sách cấu hình theo cấp, theo thứ tự từ cấp 1. Mỗi cấp giữ mô tả và các Behaviour Data được bật ở cấp đó.")]
    [SerializeField] private SkillLevelData[] levels = Array.Empty<SkillLevelData>();
    [Tooltip("Các hành vi skill này chạy, mỗi loại tối đa một lần. Thông số nằm trong Behaviour Data của từng cấp.")]
    [SerializeField] private SkillBehaviourType[] behaviourTypes = Array.Empty<SkillBehaviourType>();
    public SkillSource Source => source;
    public int Level => level;
    public int MaxLevel => levels.Length;
    public int MaxBattleLevel => MaxLevel;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public CharacterVisualState ShootingVisualState => shootingVisualState;
    public IReadOnlyList<SkillLevelData> Levels => Array.AsReadOnly(levels);
    /// <summary>Loại các hành vi skill này chạy; mỗi loại tối đa một lần.</summary>
    public IReadOnlyList<SkillBehaviourType> BehaviourTypes => Array.AsReadOnly(behaviourTypes);

    // Không fallback về cấp trước: mỗi cấp phải khai báo đầy đủ data cần hoạt động.
    public SkillLevelData GetLevel(int currentLevel)
    {
        if (currentLevel < 1 || currentLevel > levels.Length) throw new ArgumentOutOfRangeException(nameof(currentLevel));
        return levels[currentLevel - 1];
    }

    /// <summary>Skill có khai báo hành vi <paramref name="type"/> hay không.</summary>
    internal bool HasBehaviour(SkillBehaviourType type) => Array.IndexOf(behaviourTypes, type) >= 0;

    // UI đọc mô tả từ cùng cấp dùng để cấu hình runtime.
    public string GetLevelDescription(int currentLevel)
    {
        string text = GetLevel(currentLevel).Description;
        // Cấp chưa có mô tả riêng thì dùng mô tả chung của skill.
        if (string.IsNullOrWhiteSpace(text))
            return description;
        return text;
    }

    #endregion

    #region Validation

    // Chặn thiếu loại hành vi, trùng enum và data không khớp trước khi thay đổi bất kỳ runtime nào.
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(SkillSource), source) || levels == null || levels.Length == 0 ||
            level < 1 || level > levels.Length || behaviourTypes == null || behaviourTypes.Length == 0)
            throw new InvalidOperationException($"Invalid skill definition: {name}.");
        var templates = new HashSet<SkillBehaviourType>();
        foreach (SkillBehaviourType type in behaviourTypes)
            if (!Enum.IsDefined(typeof(SkillBehaviourType), type) || !templates.Add(type))
                throw new InvalidOperationException($"Unknown/duplicate behaviour type in {name}.");
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
