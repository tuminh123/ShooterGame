using System;
using System.Collections.Generic;

/// <summary>Loadout chung của người chơi (key <c>equipment</c>); chưa quản lý inventory, slot, mở khóa hoặc nâng cấp vật phẩm.</summary>
/// <remarks>Do <see cref="ProgressionManager"/> tạo; thay cho PlayerEquipmentService cũ. Miền khác chỉ thấy tổng bonus qua <see cref="IPermanentBonuses"/>.</remarks>
public sealed class Loadout : IDisposable
{

    #region Dependencies And State
    private readonly ISaveStore saves;
    private readonly Dictionary<string, StatBonus> bonuses;
    private EquipmentSaveData data;
    private bool disposed;
    private bool changingEquipment;

    public event Action EquipmentChanged;

    #endregion

    #region Initialization

    public Loadout(ISaveStore saves, EquipmentDatabaseSO database)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        if (database == null)
            throw new ArgumentNullException(nameof(database));
        bonuses = database.CreateBonusLookup();
    }

    /// <summary>Đọc loadout một lần sau khi Save đã mở file.</summary>
    public void Load()
    {
        if (!saves.IsLoaded || data != null) return;
        data = saves.Load(GameConstants.SaveKeys.Equipment, new EquipmentSaveData(), EquipmentSaveData.Validate);
    }

    #endregion

    #region Dependencies And State

    public IReadOnlyList<string> EquippedItemIds
    {
        get { EnsureAvailable(); return data.equippedItemIds.ToArray(); }
    }

    public StatBonus TotalBonus
    {
        get
        {
            EnsureAvailable();
            StatBonus total = default;
            foreach (string id in data.equippedItemIds)
                total += GetBonus(id);
            return total;
        }
    }

    #endregion

    #region Runtime Operations

    // Thay toàn bộ loadout, kiểm tra trước khi sửa RAM. false nghĩa là IO lỗi; không cần mặc lại.
    public bool SetEquippedItems(IEnumerable<string> itemIds)
    {
        EnsureAvailable();
        if (changingEquipment)
            throw new InvalidOperationException("Cannot change equipment from an equipment notification callback.");
        if (itemIds == null)
            throw new ArgumentNullException(nameof(itemIds));
        var ids = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in itemIds)
        {
            GetBonus(id);
            if (!unique.Add(id))
                throw new ArgumentException($"Duplicate equipped item ID: {id}.", nameof(itemIds));
            ids.Add(id);
        }

        changingEquipment = true;
        try
        {
            bool changed = ids.Count != data.equippedItemIds.Count || !unique.SetEquals(data.equippedItemIds);
            if (changed) data.equippedItemIds = ids;
            bool saved = !changed || SaveEquipment(); // Không đổi thì không ghi; thử ghi lại do SaveManager lo.
            if (changed)
                EquipmentChanged?.Invoke();
            return saved;
        }
        finally
        {
            changingEquipment = false;
        }
    }

    #endregion

    #region Persistence

    public bool SaveEquipment()
    {
        EnsureAvailable();
        EquipmentSaveData.Validate(data);
        return saves.Save(GameConstants.SaveKeys.Equipment, data.Clone());
    }

    #endregion

    #region Runtime Operations

    private StatBonus GetBonus(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !bonuses.TryGetValue(id, out StatBonus bonus))
            throw new KeyNotFoundException($"Unknown equipment ID: {id}. Check EquipmentDatabase.");
        return bonus;
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        EquipmentChanged = null;
        bonuses.Clear();
        data = null;
    }

    #endregion

    #region Validation

    private void EnsureAvailable()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(Loadout));
        if (data == null || !saves.IsLoaded) throw new InvalidOperationException("Equipment data has not loaded.");
    }

    #endregion
}
