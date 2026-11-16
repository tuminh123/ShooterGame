using System;
using System.Collections.Generic;
using Zenject;

// * Loadout chung của người chơi; chưa quản lý inventory, slot, mở khóa hoặc nâng cấp vật phẩm.
public sealed class PlayerEquipmentService : IInitializable, IDisposable, IEquipmentService, IEquipmentReader, IEquipmentCommands
{

    #region Dependencies And State
    private readonly ISaveReader saveReader;
    private readonly ISaveWriter saveWriter;
    private readonly ISaveFlush saveFlush;
    private readonly Dictionary<string, StatBonus> bonuses;
    private EquipmentSaveData data;
    private bool disposed;
    private bool changingEquipment;

    public event Action EquipmentChanged;

    #endregion

    #region Initialization

    public PlayerEquipmentService(ISaveService saveService, EquipmentDatabaseSO database)
    {
        this.saveReader = saveService.GetSaveReader() ?? throw new ArgumentNullException(nameof(saveService));
        this.saveWriter = saveService.GetSaveWriter() ?? throw new ArgumentNullException(nameof(saveService));
        this.saveFlush = saveService.GetSaveFlush() ?? throw new ArgumentNullException(nameof(saveService));
        if (database == null)
            throw new ArgumentNullException(nameof(database));
        bonuses = database.CreateBonusLookup();
    }

    public void Initialize()
    {
        if (!saveReader.IsLoaded || data != null) return;
        data = saveReader.Load(GameConstants.SaveKeys.Equipment, new EquipmentSaveData(), ValidateData);
    }

    #endregion

    #region Validation

    internal static void ValidateData(EquipmentSaveData value)
    {
        if (value == null || value.equippedItemIds == null) throw new InvalidOperationException("Missing equipped items.");
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in value.equippedItemIds)
            if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                throw new InvalidOperationException("Equipment IDs must be non-empty and unique.");
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

    // * Thay toàn bộ loadout, kiểm tra trước khi sửa RAM. false nghĩa là IO lỗi; không cần mặc lại.
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
            bool saved = changed ? SaveEquipment() : saveFlush.SaveIfDirty();
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
        ValidateData(data);
        return saveWriter.Save(GameConstants.SaveKeys.Equipment, data.Clone());
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
            throw new ObjectDisposedException(nameof(PlayerEquipmentService));
        if (data == null || !saveReader.IsLoaded) throw new InvalidOperationException("Equipment data has not loaded.");
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IEquipmentReader GetEquipmentReader() => this;

    public IEquipmentCommands GetEquipmentCommands() => this;

    #endregion
}
