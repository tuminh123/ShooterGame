using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trao cả gói thưởng: kiểm tra toàn bộ → đổi RAM (ví, nhân vật, túi đồ) → ghi MỘT lần kèm tiến trình của bên gọi → báo event.
/// </summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo. Luật của từng loại vẫn nằm ở hệ thống của loại đó; lớp này chỉ điều phối thứ tự,
/// để không loại thưởng nào được ghi riêng lẻ (lỗi giữa chừng sẽ mất thưởng hoặc nhận hai lần).
/// </remarks>
internal sealed class RewardGranter
{
    #region Fields

    private readonly ISaveStore saves;
    private readonly Wallet wallet;
    private readonly CharacterLevels levels;
    private readonly CharacterCollection collection;
    private readonly EquipmentInventory inventory;

    #endregion

    #region Constructor

    internal RewardGranter(ISaveStore saves, Wallet wallet, CharacterLevels levels, CharacterCollection collection, EquipmentInventory inventory)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

    #endregion

    #region Grant

    private static readonly CurrencyCost[] NoCost = Array.Empty<CurrencyCost>();

    /// <summary>Kiểm tra gói có trao được không; ném lỗi nếu cấu hình sai, không đổi gì.</summary>
    internal void Validate(IReadOnlyList<IReward> rewards) => Check(RewardBatch.From(rewards), NoCost);

    /// <summary>
    /// Trao gói và ghi cùng <paramref name="progress"/> (snapshot "đã nhận" của bên gọi).
    /// <paramref name="saved"/> = false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    internal RewardResult Grant(IReadOnlyList<IReward> rewards, SaveEntry progress, out bool saved)
        => Purchase(NoCost, rewards, progress, out saved);

    /// <summary>
    /// Trừ <paramref name="costs"/>, trao gói và ghi cùng <paramref name="progress"/> trong MỘT lần ghi (mua gói shop):
    /// không bao giờ mất tiền mà không nhận quà, hay nhận quà mà không trừ tiền. Thiếu tiền thì ném lỗi, không đổi gì.
    /// </summary>
    internal RewardResult Purchase(IReadOnlyList<CurrencyCost> costs, IReadOnlyList<IReward> rewards, SaveEntry progress, out bool saved)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        RewardBatch batch = RewardBatch.From(rewards);
        Dictionary<CurrencyType, long> balances = Check(batch, costs);

        var result = new RewardResult();
        wallet.SetBalances(balances);
        foreach (KeyValuePair<CurrencyType, long> currency in batch.Currencies)
            result.Add(new CurrencyCost(currency.Key, currency.Value));
        ApplyCharacters(batch, result);
        foreach (EquipmentGrant grant in batch.Equipment)
            result.Add(inventory.ApplyGrant(grant));

        // Chỉ ghi section thật sự đổi.
        bool charactersChanged = batch.Characters.Count > 0 || batch.Shards.Count > 0;
        bool equipmentChanged = batch.Equipment.Count > 0;
        var entries = new List<SaveEntry> { progress };
        if (balances.Count > 0) entries.Add(SaveEntry.Of(GameConstants.SaveKeys.Currencies, wallet.CreateSnapshot()));
        if (charactersChanged) entries.Add(SaveEntry.Of(GameConstants.SaveKeys.CharacterLevels, levels.CreateSnapshot()));
        if (equipmentChanged) entries.Add(SaveEntry.Of(GameConstants.SaveKeys.EquipmentItems, inventory.CreateSaveData()));
        saved = saves.SaveBatch(entries);

        bool written = saved; // Lambda không bắt được tham số out.
        Publish(() => wallet.PublishBalances(balances, written));
        if (charactersChanged) Publish(collection.PublishChanged);
        if (equipmentChanged) Publish(inventory.PublishChanged);
        return result;
    }

    private void ApplyCharacters(RewardBatch batch, RewardResult result)
    {
        foreach (CharacterDataSO character in batch.Characters)
        {
            CharacterGrantResult granted = collection.ApplyGrant(character.CharacterType, out CharacterShards gained);
            if (granted == CharacterGrantResult.NewCharacter) result.Add(character);
            else result.Add(new CharacterShardGrant(character, gained.Amount));
        }
        foreach (CharacterShardGrant shard in batch.Shards)
        {
            collection.ApplyShards(shard.Character.CharacterType, shard.Amount);
            result.Add(shard);
        }
    }

    #endregion

    #region Private Helpers

    // Kiểm tra hết trước khi đổi bất kỳ thứ gì; trả về số dư mới của ví (đã trừ giá, cộng thưởng) để bước trao dùng đúng số đã kiểm tra.
    private Dictionary<CurrencyType, long> Check(RewardBatch batch, IReadOnlyList<CurrencyCost> costs)
    {
        Dictionary<CurrencyType, long> balances = wallet.PlanPurchase(costs, batch.Currencies);
        foreach (CharacterDataSO character in batch.Characters)
            levels.GetConfig(character.CharacterType); // Ném lỗi nếu không phải nhân vật Player trong database.
        foreach (CharacterShardGrant shard in batch.Shards)
            levels.GetConfig(shard.Character.CharacterType);
        inventory.EnsureCanChange();
        foreach (EquipmentGrant grant in batch.Equipment)
            inventory.CheckGrant(grant);
        return balances;
    }

    // Đã trao xong: lỗi của listener không được làm bên gọi tưởng chưa trao.
    private static void Publish(Action publish)
    {
        try { publish(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    #endregion
}
