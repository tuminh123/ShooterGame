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

    /// <summary>Đủ tiền cho mọi mục của <paramref name="costs"/> chưa (nhiều mục cùng loại tiền được cộng gộp).</summary>
    /// <remarks>Dùng trong GetQuote để trả <see cref="UpgradeBlock.NotEnoughCurrency"/>; chỉ đọc nên gọi được từ callback.</remarks>
    public bool CanAfford(IReadOnlyList<CurrencyCost> costs) => TryGetSpendTargets(costs, out _);

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
    public bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts)
    {
        Dictionary<CurrencyType, long> targets = PlanAdd(amounts);
        SetBalances(targets);
        bool saved = targets.Count == 0 || Save();
        PublishBalances(targets, saved);
        return saved;
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

    /// <summary>
    /// Trừ <paramref name="costs"/> và ghi ví cùng snapshot <paramref name="otherSnapshot"/> của key <paramref name="otherKey"/>
    /// trong <b>một lần lưu</b>, để tiền đã trừ và tiến trình đã nâng (level, trang bị) không bao giờ lệch nhau khi IO lỗi.
    /// Trả về kết quả ghi file; false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    /// <remarks>
    /// Bên gọi kiểm tra <see cref="CanAfford"/> trước khi đổi tiến trình trong RAM, rồi gọi hàm này với snapshot mới.
    /// Game chạy một luồng nên giữa hai bước không có gì chen vào; vẫn kiểm tra lại và ném lỗi nếu bị gọi sai thứ tự.
    /// </remarks>
    internal bool SpendWith<T>(IReadOnlyList<CurrencyCost> costs, string otherKey, T otherSnapshot) where T : class
    {
        if (!TryGetSpendTargets(costs, out Dictionary<CurrencyType, long> targets))
            throw new InvalidOperationException("Not enough currency; check CanAfford before changing progress.");
        return WriteWith(targets, otherKey, otherSnapshot);
    }

    /// <summary>
    /// Như <see cref="SpendWith{T}"/> nhưng mỗi loại tiền đổi một lượng <b>có dấu</b> (âm = trừ, dương = cộng), ghi cùng một lần
    /// với snapshot tiến trình. Dùng khi một thao tác vừa trừ vừa cộng: ghép nhân vật (trừ mảnh), phân rã bản trùng (cộng mảnh),
    /// nâng sao bằng bản trùng (giá trị bản trùng dư thì cộng lại mảnh).
    /// </summary>
    /// <remarks>Số dư sau khi đổi phải không âm; bên gọi kiểm tra trước khi đổi tiến trình trong RAM, sai thì ném lỗi.</remarks>
    internal bool ApplyWith<T>(IReadOnlyDictionary<CurrencyType, long> deltas, string otherKey, T otherSnapshot) where T : class
    {
        EnsureAvailable();
        if (deltas == null) throw new ArgumentNullException(nameof(deltas));
        var targets = new Dictionary<CurrencyType, long>(deltas.Count);
        foreach (KeyValuePair<CurrencyType, long> delta in deltas)
        {
            if (delta.Value == 0) continue;
            long balance = checked(GetBalance(delta.Key) + delta.Value);
            if (balance < 0)
                throw new InvalidOperationException($"Not enough {delta.Key}; check the balance before changing progress.");
            targets[delta.Key] = balance;
        }
        return WriteWith(targets, otherKey, otherSnapshot);
    }

    // Ghi số dư mới (đã kiểm tra) và snapshot tiến trình trong một lần lưu, rồi báo số dư đổi.
    private bool WriteWith<T>(Dictionary<CurrencyType, long> targets, string otherKey, T otherSnapshot) where T : class
    {
        if (otherSnapshot == null) throw new ArgumentNullException(nameof(otherSnapshot));
        SetBalances(targets);
        // Giá 0 thì ví không đổi: chỉ cần ghi tiến trình.
        bool saved = targets.Count > 0
            ? saves.SaveBatch(new[]
            {
                SaveEntry.Of(GameConstants.SaveKeys.Currencies, CreateSnapshot()),
                SaveEntry.Of(otherKey, otherSnapshot)
            })
            : saves.Save(otherKey, otherSnapshot);
        PublishBalances(targets, saved);
        return saved;
    }

    /// <summary>Thử lưu lại snapshot ví mà không cộng/trừ thêm.</summary>
    public bool Save() => saves.Save(GameConstants.SaveKeys.Currencies, CreateSnapshot());

    #endregion

    #region Change Steps (RewardGranter ghép với hệ thống khác trong một lần lưu)

    /// <summary>Tính số dư mới sau khi cộng, không đổi gì; ném lỗi nếu số âm, tràn số hoặc đang trong callback của ví.</summary>
    internal Dictionary<CurrencyType, long> PlanAdd(IReadOnlyDictionary<CurrencyType, long> amounts)
        => PlanPurchase(Array.Empty<CurrencyCost>(), amounts);

    /// <summary>
    /// Tính số dư mới sau khi trừ <paramref name="costs"/> rồi cộng <paramref name="gains"/> (mua gói: trả giá, nhận tiền trong gói),
    /// không đổi gì. Thiếu tiền thì ném lỗi: bên gọi kiểm tra <see cref="CanAfford"/> trước.
    /// </summary>
    internal Dictionary<CurrencyType, long> PlanPurchase(IReadOnlyList<CurrencyCost> costs, IReadOnlyDictionary<CurrencyType, long> gains)
    {
        EnsureCanChange(0);
        if (gains == null) throw new ArgumentNullException(nameof(gains));
        if (!TryGetSpendTargets(costs, out Dictionary<CurrencyType, long> targets))
            throw new InvalidOperationException("Not enough currency; check CanAfford before purchasing.");
        foreach (KeyValuePair<CurrencyType, long> gain in gains)
        {
            if (gain.Value < 0) throw new ArgumentOutOfRangeException(nameof(gains));
            if (gain.Value == 0) continue;
            long current = targets.TryGetValue(gain.Key, out long afterCost) ? afterCost : GetBalance(gain.Key);
            targets[gain.Key] = checked(current + gain.Value);
        }
        return targets;
    }

    /// <summary>Đổi số dư trong RAM (đã kiểm tra); chưa ghi và chưa báo event.</summary>
    internal void SetBalances(Dictionary<CurrencyType, long> targets)
    {
        EnsureCanChange(0);
        foreach (KeyValuePair<CurrencyType, long> target in targets)
            GetOrCreateEntry(target.Key).balance = target.Value;
    }

    /// <summary>Bản sao ví đã kiểm tra để ghi.</summary>
    internal CurrencySaveData CreateSnapshot()
    {
        EnsureAvailable();
        CurrencySaveData.Validate(data);
        return data.Clone();
    }

    /// <summary>Báo số dư đổi (và lỗi ghi nếu có) sau khi đã thử lưu; trong lúc báo thì ví không được đổi tiếp.</summary>
    internal void PublishBalances(Dictionary<CurrencyType, long> targets, bool saved)
    {
        changing = true;
        try
        {
            foreach (KeyValuePair<CurrencyType, long> target in targets)
            {
                NotifyBalance(target.Key, target.Value);
                if (!saved) NotifyFailure(target.Key, saves.LastError);
            }
        }
        finally { changing = false; }
    }

    #endregion

    #region Private Helpers

    // Số dư mới của từng loại tiền sau khi trừ (bỏ qua mục giá 0); false nếu có loại không đủ.
    // Gộp các mục cùng loại để giá hai dòng cùng Gold không lọt kiểm tra từng dòng riêng.
    private bool TryGetSpendTargets(IReadOnlyList<CurrencyCost> costs, out Dictionary<CurrencyType, long> targets)
    {
        EnsureAvailable();
        if (costs == null) throw new ArgumentNullException(nameof(costs));
        targets = new Dictionary<CurrencyType, long>(costs.Count);
        foreach (CurrencyCost cost in costs)
        {
            if (cost.Amount < 0) throw new ArgumentOutOfRangeException(nameof(costs), "Cost amount must not be negative.");
            if (cost.Amount == 0) continue;
            long current = targets.TryGetValue(cost.Currency, out long pending) ? pending : GetBalance(cost.Currency);
            if (current < cost.Amount) return false;
            targets[cost.Currency] = current - cost.Amount;
        }
        return true;
    }

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
