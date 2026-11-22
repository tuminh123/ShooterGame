using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đồ đang mặc, dùng chung cho mọi nhân vật (key <c>equipment_slots</c>): mỗi ô một bản đồ, đếm set, set đang bật và tổng bonus.
/// </summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo, đọc sau <see cref="EquipmentInventory"/> vì ô chỉ lưu UID của bản đồ.
/// Miền khác thấy tổng bonus qua <see cref="IPermanentBonuses"/>, đồ/set đang mặc qua <see cref="ICharacterProgress"/>,
/// và mặc/tháo qua <see cref="IEquipment"/>.
/// </remarks>
public sealed class Loadout : IDisposable
{
    #region Fields

    private static readonly EquipmentSlot[] Slots = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)); // Thứ tự slot = thứ tự ưu tiên skill.

    private readonly ISaveStore saves;
    private readonly EquipmentInventory inventory;
    private readonly Dictionary<EquipmentSlot, EquipmentItem> equipped = new Dictionary<EquipmentSlot, EquipmentItem>();
    private readonly Dictionary<EquipmentSet, int> setCounts = new Dictionary<EquipmentSet, int>(); // Dùng lại khi liệt kê set đang bật.
    private bool loaded;
    private bool changing; // Chặn đổi đồ lồng nhau từ trong callback EquipmentChanged.
    private bool disposed;

    #endregion

    #region Events

    /// <summary>Phát sau khi đồ đang mặc đổi và đã thử lưu.</summary>
    public event Action EquipmentChanged;

    #endregion

    #region Constructor / Lifecycle

    public Loadout(ISaveStore saves, EquipmentInventory inventory)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

    /// <summary>Đọc đồ đang mặc một lần, sau khi túi đồ đã đọc xong. Ô trỏ tới món không còn hoặc sai loại ô thì gỡ và ghi lại.</summary>
    public void Load()
    {
        if (!saves.IsLoaded || loaded) return;
        EquipmentSlotsSaveData data = saves.Load(GameConstants.SaveKeys.EquipmentSlots, new EquipmentSlotsSaveData(), EquipmentSlotsSaveData.Validate);
        loaded = true;
        bool changed = false;
        foreach (EquipmentSlotsSaveData.Entry entry in data.slots)
        {
            if (inventory.TryGet(entry.uid, out EquipmentItem item) && item.Definition.Slot == entry.slot)
            {
                equipped.Add(entry.slot, item);
                continue;
            }
            Debug.LogWarning($"Ô trang bị {entry.slot} trỏ tới uid {entry.uid} không còn trong túi hoặc sai loại ô; đã gỡ.");
            changed = true;
        }
        if (changed)
            Save();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        EquipmentChanged = null;
        equipped.Clear();
    }

    #endregion

    #region Read

    /// <summary>Đồ đang mặc ở ô; null nếu ô trống.</summary>
    public EquipmentItem GetEquipped(EquipmentSlot slot)
    {
        EnsureAvailable();
        return equipped.TryGetValue(slot, out EquipmentItem item) ? item : null;
    }

    /// <summary>Số món của set đang mặc.</summary>
    public int CountEquipped(EquipmentSet set)
    {
        EnsureAvailable();
        int count = 0;
        foreach (EquipmentItem item in equipped.Values)
        {
            if (set != EquipmentSet.None && item.Definition.Set == set)
                count++;
        }
        return count;
    }

    /// <summary>Ghi đồ đang mặc theo thứ tự ô vào <paramref name="result"/> (list được xóa trước).</summary>
    public void GetEquippedItems(List<EquipmentItem> result)
    {
        EnsureAvailable();
        if (result == null) throw new ArgumentNullException(nameof(result));
        result.Clear();
        foreach (EquipmentSlot slot in Slots)
        {
            if (equipped.TryGetValue(slot, out EquipmentItem item))
                result.Add(item);
        }
    }

    /// <summary>Ghi các set đang bật vào <paramref name="result"/> (list được xóa trước), theo thứ tự món đầu tiên của set theo ô.</summary>
    public void GetActiveSets(List<EquipmentSetData> result)
    {
        EnsureAvailable();
        if (result == null) throw new ArgumentNullException(nameof(result));
        result.Clear();
        setCounts.Clear();
        foreach (EquipmentItem item in equipped.Values)
        {
            if (item.SetData == null) continue;
            setCounts.TryGetValue(item.SetData.Set, out int count);
            setCounts[item.SetData.Set] = count + 1;
        }
        foreach (EquipmentSlot slot in Slots)
        {
            if (!equipped.TryGetValue(slot, out EquipmentItem item) || item.SetData == null) continue;
            EquipmentSetData set = item.SetData;
            if (set.IsActive(setCounts[set.Set]) && !result.Contains(set))
                result.Add(set);
        }
    }

    /// <summary>Tổng chỉ số của mọi món đang mặc (chỉ số chính + dòng phụ).</summary>
    public StatBonus TotalBonus
    {
        get
        {
            EnsureAvailable();
            StatBonus total = default;
            foreach (EquipmentItem item in equipped.Values)
                total += item.GetBonus();
            return total;
        }
    }

    #endregion

    #region Change

    /// <summary>
    /// Mặc một bản đồ vào ô của nó; ô đang có món khác thì món đó về túi (đổi chỗ).
    /// Trả về có đổi hay không; <paramref name="saved"/> là kết quả ghi file.
    /// </summary>
    public bool Equip(EquipmentItem item, out bool saved)
    {
        EnsureCanChange();
        if (!inventory.Owns(item))
            throw new ArgumentException("Equipment item is not in the inventory.", nameof(item));
        saved = true;
        EquipmentSlot slot = item.Definition.Slot;
        if (equipped.TryGetValue(slot, out EquipmentItem current) && current == item)
            return false;
        equipped[slot] = item;
        saved = SaveAndNotify();
        return true;
    }

    /// <summary>Tháo đồ ở ô về túi. Trả về có đổi hay không (ô trống thì false).</summary>
    public bool Unequip(EquipmentSlot slot, out bool saved)
    {
        EnsureCanChange();
        saved = true;
        if (!equipped.Remove(slot))
            return false;
        saved = SaveAndNotify();
        return true;
    }

    /// <summary>Tháo mọi món trong một lần ghi file. Trả về có đổi hay không.</summary>
    public bool UnequipAll(out bool saved)
    {
        EnsureCanChange();
        saved = true;
        if (equipped.Count == 0)
            return false;
        equipped.Clear();
        saved = SaveAndNotify();
        return true;
    }

    /// <summary>Thử lưu lại snapshot đồ đang mặc mà không đổi gì thêm.</summary>
    public bool Save()
    {
        EnsureAvailable();
        var data = new EquipmentSlotsSaveData();
        foreach (EquipmentSlot slot in Slots)
        {
            if (equipped.TryGetValue(slot, out EquipmentItem item))
                data.slots.Add(new EquipmentSlotsSaveData.Entry { slot = slot, uid = item.Uid });
        }
        EquipmentSlotsSaveData.Validate(data);
        return saves.Save(GameConstants.SaveKeys.EquipmentSlots, data);
    }

    #endregion

    #region Private Helpers

    private bool SaveAndNotify()
    {
        changing = true;
        try
        {
            bool saved = Save();
            if (EquipmentChanged != null)
            {
                foreach (Action listener in EquipmentChanged.GetInvocationList())
                {
                    try { listener(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
            return saved;
        }
        finally { changing = false; }
    }

    private void EnsureCanChange()
    {
        EnsureAvailable();
        if (changing)
            throw new InvalidOperationException("Cannot change equipment from an equipment notification callback.");
    }

    private void EnsureAvailable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(Loadout));
        if (!loaded || !saves.IsLoaded) throw new InvalidOperationException("Equipment slots have not loaded.");
    }

    #endregion
}
