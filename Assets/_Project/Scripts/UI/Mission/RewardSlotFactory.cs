using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>Dựng ô thưởng từ dữ liệu gói thưởng; dùng chung cho màn Mission và popup kết quả trận.</summary>
public sealed class RewardSlotFactory
{
    #region Fields

    private readonly EquipmentThemeSO theme;
    private readonly string amountFormat; // {0} = số lượng.
    private readonly string levelFormat; // {0} = level.
    private readonly Color plainFrame; // Viền của thưởng không có độ hiếm.

    public RewardSlotFactory(EquipmentThemeSO theme, string amountFormat, string levelFormat, Color plainFrame)
    {
        this.theme = theme;
        this.amountFormat = amountFormat;
        this.levelFormat = levelFormat;
        this.plainFrame = plainFrame;
    }

    #endregion

    #region Build

    /// <summary>Phần thưởng sẽ nhận, theo cấu hình.</summary>
    public List<RewardSlotModel> Preview(IReadOnlyList<IReward> rewards)
    {
        RewardBatch batch = RewardBatch.From(rewards);
        var slots = new List<RewardSlotModel>();
        foreach (KeyValuePair<CurrencyType, long> currency in batch.Currencies) slots.Add(Currency(currency.Key, currency.Value));
        foreach (CharacterDataSO character in batch.Characters) slots.Add(Character(character));
        foreach (CharacterShardGrant shard in batch.Shards) slots.Add(Shard(shard));
        foreach (EquipmentGrant item in batch.Equipment) slots.Add(Equipment(item.Item, item.Rarity, item.Level));
        return slots;
    }

    /// <summary>Phần thưởng thực tế (nhân vật trùng đã thành mảnh).</summary>
    public List<RewardSlotModel> Result(RewardResult result)
    {
        var slots = new List<RewardSlotModel>();
        foreach (CurrencyCost currency in result.Currencies) slots.Add(Currency(currency.Currency, currency.Amount));
        foreach (CharacterDataSO character in result.NewCharacters) slots.Add(Character(character));
        foreach (CharacterShardGrant shard in result.Shards) slots.Add(Shard(shard));
        foreach (EquipmentItem item in result.Equipment) slots.Add(Equipment(item.Definition, item.Rarity, item.Level));
        return slots;
    }

    #endregion

    #region Slots

    private RewardSlotModel Currency(CurrencyType type, long amount)
        => new RewardSlotModel(theme.StatTheme.GetCurrencyIcon(type), Amount(amount), plainFrame,
            type == CurrencyType.Diamond ? RewardFlyTarget.Diamond : RewardFlyTarget.Gold, amount);

    private RewardSlotModel Character(CharacterDataSO character)
        => new RewardSlotModel(character.Portrait, character.DisplayName, plainFrame, RewardFlyTarget.Heroes);

    private RewardSlotModel Shard(CharacterShardGrant shard)
        => new RewardSlotModel(shard.Character.ShardIcon, Amount(shard.Amount), plainFrame, RewardFlyTarget.Heroes);

    private RewardSlotModel Equipment(EquipmentDataSO item, EquipmentRarity rarity, int level)
        => new RewardSlotModel(item.Icon, string.Format(CultureInfo.InvariantCulture, levelFormat, level), theme.GetRarity(rarity).TextColor,
            RewardFlyTarget.Bag);

    private string Amount(long amount)
        => string.Format(CultureInfo.InvariantCulture, amountFormat, amount.ToString("N0", CultureInfo.InvariantCulture));

    #endregion
}
