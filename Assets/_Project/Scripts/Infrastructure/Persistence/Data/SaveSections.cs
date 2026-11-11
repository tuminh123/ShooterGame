using System;
using System.Collections.Generic;

// Schema 4 tách hồ sơ thành nhiều key trong cùng file; ID enum đã lưu luôn giữ nguyên.
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
}

[Serializable]
public sealed class EquipmentSaveData
{
    public List<string> equippedItemIds = new List<string>();
    public EquipmentSaveData Clone() => new EquipmentSaveData { equippedItemIds = new List<string>(equippedItemIds) };
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
}
