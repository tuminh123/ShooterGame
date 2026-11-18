using System;
using System.Collections.Generic;

// Ranh giới của miền Progression: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do ProgressionManager implement. Wallet, CharacterLevels, Loadout và StageProgress
// nằm bên trong miền, không được bind vào Zenject.
// Progression giữ dữ liệu còn lại giữa các lượt chơi (ví, level vĩnh viễn, trang bị, mốc vượt màn) và chỉ phụ thuộc Save.
// Mỗi vai trò ứng với một nhóm nơi dùng thực tế; API chưa có ai dùng (nâng level vĩnh viễn, đổi trang bị)
// vẫn nằm trên system bên trong và sẽ có vai trò riêng khi UI tương ứng ra đời.
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
