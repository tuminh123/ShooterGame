using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Sở hữu số dư vĩnh viễn và lưu snapshot vào key currencies trong save người chơi.
public sealed class CurrencyService :
    IInitializable,
    IDisposable,
    ICurrencyService,
    ICurrencyReader,
    ICurrencyWallet,
    ICurrencyPersistence,
    IStageRewardTransaction
{

    #region Dependencies And State
    private readonly ISaveReader saveReader;
    private readonly ISaveWriter saveWriter;
    private readonly ISaveFlush saveFlush;
    private CurrencySaveData data;
    private readonly Dictionary<CurrencyType, CurrencySaveData.Balance> balances = new Dictionary<CurrencyType, CurrencySaveData.Balance>();
    private bool changing;
    private bool disposed;

    public event Action<CurrencyType, long> BalanceChanged;
    public event Action<CurrencyType, Exception> SaveFailed;

    #endregion

    #region Initialization

    public CurrencyService(ISaveService saveService)
    {
        this.saveReader = saveService.GetSaveReader() ?? throw new ArgumentNullException(nameof(saveService));
        this.saveWriter = saveService.GetSaveWriter() ?? throw new ArgumentNullException(nameof(saveService));
        this.saveFlush = saveService.GetSaveFlush() ?? throw new ArgumentNullException(nameof(saveService));
    }

    public void Initialize()
    {
        if (!saveReader.IsLoaded || data != null) return;
        data = saveReader.Load(GameConstants.SaveKeys.Currencies, new CurrencySaveData(), ValidateData);
        foreach (CurrencySaveData.Balance entry in data.currencies) balances.Add(entry.type, entry);
    }

    #endregion

    #region Validation

    internal static void ValidateData(CurrencySaveData value)
    {
        if (value == null || value.currencies == null) throw new InvalidOperationException("Missing currency balances.");
        var unique = new HashSet<CurrencyType>();
        foreach (CurrencySaveData.Balance entry in value.currencies)
            if (entry == null || !Enum.IsDefined(typeof(CurrencyType), entry.type) || entry.balance < 0 || !unique.Add(entry.type))
                throw new InvalidOperationException("Currency types must be defined and unique with non-negative balances.");
    }

    #endregion

    #region Runtime Operations

    public long GetBalance(CurrencyType type)
    {
        EnsureAvailable();
        if (!Enum.IsDefined(typeof(CurrencyType), type)) throw new ArgumentOutOfRangeException(nameof(type));
        return balances.TryGetValue(type, out CurrencySaveData.Balance entry) ? entry.balance : 0L;
    }

    // false chỉ báo lỗi ghi file: số dư trong RAM đã tăng, dùng Save() để thử ghi lại, không Add lại.
    public bool Add(CurrencyType type, long amount)
    {
        EnsureCanChange(amount);
        long balance = checked(GetBalance(type) + amount);
        return Apply(type, balance);
    }

    // Cấp nhiều loại tiền: kiểm tra toàn bộ trước khi sửa RAM, ghi chung một snapshot ví.
    public bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts) => AddBatch(amounts, null);

    public bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts, StageProgressSaveData stageProgress)
    {
        EnsureCanChange(0);
        if (amounts == null) throw new ArgumentNullException(nameof(amounts));
        if (stageProgress != null) StageProgressService.ValidateData(stageProgress);
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
            {
                if (!balances.TryGetValue(target.Key, out CurrencySaveData.Balance entry))
                {
                    entry = new CurrencySaveData.Balance { type = target.Key };
                    balances.Add(target.Key, entry);
                    data.currencies.Add(entry);
                }
                entry.balance = target.Value;
            }
            bool saved = stageProgress != null
                ? saveWriter.SaveBatch(GameConstants.SaveKeys.Currencies, data.Clone(),
                    GameConstants.SaveKeys.StageProgress, stageProgress.Clone())
                : targets.Count > 0 ? Save() : saveFlush.SaveIfDirty();
            foreach (KeyValuePair<CurrencyType, long> target in targets)
            {
                NotifyBalance(target.Key, target.Value);
                if (!saved) NotifyFailure(target.Key, saveReader.LastError);
            }
            return saved;
        }
        finally { changing = false; }
    }

    // Kết quả là đã trừ được tiền hay chưa; saved phản ánh riêng kết quả ghi file khi trừ thành công.
    public bool TrySpend(CurrencyType type, long amount, out bool saved)
    {
        EnsureCanChange(amount);
        saved = false;
        long balance = GetBalance(type);
        if (balance < amount) return false;
        saved = Apply(type, balance - amount);
        return true;
    }

    private bool Apply(CurrencyType type, long balance)
    {
        changing = true;
        try
        {
            long previous = GetBalance(type);
            bool changed = previous != balance;
            if (changed)
            {
                if (!balances.TryGetValue(type, out CurrencySaveData.Balance entry))
                {
                    entry = new CurrencySaveData.Balance { type = type };
                    balances.Add(type, entry);
                    data.currencies.Add(entry);
                }
                entry.balance = balance;
            }
            bool saved = changed ? Save() : saveFlush.SaveIfDirty();
            if (changed) NotifyBalance(type, balance);
            if (!saved) NotifyFailure(type, saveReader.LastError);
            return saved;
        }
        finally { changing = false; }
    }

    #endregion

    #region Persistence

    public bool Save()
    {
        EnsureAvailable();
        ValidateData(data);
        return saveWriter.Save(GameConstants.SaveKeys.Currencies, data.Clone());
    }

    #endregion

    #region Runtime Operations

    // Lỗi trình bày UI không được làm caller tưởng thao tác cộng/trừ tiền chưa thực hiện.
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

    #endregion

    #region Validation

    private void EnsureCanChange(long amount)
    {
        EnsureAvailable();
        if (changing) throw new InvalidOperationException("Cannot change currency from a currency notification callback.");
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    private void EnsureAvailable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(CurrencyService));
        if (data == null || !saveReader.IsLoaded) throw new InvalidOperationException("Currency data has not loaded.");
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        disposed = true;
        BalanceChanged = null;
        SaveFailed = null;
        balances.Clear();
        data = null;
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public ICurrencyReader GetReader() => this;

    public ICurrencyWallet GetWallet() => this;

    public ICurrencyPersistence GetPersistence() => this;

    public IStageRewardTransaction GetStageRewardTransaction() => this;

    #endregion
}
