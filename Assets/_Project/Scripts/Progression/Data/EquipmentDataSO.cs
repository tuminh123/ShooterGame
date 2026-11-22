using System;
using UnityEngine;

/// <summary>
/// Một mẫu trang bị (ví dụ "Samurai Helmet"): nhận diện, hiển thị, ô mặc, set và skill riêng.
/// Dùng chung cho mọi bản đồ cùng mẫu; độ hiếm, level và dòng phụ nằm trên từng bản (<see cref="EquipmentItem"/>).
/// </summary>
/// <remarks>
/// Asset không chứa chỉ số: chỉ số chính đọc từ bảng chung <see cref="EquipmentRulesSO"/> theo ô và độ hiếm,
/// nên các mẫu cùng ô chỉ khác nhau ở ngoại hình, set và skill.
/// </remarks>
[CreateAssetMenu(fileName = "Equipment", menuName = "Shooter Game/Data/Equipment Item")]
public sealed class EquipmentDataSO : ScriptableObject
{
    #region Identity And Display

    [Tooltip("ID ổn định được lưu trong save; phân biệt hoa/thường, không trống, không trùng. Lưu ý: đổi hoặc xóa ID thì các bản đồ đã lưu với ID này bị xóa khỏi túi đồ khi load.")]
    [SerializeField] private string id;
    [Tooltip("Tên hiển thị; độc lập với ID nên đổi tự do.")]
    [SerializeField] private string displayName;
    [Tooltip("Mô tả ngắn trong popup chi tiết.")]
    [SerializeField, TextArea] private string description;
    [Tooltip("Icon của món đồ trên thẻ, ô mặc và popup.")]
    [SerializeField] private Sprite icon;

    /// <summary>ID ổn định được lưu trong save.</summary>
    public string Id => id;

    /// <summary>Tên hiển thị.</summary>
    public string DisplayName => displayName;

    /// <summary>Mô tả ngắn trong popup chi tiết.</summary>
    public string Description => description;

    /// <summary>Icon của món đồ.</summary>
    public Sprite Icon => icon;

    #endregion

    #region Slot And Set

    [Tooltip("Ô mặc; quyết định chỉ số chính (xem Equipment Rules).")]
    [SerializeField] private EquipmentSlot slot;
    [Tooltip("Set của món đồ; None = không thuộc set nào. Set khai báo trong Equipment Database.")]
    [SerializeField] private EquipmentSet set;

    /// <summary>Ô mặc của món đồ.</summary>
    public EquipmentSlot Slot => slot;

    /// <summary>Set của món đồ; <see cref="EquipmentSet.None"/> nếu không thuộc set nào.</summary>
    public EquipmentSet Set => set;

    #endregion

    #region Skill (một skill, một cấp)

    [Tooltip("Skill riêng của món đồ (có thể để trống). Phải là definition nguồn Equipment và có đúng 1 cấp.")]
    [SerializeField] private SkillDataSO skill;
    [Tooltip("Level của món đồ cần đạt để có skill; 1 = có ngay khi nhận đồ.")]
    [SerializeField, Min(1)] private int skillUnlockLevel = 1;

    /// <summary>Skill riêng của món đồ; null nếu không có.</summary>
    public SkillDataSO Skill => skill;

    /// <summary>Level món đồ cần đạt để có skill.</summary>
    public int SkillUnlockLevel => skillUnlockLevel;

    /// <summary>
    /// Nơi DUY NHẤT quyết định món đồ ở level <paramref name="itemLevel"/> có skill hay chưa.
    /// Combat dùng lúc spawn và popup dùng để hiển thị, nên hai bên không lệch nhau.
    /// </summary>
    public bool TryGetSkill(int itemLevel, out SkillDataSO unlocked)
    {
        bool has = skill != null && itemLevel >= skillUnlockLevel;
        unlocked = has ? skill : null;
        return has;
    }

    #endregion

    #region Validation

    /// <summary>Kiểm tra cấu hình của riêng mẫu đồ; giới hạn level so với luật do database kiểm tra.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException($"{name}: Equipment ID không được trống.");
        if (!Enum.IsDefined(typeof(EquipmentSlot), slot) || !Enum.IsDefined(typeof(EquipmentSet), set))
            throw new InvalidOperationException($"{name}: Slot hoặc Set không hợp lệ.");
        if (skillUnlockLevel < 1)
            throw new InvalidOperationException($"{name}: Skill Unlock Level phải >= 1.");
        if (skill != null)
            ValidateEquipmentSkill(skill, name);
    }

    /// <summary>Skill của đồ và của set: definition riêng nguồn Equipment, có đúng một cấp (dùng chung cho EquipmentDatabaseSO).</summary>
    public static void ValidateEquipmentSkill(SkillDataSO equipmentSkill, string owner)
    {
        if (equipmentSkill.Source != SkillSource.Equipment)
            throw new InvalidOperationException($"{owner}: skill {equipmentSkill.name} phải là definition nguồn Equipment.");
        if (equipmentSkill.MaxLevel != 1)
            throw new InvalidOperationException($"{owner}: skill {equipmentSkill.name} phải có đúng 1 cấp.");
    }

    private void OnValidate()
    {
        if (skillUnlockLevel < 1)
            skillUnlockLevel = 1;
    }

    #endregion
}
