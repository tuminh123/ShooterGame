using System;
using System.Collections.Generic;

// Ranh giới của miền Progression: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do ProgressionManager implement. Wallet, CharacterLevels, Loadout và StageProgress
// nằm bên trong miền, không được bind vào Zenject.
// Progression giữ dữ liệu còn lại giữa các lượt chơi (ví, level vĩnh viễn, trang bị, mốc vượt màn) và chỉ phụ thuộc Save.
// Mỗi vai trò ứng với một nhóm nơi dùng thực tế. Nâng level/sao đi qua ICharacterUpgrades (màn Character);
// API chưa có ai dùng (đổi trang bị) vẫn nằm trên system bên trong và sẽ có vai trò riêng khi UI tương ứng ra đời.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region Wallet (Home UI, Run, Stage)

/// <summary>
/// Ví tiền vĩnh viễn. Dùng bởi Home UI (đọc số dư), RunManager (vàng nhặt trong trận) và StageRewards (thưởng thua).
/// Mọi thay đổi được lưu ngay; false nghĩa là IO lỗi, RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
/// </summary>
public interface IWallet
{
    /// <summary>Số dư hiện tại; loại chưa có bản ghi là 0.</summary>
    long GetBalance(CurrencyType type);

    /// <summary>(loại tiền, số dư mới), phát sau khi RAM đã đổi và đã thử lưu.</summary>
    event Action<CurrencyType, long> BalanceChanged;

    /// <summary>Cộng tiền rồi lưu.</summary>
    bool Add(CurrencyType type, long amount);

    /// <summary>Cộng nhiều loại cùng lúc: kiểm tra hết trước khi sửa RAM, ghi chung một snapshot.</summary>
    bool AddBatch(IReadOnlyDictionary<CurrencyType, long> amounts);

    /// <summary>Trừ tiền nếu đủ. Trả về đã trừ được hay chưa; <paramref name="saved"/> là kết quả ghi file.</summary>
    bool TrySpend(CurrencyType type, long amount, out bool saved);
}

#endregion

#region Permanent Bonuses (PlayerSystem)

/// <summary>
/// Tổng bonus chỉ số vĩnh viễn của một nhân vật Player: level vĩnh viễn + trang bị. Dùng bởi PlayerSystem.
/// </summary>
/// <remarks>PlayerSystem chỉ cần một con số tổng, nên vai trò này gộp sẵn thay vì lộ hai system riêng.</remarks>
public interface IPermanentBonuses
{
    /// <summary>Bonus cộng thẳng của <paramref name="type"/>; type không có cấu hình level thì ném lỗi.</summary>
    StatBonus GetBonus(CharacterType type);

    /// <summary>Level vĩnh viễn của một nhân vật hoặc loadout vừa đổi: Player cần tính lại nguồn bonus.</summary>
    event Action BonusesChanged;
}

#endregion

#region Character Upgrades (Character screen UI)

/// <summary>Loại nâng cấp vĩnh viễn của một nhân vật. Mỗi loại ứng với một track trong ProgressionManager.</summary>
public enum CharacterUpgradeKind { Level, Star }

/// <summary>Lý do không nâng được; <see cref="None"/> nghĩa là nâng được.</summary>
public enum UpgradeBlock { None, MaxReached, NotEnoughCurrency, NotAvailable }

/// <summary>Một mục giá: loại tiền và số lượng.</summary>
public readonly struct CurrencyCost
{
    public readonly CurrencyType Currency;
    public readonly long Amount;

    public CurrencyCost(CurrencyType currency, long amount)
    {
        Currency = currency;
        Amount = amount;
    }
}

/// <summary>
/// Kết quả hỏi giá một lần nâng: cấp hiện tại/tối đa, danh sách giá và lý do bị chặn.
/// Giá là danh sách để sau này một lần nâng có thể tốn hai loại tiền.
/// </summary>
public readonly struct UpgradeQuote
{
    public readonly int Current;
    public readonly int Max;
    public readonly IReadOnlyList<CurrencyCost> Costs;
    public readonly UpgradeBlock Block;

    /// <summary>True khi không có lý do nào chặn.</summary>
    public bool CanUpgrade => Block == UpgradeBlock.None;

    public UpgradeQuote(int current, int max, IReadOnlyList<CurrencyCost> costs, UpgradeBlock block)
    {
        Current = current;
        Max = max;
        Costs = costs ?? Array.Empty<CurrencyCost>();
        Block = block;
    }
}

/// <summary>
/// Nâng cấp vĩnh viễn nhân vật (level, sao). Dùng bởi màn Character.
/// <see cref="GetQuote"/> và <see cref="TryUpgrade"/> đi thành cặp: TryUpgrade luôn kiểm tra lại điều kiện.
/// </summary>
public interface ICharacterUpgrades
{
    /// <summary>Cấp hiện tại, cấp tối đa, giá và lý do bị chặn của một loại nâng cấp.</summary>
    UpgradeQuote GetQuote(CharacterType type, CharacterUpgradeKind kind);

    /// <summary>
    /// Nâng một cấp nếu đủ điều kiện. Trả về đã nâng hay chưa; <paramref name="saved"/> là kết quả ghi file.
    /// saved=false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    bool TryUpgrade(CharacterType type, CharacterUpgradeKind kind, out bool saved);

    /// <summary>Phát sau khi một lần nâng thành công (đã đổi RAM và đã thử lưu).</summary>
    event Action<CharacterType, CharacterUpgradeKind> Upgraded;
}

#endregion

#region Stage Progress (StageRewards)

/// <summary>Mốc vượt màn lần đầu và thưởng thắng. Dùng bởi StageRewards (miền Stage).</summary>
public interface IStageProgress
{
    /// <summary>Màn đã từng được thắng (đã nhận thưởng lần đầu) chưa.</summary>
    bool HasFirstCleared(string stageId);

    /// <summary>Đánh dấu màn đã thắng và cộng thưởng trong <b>cùng một lần ghi file</b>, để hai thứ không lệch nhau.</summary>
    bool GrantVictoryRewards(string stageId, IReadOnlyDictionary<CurrencyType, long> rewards);
}

#endregion
