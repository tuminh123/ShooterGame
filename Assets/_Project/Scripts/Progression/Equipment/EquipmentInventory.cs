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
    public EquipmentInventory(ISaveStore saves, EquipmentDatabaseSO database, System.Random random)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
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
        EquipmentItem item = CreateItem(definition, rarity);
        changing = true;
        try
        {
            saved = Save();
            Notify();
            return item;
        }
        finally { changing = false; }
    }

    /// <summary>Level hiện tại/tối đa và giá nâng; MaxReached khi đã ở level tối đa.</summary>
    public UpgradeQuote GetQuote(EquipmentItem item)
    {
        EnsureOwned(item);
        int max = growth.MaxLevel;
        if (item.Level >= max)
            return new UpgradeQuote(item.Level, max, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.MaxReached);
        var costs = new[] { growth.GetLevelUpCost(item.Level) };
        // TODO: kiểm tra số dư qua Wallet; thiếu tiền thì trả UpgradeBlock.NotEnoughCurrency (giống LevelTrack).
        return new UpgradeQuote(item.Level, max, 0, 0, costs, UpgradeBlock.None);
    }

    /// <summary>
    /// Nâng một level nếu quote cho phép: tăng chỉ số chính, áp mốc dòng phụ, ghi file.
    /// Trả về đã nâng hay chưa; <paramref name="saved"/> = false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    public bool TryLevelUp(EquipmentItem item, out bool saved)
    {
        EnsureCanChange();
        saved = false;
        // Kiểm tra lại: UI có thể đang giữ một quote cũ.
        if (!GetQuote(item).CanUpgrade) return false;
        // TODO: trừ tiền trong cùng một lần save với việc lên cấp (SaveBatch currencies + equipment_items).
        changing = true;
        try
        {
            growth.LevelUp(item);
            saved = Save();
            Notify();
            return true;
        }
        finally { changing = false; }
    }

    /// <summary>Thử lưu lại snapshot túi đồ mà không đổi gì thêm.</summary>
    public bool Save()
    {
        EnsureAvailable();
        var data = new EquipmentItemsSaveData { nextUid = nextUid, starterItemsGranted = starterItemsGranted };
        foreach (EquipmentItem item in items)
            data.items.Add(item.ToSaveData());
        EquipmentItemsSaveData.Validate(data);
        return saves.Save(GameConstants.SaveKeys.EquipmentItems, data);
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
            CreateItem(starter.Item, starter.Rarity);
    }

    private EquipmentItem CreateItem(EquipmentDataSO definition, EquipmentRarity rarity)
    {
        if (definition == null || !definitions.TryGetValue(definition.Id, out EquipmentDataSO known) || known != definition)
            throw new ArgumentException("Equipment definition is not in Equipment Database.", nameof(definition));
        if (!Enum.IsDefined(typeof(EquipmentRarity), rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        List<StatModifier> substats = growth.RollInitialSubstats(definition.Slot, rarity);
        EquipmentItem item = Build(nextUid, definition, rarity, 1, substats);
        nextUid = checked(nextUid + 1);
        items.Add(item);
        return item;
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

    private void EnsureCanChange()
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
