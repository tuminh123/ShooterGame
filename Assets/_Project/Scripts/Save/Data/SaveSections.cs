using System;
using System.Collections.Generic;

// Schema 4 tách hồ sơ thành nhiều key trong cùng file; ID enum đã lưu luôn giữ nguyên.
// Mỗi section tự kiểm tra dữ liệu của nó (Validate): service sở hữu dữ liệu và SaveMigration dùng chung một quy tắc,
// nên miền Save không phải tham chiếu ngược lên Progression/Character chỉ để kiểm tra.
[Serializable]
public sealed class SaveFormatData
{
    public const int CurrentVersion = 4;
    public int version = CurrentVersion;
}

[Serializable]
public sealed class PlayerSelectionSaveData
{
    public CharacterType selectedCharacterType = CharacterType.None;
    public PlayerSelectionSaveData Clone() => new PlayerSelectionSaveData { selectedCharacterType = selectedCharacterType };

    /// <summary>Nhân vật đang chọn phải là giá trị enum đã định nghĩa (None = chưa chọn).</summary>
    public static void Validate(PlayerSelectionSaveData value)
    {
        if (value == null || !Enum.IsDefined(typeof(CharacterType), value.selectedCharacterType))
            throw new InvalidOperationException("Selected character type is not defined.");
    }
}

[Serializable]
public sealed class CharacterLevelsSaveData
{
    [Serializable]
    public sealed class Entry
    {
        public CharacterType characterType;
        public int permanentLevel = 1;
    }
    public List<Entry> characters = new List<Entry>();
    public CharacterLevelsSaveData Clone()
    {
        var copy = new CharacterLevelsSaveData();
        foreach (Entry entry in characters)
            copy.characters.Add(new Entry { characterType = entry.characterType, permanentLevel = entry.permanentLevel });
        return copy;
    }

    /// <summary>Mỗi nhân vật xuất hiện tối đa một lần, type đã định nghĩa (khác None) và level dương.</summary>
    public static void Validate(CharacterLevelsSaveData value)
    {
        if (value == null || value.characters == null) throw new InvalidOperationException("Missing character level data.");
        var types = new HashSet<CharacterType>();
        foreach (Entry entry in value.characters)
            if (entry == null || entry.characterType == CharacterType.None ||
                !Enum.IsDefined(typeof(CharacterType), entry.characterType) || entry.permanentLevel < 1 || !types.Add(entry.characterType))
                throw new InvalidOperationException("Character progress must have unique defined types and positive levels.");
    }
}

[Serializable]
public sealed class EquipmentSaveData
{
    public List<string> equippedItemIds = new List<string>();
    public EquipmentSaveData Clone() => new EquipmentSaveData { equippedItemIds = new List<string>(equippedItemIds) };

    /// <summary>ID trang bị không rỗng và không trùng (phân biệt hoa/thường). Việc ID có trong database do Loadout kiểm tra.</summary>
    public static void Validate(EquipmentSaveData value)
    {
        if (value == null || value.equippedItemIds == null) throw new InvalidOperationException("Missing equipped items.");
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in value.equippedItemIds)
            if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                throw new InvalidOperationException("Equipment IDs must be non-empty and unique.");
    }
}

[Serializable]
public sealed class CurrencySaveData
{
    [Serializable]
    public sealed class Balance
    {
        public CurrencyType type;
        public long balance;
    }
    public List<Balance> currencies = CreateDefaults();
    private static List<Balance> CreateDefaults()
    {
        var entries = new List<Balance>();
        foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType))) entries.Add(new Balance { type = type });
        return entries;
    }
    public CurrencySaveData Clone()
    {
        var copy = new CurrencySaveData { currencies = new List<Balance>(currencies.Count) };
        foreach (Balance entry in currencies) copy.currencies.Add(new Balance { type = entry.type, balance = entry.balance });
        return copy;
    }

    /// <summary>Loại tiền đã định nghĩa, không trùng, số dư không âm.</summary>
    public static void Validate(CurrencySaveData value)
    {
        if (value == null || value.currencies == null) throw new InvalidOperationException("Missing currency balances.");
        var unique = new HashSet<CurrencyType>();
        foreach (Balance entry in value.currencies)
            if (entry == null || !Enum.IsDefined(typeof(CurrencyType), entry.type) || entry.balance < 0 || !unique.Add(entry.type))
                throw new InvalidOperationException("Currency types must be defined and unique with non-negative balances.");
    }
}

[Serializable]
public sealed class StageProgressSaveData
{
    // StageId -> true once the first victory reward has been granted.
    public Dictionary<string, bool> firstClearStages = new Dictionary<string, bool>(StringComparer.Ordinal);

    public StageProgressSaveData Clone() => new StageProgressSaveData
    {
        firstClearStages = new Dictionary<string, bool>(firstClearStages, StringComparer.Ordinal)
    };

    /// <summary>Có bảng cờ và mọi Stage ID đã lưu đều không rỗng.</summary>
    public static void Validate(StageProgressSaveData value)
    {
        if (value == null || value.firstClearStages == null)
            throw new InvalidOperationException("Missing stage first-clear flags.");
        foreach (string stageId in value.firstClearStages.Keys)
            if (string.IsNullOrWhiteSpace(stageId)) throw new InvalidOperationException("Stage ID must not be empty.");
    }
}
