using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Progression: sở hữu dữ liệu còn lại giữa các lượt chơi — ví tiền, level vĩnh viễn, trang bị
/// và mốc vượt màn. Đây là class <b>duy nhất</b> của Progression được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// ProgressionManager
/// ├── Wallet           ví vàng/kim cương (key currencies)
/// ├── CharacterLevels  level, thanh EXP nâng cấp và số sao theo nhân vật (key character_levels); nhận Wallet để trừ tiền cùng lần
/// ├── EquipmentInventory  túi đồ: các bản trang bị, nhận đồ, nâng cấp (key equipment_items); nhận Wallet để trừ tiền cùng lần
/// ├── EquipmentLoadout đồ đang mặc theo ô, set đang bật, tổng bonus trang bị (key equipment_slots)
/// ├── StageProgress    mốc thắng lần đầu (key stage_progress); nhận Wallet để ghi thưởng cùng lần
/// ├── LevelTrack       nâng level vĩnh viễn (dùng CharacterLevels) cho ICharacterUpgrades
/// ├── CharacterCollection  sở hữu, bản trùng, ghép/phân rã bằng mảnh theo độ hiếm (dữ liệu nằm trong CharacterLevels)
/// └── StarTrack        nâng sao bằng mảnh + bản trùng (qua CharacterCollection)
/// </code>
/// Thay cho CurrencyService, CharacterLevelService, LevelManager, PlayerEquipmentService và StageProgressService.
/// Chiều phụ thuộc: Progression → Save. Miền khác chỉ thấy <c>ProgressionContracts.cs</c>.
/// </remarks>
public sealed class ProgressionManager : DomainManager, IWallet, IPermanentBonuses, ICharacterProgress, IStageProgress, ICharacterUpgrades,
    ICharacterCollection, IEquipment, IEquipmentGrant, IInitializable, IDisposable
{
    #region Fields

    private readonly Wallet wallet;
    private readonly CharacterLevels levels;
    private readonly CharacterCollection collection;
    private readonly EquipmentInventory equipment;
    private readonly EquipmentLoadout loadout;
    private readonly StageProgress stages;
    private readonly LevelTrack levelTrack;
    private readonly StarTrack starTrack;
    private bool disposed;

    #endregion

    #region Events

    // Event chuyển tiếp: đăng ký vào event của Manager thực chất là đăng ký thẳng vào system con
    // (add/remove), Manager không giữ danh sách listener riêng.
    /// <inheritdoc/>
    public event Action<CurrencyType, long> BalanceChanged
    {
        add => wallet.BalanceChanged += value;
        remove => wallet.BalanceChanged -= value;
    }

    /// <inheritdoc/>
    public event Action BonusesChanged;

    /// <inheritdoc/>
    public event Action<CharacterType, CharacterUpgradeKind> Upgraded;

    /// <inheritdoc/>
    public event Action Changed;

    // Tên trùng với IEquipment.Changed nên khai báo tường minh: hai event khác nhau, không lẫn listener.
    /// <inheritdoc/>
    event Action ICharacterCollection.Changed
    {
        add => collection.Changed += value;
        remove => collection.Changed -= value;
    }

    #endregion

    #region Constructor / Lifecycle

    /// <summary>Dựng các system con; chưa đọc save (SaveManager.Initialize chưa chạy lúc container dựng object).</summary>
    public ProgressionManager(
        ISaveStore saves,
        CharacterDatabaseSO characterDatabase,
        LevelDatabaseSO levelDatabase,
        EquipmentDatabaseSO equipmentDatabase)
    {
        if (saves == null) throw new ArgumentNullException(nameof(saves));
        wallet = new Wallet(saves);
        levels = new CharacterLevels(saves, wallet, characterDatabase, levelDatabase);
        equipment = new EquipmentInventory(saves, wallet, equipmentDatabase, new System.Random());
        loadout = new EquipmentLoadout(saves, equipment);
        stages = new StageProgress(saves, wallet);
        collection = new CharacterCollection(levels, levelDatabase);
        levelTrack = new LevelTrack(levels, wallet);
        starTrack = new StarTrack(levels, collection);

        // Gộp các nguồn đổi bonus thành một event: PlayerSystem chỉ cần biết "tính lại".
        // Túi đồ đổi (lên cấp một món đang mặc) cũng có thể đổi bonus, nên cũng báo tính lại.
        levels.LevelChanged += OnLevelChanged;
        equipment.Changed += OnEquipmentChanged;
        loadout.EquipmentChanged += OnEquipmentChanged;
    }

    /// <summary>Đọc mọi section sau SaveManager (-200) và trước CharacterManager (-150), xem ExecutionOrder.</summary>
    public override void Initialize()
    {
        if (disposed) return;
        wallet.Load();
        levels.Load();
        equipment.Load();
        loadout.Load(); // Sau túi đồ: ô chỉ lưu UID của bản đồ.
        stages.Load();
    }

    /// <summary>Gỡ listener rồi dọn từng system; lỗi một system không chặn các system còn lại.</summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        levels.LevelChanged -= OnLevelChanged;
        equipment.Changed -= OnEquipmentChanged;
        loadout.EquipmentChanged -= OnEquipmentChanged;
        BonusesChanged = null;
        Upgraded = null;
        Changed = null;
        DisposeSafely(loadout);
        DisposeSafely(equipment);
        DisposeSafely(levels);
        wallet.Dispose();
    }

    #endregion

    #region IWallet

    /// <inheritdoc/>
    public long GetBalance(CurrencyType type) => wallet.GetBalance(type);

    /// <inheritdoc/>
    public bool Add(CurrencyType type, long amount) => wallet.Add(type, amount);

    /// <inheritdoc/>
    public bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts) => wallet.AddBatch(amounts);

    /// <inheritdoc/>
    public bool TrySpend(CurrencyType type, long amount, out bool saved) => wallet.TrySpend(type, amount, out saved);

    #endregion

    #region IPermanentBonuses

    /// <inheritdoc/>
    public StatBonus GetBonus(CharacterType type) => levels.GetTotalBonus(type) + loadout.TotalBonus;

    #endregion

    #region ICharacterProgress

    /// <inheritdoc/>
    public int GetLevel(CharacterType type) => levels.GetLevel(type);

    /// <inheritdoc/>
    public int GetStars(CharacterType type) => levels.GetStars(type);

    /// <inheritdoc/>
    public void GetEquippedItems(List<EquipmentItem> result) => loadout.GetEquippedItems(result);

    /// <inheritdoc/>
    public void GetActiveSets(List<EquipmentSetData> result) => loadout.GetActiveSets(result);

    #endregion

    #region IStageProgress

    /// <inheritdoc/>
    public bool HasFirstCleared(string stageId) => stages.HasFirstCleared(stageId);

    /// <inheritdoc/>
    public bool GrantVictoryRewards(string stageId, IReadOnlyDictionary<CurrencyType, long> rewards)
        => stages.GrantVictoryRewards(stageId, rewards);

    #endregion

    #region ICharacterUpgrades

    /// <inheritdoc/>
    public UpgradeQuote GetQuote(CharacterType type, CharacterUpgradeKind kind) => Track(kind).GetQuote(type);

    /// <inheritdoc/>
    public bool TryUpgrade(CharacterType type, CharacterUpgradeKind kind, out bool saved)
    {
        saved = false;
        if (disposed || !Track(kind).TryUpgrade(type, out saved)) return false;
        // Sao có thể cộng chỉ số dự phòng (sao không có bước skill) nên báo Player tính lại bonus.
        // Level chỉ đổi bonus khi thật sự lên cấp, đã báo qua CharacterLevels.LevelChanged.
        if (kind == CharacterUpgradeKind.Star)
            OnBonusSourceChanged();
        Upgraded?.Invoke(type, kind);
        return true;
    }

    // Một switch duy nhất chọn track theo loại nâng cấp.
    private IUpgradeTrack Track(CharacterUpgradeKind kind)
    {
        switch (kind)
        {
            case CharacterUpgradeKind.Level: return levelTrack;
            case CharacterUpgradeKind.Star: return starTrack;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    #endregion

    #region ICharacterCollection

    /// <inheritdoc/>
    public CharacterCollectionStatus GetStatus(CharacterType type) => collection.GetStatus(type);

    /// <inheritdoc/>
    public CharacterGrantResult Grant(CharacterType type, out CharacterShards gained, out bool saved) => collection.Grant(type, out gained, out saved);

    /// <inheritdoc/>
    public bool TryAssemble(CharacterType type, out bool saved) => collection.TryAssemble(type, out saved);

    #endregion

    #region IEquipment

    /// <inheritdoc/>
    public IReadOnlyList<EquipmentItem> Items => equipment.Items;

    /// <inheritdoc/>
    public EquipmentItem GetEquipped(EquipmentSlot slot) => loadout.GetEquipped(slot);

    /// <inheritdoc/>
    public int CountEquipped(EquipmentSet set) => loadout.CountEquipped(set);

    /// <inheritdoc/>
    public bool Equip(EquipmentItem item, out bool saved) => loadout.Equip(item, out saved);

    /// <inheritdoc/>
    public bool Unequip(EquipmentSlot slot, out bool saved) => loadout.Unequip(slot, out saved);

    /// <inheritdoc/>
    public bool UnequipAll(out bool saved) => loadout.UnequipAll(out saved);

    /// <inheritdoc/>
    public UpgradeQuote GetQuote(EquipmentItem item) => equipment.GetQuote(item);

    /// <inheritdoc/>
    public bool TryUpgrade(EquipmentItem item, out bool saved) => equipment.TryLevelUp(item, out saved);

    #endregion

    #region IEquipmentGrant

    /// <inheritdoc/>
    public EquipmentItem Grant(EquipmentDataSO item, EquipmentRarity rarity, out bool saved) => equipment.Grant(item, rarity, out saved);

    #endregion

    #region Private Helpers

    private void OnLevelChanged(CharacterType type, int previousLevel, int newLevel) => OnBonusSourceChanged();

    private void OnBonusSourceChanged()
    {
        if (!disposed) BonusesChanged?.Invoke();
    }

    // Màn Equipment vẽ lại trước, rồi Player tính lại bonus (thứ tự không quan trọng, cả hai chỉ đọc).
    private void OnEquipmentChanged()
    {
        if (disposed) return;
        Changed?.Invoke();
        OnBonusSourceChanged();
    }

    // System có thể đã lỗi giữa chừng; vẫn dọn những system còn lại.
    private static void DisposeSafely(IDisposable system)
    {
        if (system == null) return;
        try { system.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    #endregion
}
