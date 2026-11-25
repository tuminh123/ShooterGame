using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Túi đồ của người chơi (key <c>equipment_items</c>): mọi bản trang bị đang sở hữu, nhận đồ, hỏi giá và nâng cấp.
/// </summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo; miền khác đọc qua <see cref="IEquipment"/> và <see cref="ICharacterProgress"/>.
/// Save chỉ giữ ID mẫu đồ + kết quả random; lúc load tra lại mẫu đồ, set và chỉ số chính một lần.
/// Mỗi thay đổi chép toàn bộ runtime ra snapshot rồi ghi, nên RAM là nguồn sự thật duy nhất.
/// </remarks>
public sealed class EquipmentInventory : IDisposable
{
    #region Fields

    private readonly ISaveStore saves;
    private readonly Wallet wallet; // Trừ tiền lên cấp và ghi cùng một lần với túi đồ (giống StageProgress).
    private readonly EquipmentDatabaseSO database;
    private readonly Dictionary<string, EquipmentDataSO> definitions;
    private readonly Dictionary<EquipmentSet, EquipmentSetData> sets;
    private readonly EquipmentGrowth growth;
    private readonly List<EquipmentItem> items = new List<EquipmentItem>();
    private int nextUid = 1;
    private bool starterItemsGranted;
    private bool loaded;
    private bool changing; // Chặn đổi túi đồ từ trong callback Changed, để thứ tự thông báo và snapshot luôn khớp nhau.
    private bool disposed;

    #endregion

    #region Events

    /// <summary>Phát sau khi túi đồ đổi (nhận đồ, lên cấp) và đã thử lưu.</summary>
    public event Action Changed;

    #endregion

    #region Constructor / Lifecycle

    /// <summary>Kiểm tra database và dựng bảng tra; chưa đọc save (SaveManager.Initialize chưa chạy).</summary>
    public EquipmentInventory(ISaveStore saves, Wallet wallet, EquipmentDatabaseSO database, System.Random random)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        this.database = database != null ? database : throw new ArgumentNullException(nameof(database));
        database.Validate();
        definitions = database.CreateItemLookup();
        sets = database.CreateSetLookup();
        growth = new EquipmentGrowth(database.Rules, random);
    }

    /// <summary>
    /// Đọc túi đồ một lần sau khi Save đã mở file: xóa món có ID không còn, sửa dòng phụ lệch luật,
    /// nhận đồ khởi đầu nếu chưa nhận. Có sửa thì ghi lại ngay.
    /// </summary>
    public void Load()
    {
        if (!saves.IsLoaded || loaded) return;
        EquipmentItemsSaveData data = saves.Load(GameConstants.SaveKeys.EquipmentItems, new EquipmentItemsSaveData(), EquipmentItemsSaveData.Validate);
        loaded = true;
        nextUid = data.nextUid;
        starterItemsGranted = data.starterItemsGranted;
        bool changed = false;
        foreach (EquipmentItemsSaveData.Item saved in data.items)
        {
            if (TryRestore(saved, out bool repaired))
                changed |= repaired;
            else
                changed = true;
        }
        if (!starterItemsGranted)
        {
            GrantStarterItems();
            changed = true;
        }
        if (changed)
            Save();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Changed = null;
        items.Clear();
    }

    #endregion

    #region Read

    /// <summary>Mọi bản đồ đang sở hữu, theo thứ tự nhận.</summary>
    public IReadOnlyList<EquipmentItem> Items
    {
        get
        {
            EnsureAvailable();
            return items;
        }
    }

    /// <summary>Tìm bản đồ theo UID.</summary>
    public bool TryGet(int uid, out EquipmentItem item)
    {
        EnsureAvailable();
        foreach (EquipmentItem candidate in items)
        {
            if (candidate.Uid == uid)
            {
                item = candidate;
                return true;
            }
        }
        item = null;
        return false;
    }

    /// <summary>Bản đồ này có thuộc túi (không phải bản cũ đã bị xóa) hay không.</summary>
    public bool Owns(EquipmentItem item) => item != null && items.Contains(item);

    #endregion

    #region Change

    /// <summary>
    /// Nhận một bản đồ mới (dòng phụ được roll ngay). Miền khác gọi qua <see cref="IEquipmentGrant"/>.
    /// <paramref name="saved"/> là kết quả ghi file.
    /// </summary>
    public EquipmentItem Grant(EquipmentDataSO definition, EquipmentRarity rarity, out bool saved)
    {
        EnsureCanChange();
        EquipmentItem item = CreateItem(definition, rarity, 1, growth.RollInitialSubstats);
        changing = true;
        try
        {
            saved = Save();
            Notify();
            return item;
        }
        finally { changing = false; }
    }

    /// <summary>Kiểm tra một bản đồ thưởng có tạo được không; ném lỗi nếu sai, không đổi gì.</summary>
    internal void CheckGrant(EquipmentGrant grant)
    {
        EnsureDefinition(grant.Item, grant.Rarity);
        if (grant.Level < 1 || grant.Level > growth.MaxLevel)
            throw new InvalidOperationException($"Reward {grant.Item.Id}: equipment level must be 1..{growth.MaxLevel}.");
        grant.Substats.Validate(database.Rules, grant.Item.Slot, grant.Rarity, grant.Level);
    }

    /// <summary>Thêm bản đồ thưởng vào túi, chỉ đổi RAM; RewardGranter ghi cùng các phần thưởng khác rồi gọi <see cref="PublishChanged"/>.</summary>
    internal EquipmentItem ApplyGrant(EquipmentGrant grant)
    {
        EnsureCanChange();
        return CreateItem(grant.Item, grant.Rarity, grant.Level,
            (slot, rarity) => grant.Substats.Create(growth, slot, rarity, grant.Level));
    }

    /// <summary>Báo túi đồ đổi sau khi đã thử lưu; trong lúc báo thì túi không được đổi tiếp.</summary>
    internal void PublishChanged()
    {
        changing = true;
        try { Notify(); }
        finally { changing = false; }
    }

    /// <summary>Level hiện tại/tối đa và giá nâng; MaxReached khi đã ở level tối đa, NotEnoughCurrency khi thiếu tiền.</summary>
    public UpgradeQuote GetQuote(EquipmentItem item)
    {
        EnsureOwned(item);
        int max = growth.MaxLevel;
        if (item.Level >= max)
            return new UpgradeQuote(item.Level, max, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.MaxReached);
        // Món càng hiếm càng đắt: hệ số theo độ hiếm nằm trong EquipmentRulesSO.
        var costs = new[] { growth.GetLevelUpCost(item.Level, item.Rarity) };
        UpgradeBlock block = wallet.CanAfford(costs) ? UpgradeBlock.None : UpgradeBlock.NotEnoughCurrency;
        return new UpgradeQuote(item.Level, max, 0, 0, costs, block);
    }

    /// <summary>
    /// Nâng một level nếu quote cho phép (chưa tối đa, đủ tiền): tăng chỉ số chính, áp mốc dòng phụ, trừ tiền,
    /// rồi ghi ví và túi đồ trong <b>một lần lưu</b>.
    /// Trả về đã nâng hay chưa; <paramref name="saved"/> = false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    public bool TryLevelUp(EquipmentItem item, out bool saved)
    {
        EnsureCanChange();
        saved = false;
        // Kiểm tra lại: UI có thể đang giữ một quote cũ.
        UpgradeQuote quote = GetQuote(item);
        if (!quote.CanUpgrade) return false;
        changing = true;
        try
        {
            growth.LevelUp(item);
            saved = wallet.SpendWith(quote.Costs, GameConstants.SaveKeys.EquipmentItems, CreateSaveData());
            Notify();
            return true;
        }
        finally { changing = false; }
    }

    /// <summary>Thử lưu lại snapshot túi đồ mà không đổi gì thêm.</summary>
    public bool Save()
    {
        EnsureAvailable();
        return saves.Save(GameConstants.SaveKeys.EquipmentItems, CreateSaveData());
    }

    // Chép toàn bộ runtime ra snapshot độc lập đã kiểm tra; dùng chung cho Save, lần ghi cùng ví khi lên cấp và gói thưởng.
    internal EquipmentItemsSaveData CreateSaveData()
    {
        var data = new EquipmentItemsSaveData { nextUid = nextUid, starterItemsGranted = starterItemsGranted };
        foreach (EquipmentItem item in items)
            data.items.Add(item.ToSaveData());
        EquipmentItemsSaveData.Validate(data);
        return data;
    }

    #endregion

    #region Private Helpers

    // Dựng runtime từ bản ghi. false nếu ID mẫu đồ không còn (món bị xóa); repaired = true nếu phải sửa level/dòng phụ.
    private bool TryRestore(EquipmentItemsSaveData.Item saved, out bool repaired)
    {
        repaired = false;
        if (!definitions.TryGetValue(saved.itemId, out EquipmentDataSO definition))
        {
            Debug.LogWarning($"Equipment ID '{saved.itemId}' (uid {saved.uid}) không còn trong Equipment Database; món này bị xóa khỏi túi đồ.");
            return false;
        }
        int level = Mathf.Clamp(saved.level, 1, growth.MaxLevel);
        var substats = new List<StatModifier>(saved.substats.Count);
        foreach (EquipmentItemsSaveData.Substat substat in saved.substats)
            substats.Add(new StatModifier(substat.stat, StatOperation.Add, substat.value));
        EquipmentItem item = Build(saved.uid, definition, saved.rarity, level, substats);
        repaired = growth.Repair(item) || level != saved.level;
        if (repaired)
            Debug.LogWarning($"Equipment uid {saved.uid} ({saved.itemId}) lệch luật hiện tại; level/dòng phụ đã được sửa lại.");
        items.Add(item);
        return true;
    }

    private void GrantStarterItems()
    {
        starterItemsGranted = true;
        foreach (EquipmentDatabaseSO.StarterItem starter in database.StarterItems)
            CreateItem(starter.Item, starter.Rarity, 1, growth.RollInitialSubstats);
    }

    private EquipmentItem CreateItem(EquipmentDataSO definition, EquipmentRarity rarity, int level,
        Func<EquipmentSlot, EquipmentRarity, List<StatModifier>> createSubstats)
    {
        EnsureDefinition(definition, rarity);
        List<StatModifier> substats = createSubstats(definition.Slot, rarity);
        EquipmentItem item = Build(nextUid, definition, rarity, level, substats);
        nextUid = checked(nextUid + 1);
        items.Add(item);
        return item;
    }

    private void EnsureDefinition(EquipmentDataSO definition, EquipmentRarity rarity)
    {
        if (definition == null || !definitions.TryGetValue(definition.Id, out EquipmentDataSO known) || known != definition)
            throw new ArgumentException("Equipment definition is not in Equipment Database.", nameof(definition));
        if (!Enum.IsDefined(typeof(EquipmentRarity), rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
    }

    private EquipmentItem Build(int uid, EquipmentDataSO definition, EquipmentRarity rarity, int level, List<StatModifier> substats)
    {
        EquipmentSetData set = null;
        if (definition.Set != EquipmentSet.None)
            set = sets[definition.Set];
        StatType mainStat = growth.GetMainStat(definition.Slot);
        float mainValue = growth.GetMainValue(definition.Slot, rarity, level);
        return new EquipmentItem(uid, definition, set, rarity, level, mainStat, mainValue, substats);
    }

    // Lỗi trình bày UI không được làm bên gọi tưởng thao tác chưa thực hiện.
    private void Notify()
    {
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void EnsureOwned(EquipmentItem item)
    {
        EnsureAvailable();
        if (!Owns(item))
            throw new ArgumentException("Equipment item is not in the inventory.", nameof(item));
    }

    /// <summary>Ném lỗi nếu túi chưa đọc xong hoặc đang trong callback của chính nó.</summary>
    internal void EnsureCanChange()
    {
        EnsureAvailable();
        if (changing)
            throw new InvalidOperationException("Cannot change equipment from an equipment notification callback.");
    }

    private void EnsureAvailable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(EquipmentInventory));
        if (!loaded || !saves.IsLoaded) throw new InvalidOperationException("Equipment data has not loaded.");
    }

    #endregion
}
