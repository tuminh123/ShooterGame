using System;
using System.Collections.Generic;

#region CurrencyReader

/// <summary>Đọc số dư và theo dõi thay đổi ví hoặc lỗi lưu.</summary>
public interface ICurrencyReader
{
    event Action<CurrencyType, long> BalanceChanged;
    event Action<CurrencyType, Exception> SaveFailed;
    long GetBalance(CurrencyType type);
}

#endregion

#region CurrencyWallet

/// <summary>Cộng hoặc chi tiền; trạng thái RAM và kết quả lưu được phân biệt.</summary>
public interface ICurrencyWallet
{
    bool Add(CurrencyType type, long amount);
    bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts);
    bool TrySpend(CurrencyType type, long amount, out bool saved);
}

#endregion

#region Stage Reward Transaction

/// <summary>Ghi thưởng và mốc vượt màn trong cùng snapshot lưu.</summary>
public interface IStageRewardTransaction
{
    bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts, StageProgressSaveData stageProgress);
}

#endregion

#region CurrencyPersistence

/// <summary>Thử lưu lại snapshot ví mà không cộng hoặc trừ thêm.</summary>
public interface ICurrencyPersistence
{
    bool Save();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của CurrencyService; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ICurrencyService
{
    ICurrencyReader GetReader();
    ICurrencyWallet GetWallet();
    ICurrencyPersistence GetPersistence();
    IStageRewardTransaction GetStageRewardTransaction();
}

#endregion
