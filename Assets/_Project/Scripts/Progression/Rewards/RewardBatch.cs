using System;
using System.Collections.Generic;

#region Entries

/// <summary>Mảnh của một nhân vật trong gói thưởng.</summary>
public readonly struct CharacterShardGrant
{
    public readonly CharacterDataSO Character;
    public readonly long Amount;

    public CharacterShardGrant(CharacterDataSO character, long amount)
    {
        Character = character;
        Amount = amount;
    }
}

/// <summary>Một bản trang bị trong gói thưởng: mẫu, độ hiếm, level và cách có dòng phụ.</summary>
public readonly struct EquipmentGrant
{
    public readonly EquipmentDataSO Item;
    public readonly EquipmentRarity Rarity;
    public readonly int Level;
    public readonly IEquipmentSubstats Substats;

    public EquipmentGrant(EquipmentDataSO item, EquipmentRarity rarity, int level, IEquipmentSubstats substats)
    {
        Item = item;
        Rarity = rarity;
        Level = level;
        Substats = substats;
    }
}

#endregion

/// <summary>Nội dung một gói thưởng trước khi trao, gom theo loại. UI cũng đọc lớp này để hiện trước phần thưởng.</summary>
public sealed class RewardBatch
{
    #region Content

    private readonly Dictionary<CurrencyType, long> currencies = new Dictionary<CurrencyType, long>();
    private readonly List<CharacterDataSO> characters = new List<CharacterDataSO>();
    private readonly List<CharacterShardGrant> shards = new List<CharacterShardGrant>();
    private readonly List<EquipmentGrant> equipment = new List<EquipmentGrant>();

    /// <summary>Tổng từng loại tiền.</summary>
    public IReadOnlyDictionary<CurrencyType, long> Currencies => currencies;

    /// <summary>Nhân vật được trao, theo thứ tự; đã có thì thành mảnh lúc trao.</summary>
    public IReadOnlyList<CharacterDataSO> Characters => characters;

    /// <summary>Mảnh riêng của từng nhân vật.</summary>
    public IReadOnlyList<CharacterShardGrant> Shards => shards;

    /// <summary>Trang bị được trao; mỗi phần tử là một bản đồ.</summary>
    public IReadOnlyList<EquipmentGrant> Equipment => equipment;

    #endregion

    #region Build

    /// <summary>Gom mọi reward của một danh sách thành một gói (kiểm tra tham số từng reward).</summary>
    public static RewardBatch From(IReadOnlyList<IReward> rewards)
    {
        if (rewards == null) throw new ArgumentNullException(nameof(rewards));
        var batch = new RewardBatch();
        foreach (IReward reward in rewards)
        {
            if (reward == null) throw new InvalidOperationException("Reward list has an empty entry.");
            reward.Validate();
            reward.AddTo(batch);
        }
        return batch;
    }

    /// <summary>Cộng tiền; cùng loại thì gộp.</summary>
    public void AddCurrency(CurrencyType type, long amount)
    {
        currencies.TryGetValue(type, out long current);
        currencies[type] = checked(current + amount);
    }

    /// <summary>Thêm một nhân vật.</summary>
    public void AddCharacter(CharacterDataSO character) => characters.Add(character);

    /// <summary>Thêm mảnh của một nhân vật.</summary>
    public void AddShards(CharacterDataSO character, long amount) => shards.Add(new CharacterShardGrant(character, amount));

    /// <summary>Thêm một bản trang bị.</summary>
    public void AddEquipment(EquipmentGrant grant) => equipment.Add(grant);

    #endregion
}

/// <summary>Kết quả THỰC TẾ sau khi trao (nhân vật trùng đã thành mảnh, dòng phụ đã roll), để popup hiện đúng.</summary>
public sealed class RewardResult
{
    private readonly List<CurrencyCost> currencies = new List<CurrencyCost>();
    private readonly List<CharacterDataSO> newCharacters = new List<CharacterDataSO>();
    private readonly List<CharacterShardGrant> shards = new List<CharacterShardGrant>();
    private readonly List<EquipmentItem> equipment = new List<EquipmentItem>();

    /// <summary>Tiền đã cộng.</summary>
    public IReadOnlyList<CurrencyCost> Currencies => currencies;

    /// <summary>Nhân vật vừa sở hữu lần đầu.</summary>
    public IReadOnlyList<CharacterDataSO> NewCharacters => newCharacters;

    /// <summary>Mảnh đã nhận, gồm cả mảnh từ nhân vật trùng.</summary>
    public IReadOnlyList<CharacterShardGrant> Shards => shards;

    /// <summary>Các bản đồ vừa vào túi.</summary>
    public IReadOnlyList<EquipmentItem> Equipment => equipment;

    internal void Add(CurrencyCost currency) => currencies.Add(currency);
    internal void Add(CharacterDataSO character) => newCharacters.Add(character);
    internal void Add(CharacterShardGrant shard) => shards.Add(shard);
    internal void Add(EquipmentItem item) => equipment.Add(item);

    /// <summary>Cộng thêm một kết quả khác (thưởng x2, vàng nhặt trong trận); tiền và mảnh cùng loại thì gộp.</summary>
    public void Include(RewardResult other)
    {
        foreach (CurrencyCost added in other.currencies)
        {
            int index = currencies.FindIndex(item => item.Currency == added.Currency);
            if (index < 0) currencies.Add(added);
            else currencies[index] = new CurrencyCost(added.Currency, checked(currencies[index].Amount + added.Amount));
        }
        foreach (CharacterShardGrant added in other.shards)
        {
            int index = shards.FindIndex(item => item.Character == added.Character);
            if (index < 0) shards.Add(added);
            else shards[index] = new CharacterShardGrant(added.Character, checked(shards[index].Amount + added.Amount));
        }
        newCharacters.AddRange(other.newCharacters);
        equipment.AddRange(other.equipment);
    }
}
