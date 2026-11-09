using System;
using System.Collections.Generic;
using UnityEngine;

// Catalog riêng cho skill nhận trong gameplay; skill vốn có của nhân vật nằm trên CharacterDataSO.
[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Shooter Game/Data/Skill Database")]
public sealed class SkillDatabaseSO : ScriptableObject
{
    #region Catalog Entries

    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private SkillDataSO skill; // Template dùng chung; database không tạo hoặc giữ runtime.
        [SerializeField] private bool availableInRunChoices; // Bật/tắt một skill gameplay trong bảng thưởng.

        public SkillDataSO Skill => skill;
        public bool AvailableInRunChoices => availableInRunChoices;
    }

    #endregion

    #region Catalog Storage

    [SerializeField] private List<Entry> skills = new List<Entry>(); // Catalog gameplay, mỗi asset xuất hiện đúng một lần.
    public IReadOnlyList<Entry> Skills => skills.AsReadOnly();

    #endregion

    #region Validation

    // Kiểm tra catalog và từng template trước khi bind; reference thiếu/trùng phải sửa ngay ở asset.
    public void Validate()
    {
        if (skills == null || skills.Count == 0)
            throw new InvalidOperationException("Skill database must contain skills.");
        var unique = new HashSet<SkillDataSO>(); // Chống cùng một asset được khai báo thành nhiều entry.
        foreach (Entry entry in skills)
        {
            if (entry == null || entry.Skill == null)
                throw new InvalidOperationException("Skill database contains a missing skill.");
            if (!unique.Add(entry.Skill))
                throw new InvalidOperationException($"Duplicate skill in database: {entry.Skill.name}.");
            if (entry.Skill.Source != SkillSource.Gameplay)
                throw new InvalidOperationException($"Character/equipment skill cannot enter gameplay database: {entry.Skill.name}.");
            entry.Skill.Validate();
        }
    }

    #endregion

    #region Reward Catalog Snapshot

    // Snapshot riêng cho bên dùng; không thay đổi catalog trên asset trong lúc chơi.
    public IReadOnlyList<SkillDataSO> CreateRunChoiceCatalog()
    {
        Validate();
        var result = new List<SkillDataSO>(); // List mới để bên dùng không thay đổi danh sách serialized của database.
        foreach (Entry entry in skills)
            if (entry.AvailableInRunChoices && entry.Skill.Source == SkillSource.Gameplay) result.Add(entry.Skill);
        return result.AsReadOnly();
    }
    #endregion
}
