using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Một set trang bị: hiển thị, số món cần mặc và skill duy nhất (một cấp) của set.
/// </summary>
/// <remarks>Khóa là <see cref="EquipmentSet"/>; mỗi set khai báo đúng một lần trong <see cref="EquipmentDatabaseSO"/>.</remarks>
[Serializable]
public sealed class EquipmentSetData
{
    [SerializeField] private EquipmentSet set;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("Số món cùng set phải đang mặc để bật skill (1..6).")]
    [SerializeField, Range(1, 6)] private int requiredPieces = 2;
    [Tooltip("Skill duy nhất của set: definition nguồn Equipment, đúng 1 cấp.")]
    [SerializeField] private SkillDataSO skill;

    /// <summary>Khóa của set.</summary>
    public EquipmentSet Set => set;

    /// <summary>Tên hiển thị.</summary>
    public string DisplayName => displayName;

    /// <summary>Mô tả hiệu ứng set.</summary>
    public string Description => description;

    /// <summary>Icon của set.</summary>
    public Sprite Icon => icon;

    /// <summary>Số món cần mặc để bật skill.</summary>
    public int RequiredPieces => requiredPieces;

    /// <summary>Skill của set.</summary>
    public SkillDataSO Skill => skill;

    /// <summary>Nơi DUY NHẤT quyết định set đã bật chưa với số món đang mặc (Progression và UI cùng gọi).</summary>
    public bool IsActive(int equippedPieces) => equippedPieces >= requiredPieces;
}

/// <summary>
/// Danh mục trang bị: luật dùng chung, các mẫu đồ, các set và đồ khởi đầu. Save chỉ giữ ID mẫu đồ; mọi thứ khác đọc lại từ đây.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentDatabase", menuName = "Shooter Game/Data/Equipment Database")]
public sealed class EquipmentDatabaseSO : ScriptableObject
{
    #region Entry Types

    /// <summary>Một món đồ khởi đầu: mẫu và độ hiếm (dòng phụ được roll lúc nhận).</summary>
    [Serializable]
    public sealed class StarterItem
    {
        [SerializeField] private EquipmentDataSO item;
        [SerializeField] private EquipmentRarity rarity;

        public EquipmentDataSO Item => item;
        public EquipmentRarity Rarity => rarity;
    }

    #endregion

    #region Configuration

    [Tooltip("Luật tăng trưởng dùng chung (chỉ số chính, dòng phụ, level, giá).")]
    [SerializeField] private EquipmentRulesSO rules;
    [Tooltip("Mọi mẫu đồ của game; ID không trùng.")]
    [SerializeField] private List<EquipmentDataSO> items = new List<EquipmentDataSO>();
    [Tooltip("Mỗi EquipmentSet (trừ None) khai báo đúng một lần.")]
    [SerializeField] private List<EquipmentSetData> sets = new List<EquipmentSetData>();
    [Tooltip("Đồ người chơi nhận một lần khi save chưa từng nhận (save mới hoặc sau khi reset). Dùng để test.")]
    [SerializeField] private List<StarterItem> starterItems = new List<StarterItem>();

    /// <summary>Luật tăng trưởng dùng chung.</summary>
    public EquipmentRulesSO Rules => rules;

    /// <summary>Mọi mẫu đồ của game (panel test dùng để gacha đủ các món).</summary>
    public IReadOnlyList<EquipmentDataSO> Items => items;

    /// <summary>Đồ khởi đầu.</summary>
    public IReadOnlyList<StarterItem> StarterItems => starterItems;

    #endregion

    #region Lookups

    /// <summary>Tra mẫu đồ theo ID (phân biệt hoa/thường) cho system sở hữu; không cache trạng thái lên asset.</summary>
    public Dictionary<string, EquipmentDataSO> CreateItemLookup()
    {
        var lookup = new Dictionary<string, EquipmentDataSO>(StringComparer.Ordinal);
        foreach (EquipmentDataSO item in items)
        {
            if (item == null)
                throw new InvalidOperationException($"{name}: danh sách Items có phần tử trống.");
            item.Validate();
            if (lookup.ContainsKey(item.Id))
                throw new InvalidOperationException($"{name}: trùng Equipment ID {item.Id}.");
            lookup.Add(item.Id, item);
        }
        return lookup;
    }

    /// <summary>Tra set theo enum; mọi set (trừ None) phải có đúng một mục.</summary>
    public Dictionary<EquipmentSet, EquipmentSetData> CreateSetLookup()
    {
        var lookup = new Dictionary<EquipmentSet, EquipmentSetData>();
        foreach (EquipmentSetData set in sets)
        {
            if (set == null || set.Set == EquipmentSet.None || !Enum.IsDefined(typeof(EquipmentSet), set.Set))
                throw new InvalidOperationException($"{name}: set trống hoặc là None.");
            if (lookup.ContainsKey(set.Set))
                throw new InvalidOperationException($"{name}: set {set.Set} khai báo hai lần.");
            if (set.RequiredPieces < 1 || set.RequiredPieces > Enum.GetValues(typeof(EquipmentSlot)).Length)
                throw new InvalidOperationException($"{name}: set {set.Set} có Required Pieces ngoài 1..6.");
            if (set.Skill == null)
                throw new InvalidOperationException($"{name}: set {set.Set} chưa có skill.");
            EquipmentDataSO.ValidateEquipmentSkill(set.Skill, $"{name} / set {set.Set}");
            lookup.Add(set.Set, set);
        }
        foreach (EquipmentSet set in (EquipmentSet[])Enum.GetValues(typeof(EquipmentSet)))
        {
            if (set != EquipmentSet.None && !lookup.ContainsKey(set))
                throw new InvalidOperationException($"{name}: thiếu khai báo set {set}.");
        }
        return lookup;
    }

    #endregion

    #region Validation

    /// <summary>Kiểm tra toàn bộ danh mục: luật, mẫu đồ, set, đồ khởi đầu và skill mở trong giới hạn level.</summary>
    public void Validate()
    {
        if (rules == null)
            throw new InvalidOperationException($"{name}: chưa gán Equipment Rules.");
        rules.Validate();
        Dictionary<string, EquipmentDataSO> lookup = CreateItemLookup();
        CreateSetLookup();
        foreach (EquipmentDataSO item in lookup.Values)
        {
            if (item.Skill != null && item.SkillUnlockLevel > rules.MaxLevel)
                throw new InvalidOperationException($"{item.name}: Skill Unlock Level vượt level tối đa {rules.MaxLevel}.");
        }
        foreach (StarterItem starter in starterItems)
        {
            if (starter == null || starter.Item == null || !items.Contains(starter.Item))
                throw new InvalidOperationException($"{name}: đồ khởi đầu trống hoặc không có trong Items.");
            if (!Enum.IsDefined(typeof(EquipmentRarity), starter.Rarity))
                throw new InvalidOperationException($"{name}: đồ khởi đầu có độ hiếm không hợp lệ.");
        }
    }

    #endregion
}
