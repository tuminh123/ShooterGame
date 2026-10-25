using System;
using System.Collections.Generic;

// * Loadout chung của người chơi; chưa quản lý inventory, slot, mở khóa hoặc nâng cấp vật phẩm.
public sealed class PlayerEquipmentService : IDisposable
{
    private readonly SaveManager saves;
    private readonly Dictionary<string, StatBonus> bonuses;
    private bool disposed;
    private bool changingEquipment;

    public event Action EquipmentChanged;

    public PlayerEquipmentService(SaveManager saves, EquipmentDatabaseSO database)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        if (database == null)
            throw new ArgumentNullException(nameof(database));
        bonuses = database.CreateBonusLookup();
    }

    public IReadOnlyList<string> EquippedItemIds
    {
        get { EnsureAvailable(); return saves.GetEquippedItemIds(); }
    }

    public StatBonus TotalBonus
    {
        get
        {
            EnsureAvailable();
            StatBonus total = default;
            foreach (string id in saves.GetEquippedItemIds())
                total += GetBonus(id);
            return total;
        }
    }

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
            bool changed = saves.SetEquippedItemIds(ids);
            bool saved = !saves.IsDirty || saves.Save();
            if (changed)
                EquipmentChanged?.Invoke();
            return saved;
        }
        finally
        {
            changingEquipment = false;
        }
    }

    private StatBonus GetBonus(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !bonuses.TryGetValue(id, out StatBonus bonus))
            throw new KeyNotFoundException($"Unknown equipment ID: {id}. Check EquipmentDatabase.");
        return bonus;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        EquipmentChanged = null;
        bonuses.Clear();
    }

    private void EnsureAvailable()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PlayerEquipmentService));
    }
}
