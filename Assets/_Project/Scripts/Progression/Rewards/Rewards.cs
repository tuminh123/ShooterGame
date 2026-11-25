using System;
using UnityEngine;

// Các loại thưởng (strategy của IReward). Chỉ chứa tham số; asset chọn bằng [SerializeReference].
// Constructor có tham số dùng khi điền asset hoặc kiểm tra bằng code.

#region Currency

/// <summary>Tiền hoặc tài nguyên đếm số lượng (CurrencyType).</summary>
[Serializable]
public sealed class CurrencyReward : IReward
{
    [SerializeField] private CurrencyType currency = CurrencyType.Gold;
    [SerializeField, Min(1)] private int amount = 100;

    public CurrencyReward() { }

    public CurrencyReward(CurrencyType currency, int amount)
    {
        this.currency = currency;
        this.amount = amount;
    }

    /// <inheritdoc/>
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(CurrencyType), currency) || amount < 1)
            throw new InvalidOperationException("Currency reward needs a defined currency and amount >= 1.");
    }

    /// <inheritdoc/>
    public void AddTo(RewardBatch batch) => batch.AddCurrency(currency, amount);
}

#endregion

#region Character

/// <summary>Một nhân vật Player; đã có thì thành mảnh riêng của nhân vật đó (cùng luật gacha).</summary>
[Serializable]
public sealed class CharacterReward : IReward
{
    [SerializeField] private CharacterDataSO character;

    public CharacterReward() { }

    public CharacterReward(CharacterDataSO character) => this.character = character;

    /// <inheritdoc/>
    public void Validate() => RewardChecks.RequirePlayer(character);

    /// <inheritdoc/>
    public void AddTo(RewardBatch batch) => batch.AddCharacter(character);
}

/// <summary>Mảnh riêng của một nhân vật Player.</summary>
[Serializable]
public sealed class CharacterShardReward : IReward
{
    [SerializeField] private CharacterDataSO character;
    [SerializeField, Min(1)] private int amount = 10;

    public CharacterShardReward() { }

    public CharacterShardReward(CharacterDataSO character, int amount)
    {
        this.character = character;
        this.amount = amount;
    }

    /// <inheritdoc/>
    public void Validate()
    {
        RewardChecks.RequirePlayer(character);
        if (amount < 1) throw new InvalidOperationException("Shard reward needs amount >= 1.");
    }

    /// <inheritdoc/>
    public void AddTo(RewardBatch batch) => batch.AddShards(character, amount);
}

// Kiểm tra dùng chung của hai reward nhân vật.
internal static class RewardChecks
{
    internal static void RequirePlayer(CharacterDataSO character)
    {
        if (character == null || character.Team != CharacterTeam.Player)
            throw new InvalidOperationException("Character reward needs a Player character.");
    }
}

#endregion

#region Equipment

/// <summary>Trang bị với độ hiếm, level và dòng phụ chỉnh được. Chỉ số chính luôn tính theo ô, độ hiếm và level.</summary>
[Serializable]
public sealed class EquipmentReward : IReward
{
    [SerializeField] private EquipmentDataSO item;
    [SerializeField] private EquipmentRarity rarity;
    [SerializeField, Min(1)] private int level = 1;
    [Tooltip("Số bản; mỗi bản roll dòng phụ riêng.")]
    [SerializeField, Min(1)] private int quantity = 1;
    [Tooltip("Rolled: roll như đồ thường. Fixed: dòng phụ tự nhập.")]
    [SerializeReference] private IEquipmentSubstats substats = new RolledSubstats();

    public EquipmentReward() { }

    public EquipmentReward(EquipmentDataSO item, EquipmentRarity rarity, int level, int quantity, IEquipmentSubstats substats)
    {
        this.item = item;
        this.rarity = rarity;
        this.level = level;
        this.quantity = quantity;
        this.substats = substats;
    }

    /// <inheritdoc/>
    public void Validate()
    {
        if (item == null || !Enum.IsDefined(typeof(EquipmentRarity), rarity) || level < 1 || quantity < 1 || substats == null)
            throw new InvalidOperationException("Equipment reward needs an item, defined rarity, level >= 1, quantity >= 1 and a substat source.");
    }

    /// <inheritdoc/>
    public void AddTo(RewardBatch batch)
    {
        for (int i = 0; i < quantity; i++)
            batch.AddEquipment(new EquipmentGrant(item, rarity, level, substats));
    }
}

#endregion
