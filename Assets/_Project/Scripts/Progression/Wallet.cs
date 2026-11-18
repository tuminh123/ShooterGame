using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Số dư tiền vĩnh viễn (vàng, kim cương), lưu ở key <c>currencies</c> ngay khi thay đổi.
/// </summary>
/// <remarks>Do <see cref="ProgressionManager"/> tạo; thay cho CurrencyService cũ. Miền khác dùng qua <see cref="IWallet"/>.</remarks>
public sealed class Wallet
{
    #region Fields

    private readonly ISaveStore saves;
    private CurrencySaveData data;
    private readonly Dictionary<CurrencyType, CurrencySaveData.Balance> balances = new Dictionary<CurrencyType, CurrencySaveData.Balance>();
    // Chặn đổi tiền từ trong callback BalanceChanged, để thứ tự thông báo và snapshot luôn khớp nhau.
    private bool changing;
    private bool disposed;

    #endregion

    #region Events

    /// <summary>(loại, số dư mới).</summary>
    public event Action<CurrencyType, long> BalanceChanged;

    /// <summary>(loại, lỗi IO): số dư đã đổi trong RAM nhưng chưa ghi được file.</summary>
    public event Action<CurrencyType, Exception> SaveFailed;

    #endregion

    #region Constructor / Lifecycle

    public Wallet(ISaveStore saves)
    {
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
    }

    /// <summary>Đọc ví một lần sau khi Save đã mở file.</summary>
    public void Load()
    {
        if (!saves.IsLoaded || data != null) return;
        data = saves.Load(GameConstants.SaveKeys.Currencies, new CurrencySaveData(), CurrencySaveData.Validate);
        foreach (CurrencySaveData.Balance entry in data.currencies) balances.Add(entry.type, entry);
    }

    public void Dispose()
    {
        disposed = true;
        BalanceChanged = null;
        SaveFailed = null;
        balances.Clear();
        data = null;
    }

    #endregion

    #region Read

    /// <summary>Số dư hiện tại; loại chưa có bản ghi là 0.</summary>
    public long GetBalance(CurrencyType type)
    {
        EnsureAvailable();
        if (!Enum.IsDefined(typeof(CurrencyType), type)) throw new ArgumentOutOfRangeException(nameof(type));
        return balances.TryGetValue(type, out CurrencySaveData.Balance entry) ? entry.balance : 0L;
    }

    #endregion

    #region Change

    /// <summary>false chỉ báo lỗi ghi file: số dư trong RAM đã tăng, không gọi Add lại.</summary>
    public bool Add(CurrencyType type, long amount)
    {
        EnsureCanChange(amount);
        long balance = checked(GetBalance(type) + amount);
        return Apply(type, balance);
    }

    /// <summary>Cộng nhiều loại tiền: kiểm tra toàn bộ trước khi sửa RAM, ghi chung một snapshot ví.</summary>
    public bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts) => AddBatch(amounts, null);

    /// <summary>
    /// Như trên, kèm snapshot mốc vượt màn ghi cùng lần với ví (dùng bởi <see cref="StageProgress"/>),
    /// để thưởng thắng và cờ "đã thắng" không bao giờ lệch nhau khi IO lỗi.
    /// </summary>
    internal bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts, StageProgressSaveData stageProgress)
    {
        EnsureCanChange(0);
        if (amounts == null) throw new ArgumentNullException(nameof(amounts));
        if (stageProgress != null) StageProgressSaveData.Validate(stageProgress);
        var targets = new Dictionary<CurrencyType, long>();
        foreach (KeyValuePair<CurrencyType, long> reward in amounts)
        {
            if (reward.Value < 0) throw new ArgumentOutOfRangeException(nameof(amounts));
            long previous = GetBalance(reward.Key);
            if (reward.Value > 0) targets.Add(reward.Key, checked(previous + reward.Value));
        }
        changing = true;
        try
        {
            foreach (KeyValuePair<CurrencyType, long> target in targets)
                GetOrCreateEntry(target.Key).balance = target.Value;
            bool saved = stageProgress != null
                ? saves.SaveBatch(GameConstants.SaveKeys.Currencies, data.Clone(),
                    GameConstants.SaveKeys.StageProgress, stageProgress.Clone())
                : targets.Count > 0 ? Save() : true;
            foreach (KeyValuePair<CurrencyType, long> target in targets)
            {
                NotifyBalance(target.Key, target.Value);
                if (!saved) NotifyFailure(target.Key, saves.LastError);
            }
            return saved;
        }
        finally { changing = false; }
    }

    /// <summary>Trả về đã trừ được tiền hay chưa; <paramref name="saved"/> là kết quả ghi file khi trừ thành công.</summary>
    public bool TrySpend(CurrencyType type, long amount, out bool saved)
    {
        EnsureCanChange(amount);
        saved = false;
        long balance = GetBalance(type);
        if (balance < amount) return false;
        saved = Apply(type, balance - amount);
        return true;
    }

    /// <summary>Thử lưu lại snapshot ví mà không cộng/trừ thêm.</summary>
    public bool Save()
    {
        EnsureAvailable();
        CurrencySaveData.Validate(data);
        return saves.Save(GameConstants.SaveKeys.Currencies, data.Clone());
    }

    #endregion

    #region Private Helpers

    private bool Apply(CurrencyType type, long balance)
    {
        changing = true;
        try
        {
            bool changed = GetBalance(type) != balance;
            if (changed) GetOrCreateEntry(type).balance = balance;
            // Không đổi thì không ghi; trước đây gọi SaveIfDirty, nay việc thử ghi lại do SaveManager lo.
            bool saved = !changed || Save();
            if (changed) NotifyBalance(type, balance);
            if (!saved) NotifyFailure(type, saves.LastError);
            return saved;
        }
        finally { changing = false; }
    }

    private CurrencySaveData.Balance GetOrCreateEntry(CurrencyType type)
    {
        if (balances.TryGetValue(type, out CurrencySaveData.Balance entry)) return entry;
        entry = new CurrencySaveData.Balance { type = type };
        balances.Add(type, entry);
        data.currencies.Add(entry);
        return entry;
    }

    // Lỗi trình bày UI không được làm bên gọi tưởng thao tác cộng/trừ tiền chưa thực hiện.
    private void NotifyBalance(CurrencyType type, long balance)
    {
        if (BalanceChanged == null) return;
        foreach (Action<CurrencyType, long> listener in BalanceChanged.GetInvocationList())
            try { listener(type, balance); }
            catch (Exception exception) { Debug.LogException(exception); }
    }

    private void NotifyFailure(CurrencyType type, Exception error)
    {
        if (SaveFailed == null) return;
        foreach (Action<CurrencyType, Exception> listener in SaveFailed.GetInvocationList())
            try { listener(type, error); }
            catch (Exception exception) { Debug.LogException(exception); }
    }

    private void EnsureCanChange(long amount)
    {
        EnsureAvailable();
        if (changing) throw new InvalidOperationException("Cannot change currency from a currency notification callback.");
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    private void EnsureAvailable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(Wallet));
        if (data == null || !saves.IsLoaded) throw new InvalidOperationException("Currency data has not loaded.");
    }

    #endregion
}
