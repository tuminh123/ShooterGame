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
/// ├── CharacterLevels  level vĩnh viễn theo nhân vật (key character_levels)
/// ├── Loadout          trang bị đang mặc (key equipment)
/// └── StageProgress    mốc thắng lần đầu (key stage_progress); nhận Wallet để ghi thưởng cùng lần
/// </code>
/// Thay cho CurrencyService, CharacterLevelService, LevelManager, PlayerEquipmentService và StageProgressService.
/// Chiều phụ thuộc: Progression → Save. Miền khác chỉ thấy <c>ProgressionContracts.cs</c>.
/// </remarks>
public sealed class ProgressionManager : IWallet, IPermanentBonuses, IStageProgress, IInitializable, IDisposable
{
    #region Fields

    private readonly Wallet wallet;
    private readonly CharacterLevels levels;
    private readonly Loadout loadout;
    private readonly StageProgress stages;
    private bool disposed;

    #endregion

    #region Events

    /// <inheritdoc/>
    public event Action<CurrencyType, long> BalanceChanged
    {
        add => wallet.BalanceChanged += value;
        remove => wallet.BalanceChanged -= value;
    }

    /// <inheritdoc/>
    public event Action BonusesChanged;

    #endregion

    #region Constructor / Lifecycle

    /// <summary>Dựng bốn system; chưa đọc save (SaveManager.Initialize chưa chạy lúc container dựng object).</summary>
    public ProgressionManager(
        ISaveStore saves,
        CharacterDatabaseSO characterDatabase,
        LevelDatabaseSO levelDatabase,
        EquipmentDatabaseSO equipmentDatabase)
    {
        if (saves == null) throw new ArgumentNullException(nameof(saves));
        wallet = new Wallet(saves);
        levels = new CharacterLevels(saves, characterDatabase, levelDatabase);
        loadout = new Loadout(saves, equipmentDatabase);
        stages = new StageProgress(saves, wallet);

        // Gộp hai nguồn đổi bonus thành một event: PlayerSystem chỉ cần biết "tính lại".
        levels.LevelChanged += OnLevelChanged;
        loadout.EquipmentChanged += OnBonusSourceChanged;
    }

    /// <summary>Đọc mọi section sau SaveManager (-200) và trước CharacterManager (-150), xem ExecutionOrder.</summary>
    public void Initialize()
    {
        if (disposed) return;
        wallet.Load();
        levels.Load();
        loadout.Load();
        stages.Load();
    }

    /// <summary>Gỡ listener rồi dọn từng system; lỗi một system không chặn các system còn lại.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        levels.LevelChanged -= OnLevelChanged;
        loadout.EquipmentChanged -= OnBonusSourceChanged;
        BonusesChanged = null;
        DisposeSafely(loadout);
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

    #region IStageProgress

    /// <inheritdoc/>
    public bool HasFirstCleared(string stageId) => stages.HasFirstCleared(stageId);

    /// <inheritdoc/>
    public bool GrantVictoryRewards(string stageId, IReadOnlyDictionary<CurrencyType, long> rewards)
        => stages.GrantVictoryRewards(stageId, rewards);

    #endregion

    #region Private Helpers

    private void OnLevelChanged(CharacterType type, int previousLevel, int newLevel) => OnBonusSourceChanged();

    private void OnBonusSourceChanged()
    {
        if (!disposed) BonusesChanged?.Invoke();
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
