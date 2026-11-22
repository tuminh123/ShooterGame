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
        public int upgradePoints; // Nguyên liệu đã nạp vào thanh của cấp hiện tại; save cũ không có field này nên đọc ra 0.
        public int stars; // Số sao đã mở; save cũ không có field này nên đọc ra 0.
    }
    public List<Entry> characters = new List<Entry>();
    public CharacterLevelsSaveData Clone()
    {
        var copy = new CharacterLevelsSaveData();
        foreach (Entry entry in characters)
            copy.characters.Add(new Entry
            {
                characterType = entry.characterType,
                permanentLevel = entry.permanentLevel,
                upgradePoints = entry.upgradePoints,
                stars = entry.stars
            });
        return copy;
    }

    /// <summary>Mỗi nhân vật xuất hiện tối đa một lần, type đã định nghĩa (khác None), level dương, điểm thanh và số sao không âm.</summary>
    public static void Validate(CharacterLevelsSaveData value)
    {
        if (value == null || value.characters == null) throw new InvalidOperationException("Missing character level data.");
        var types = new HashSet<CharacterType>();
        foreach (Entry entry in value.characters)
            if (entry == null || entry.characterType == CharacterType.None ||
                !Enum.IsDefined(typeof(CharacterType), entry.characterType) || entry.permanentLevel < 1 || entry.upgradePoints < 0 || entry.stars < 0 ||
                !types.Add(entry.characterType))
                throw new InvalidOperationException("Character progress must have unique defined types, positive levels and non-negative upgrade points and stars.");
    }
}

/// <summary>Túi đồ: mọi bản trang bị đang sở hữu (key <c>equipment_items</c>). Do EquipmentInventory sở hữu.</summary>
[Serializable]
public sealed class EquipmentItemsSaveData
{
    [Serializable]
    public sealed class Substat
    {
        public StatType stat;
        public float value;
    }

    [Serializable]
    public sealed class Item
    {
        public int uid;
        public string itemId; // ID mẫu đồ trong EquipmentDatabase; mất ID thì món bị xóa khi load.
        public EquipmentRarity rarity;
        public int level = 1;
        public List<Substat> substats = new List<Substat>(); // Kết quả random đã lưu, không random lại.
    }

    public int nextUid = 1;
    public bool starterItemsGranted; // Save cũ không có field này nên đọc ra false: nhận đồ khởi đầu một lần.
    public List<Item> items = new List<Item>();

    public EquipmentItemsSaveData Clone()
    {
        var copy = new EquipmentItemsSaveData { nextUid = nextUid, starterItemsGranted = starterItemsGranted };
        foreach (Item item in items)
        {
            var substats = new List<Substat>(item.substats.Count);
            foreach (Substat substat in item.substats)
                substats.Add(new Substat { stat = substat.stat, value = substat.value });
            copy.items.Add(new Item { uid = item.uid, itemId = item.itemId, rarity = item.rarity, level = item.level, substats = substats });
        }
        return copy;
    }

    /// <summary>
    /// UID dương, không trùng và nhỏ hơn nextUid; ID không trống; độ hiếm và chỉ số đã định nghĩa; level dương;
    /// dòng phụ không trùng nhau và là số hữu hạn. Việc ID có trong database và dòng phụ khớp luật do EquipmentInventory kiểm tra.
    /// </summary>
    public static void Validate(EquipmentItemsSaveData value)
    {
        if (value == null || value.items == null || value.nextUid < 1)
            throw new InvalidOperationException("Missing equipment items.");
        var uids = new HashSet<int>();
        foreach (Item item in value.items)
        {
            if (item == null || item.uid < 1 || item.uid >= value.nextUid || !uids.Add(item.uid) || string.IsNullOrWhiteSpace(item.itemId) ||
                !Enum.IsDefined(typeof(EquipmentRarity), item.rarity) || item.level < 1 || item.substats == null)
                throw new InvalidOperationException("Equipment items must have unique positive UIDs, IDs, defined rarities and positive levels.");
            var stats = new HashSet<StatType>();
            foreach (Substat substat in item.substats)
                if (substat == null || !Enum.IsDefined(typeof(StatType), substat.stat) || !stats.Add(substat.stat) ||
                    float.IsNaN(substat.value) || float.IsInfinity(substat.value))
                    throw new InvalidOperationException("Equipment substats must be defined, unique and finite.");
        }
    }
}

/// <summary>Đồ đang mặc: ô → UID (key <c>equipment_slots</c>). Do Loadout sở hữu.</summary>
[Serializable]
public sealed class EquipmentSlotsSaveData
{
    [Serializable]
    public sealed class Entry
    {
        public EquipmentSlot slot;
        public int uid;
    }

    public List<Entry> slots = new List<Entry>();

    public EquipmentSlotsSaveData Clone()
    {
        var copy = new EquipmentSlotsSaveData();
        foreach (Entry entry in slots)
            copy.slots.Add(new Entry { slot = entry.slot, uid = entry.uid });
        return copy;
    }

    /// <summary>Mỗi ô tối đa một món, mỗi món tối đa một ô, UID dương. Việc UID còn trong túi và đúng loại ô do Loadout kiểm tra.</summary>
    public static void Validate(EquipmentSlotsSaveData value)
    {
        if (value == null || value.slots == null)
            throw new InvalidOperationException("Missing equipment slots.");
        var slots = new HashSet<EquipmentSlot>();
        var uids = new HashSet<int>();
        foreach (Entry entry in value.slots)
            if (entry == null || !Enum.IsDefined(typeof(EquipmentSlot), entry.slot) || entry.uid < 1 || !slots.Add(entry.slot) || !uids.Add(entry.uid))
                throw new InvalidOperationException("Equipment slots must be defined and unique, with unique positive UIDs.");
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
    // StageId -> true khi đã trao thưởng thắng lần đầu.
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
