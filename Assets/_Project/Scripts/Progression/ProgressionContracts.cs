using System;
using System.Collections.Generic;

// Ranh giới của miền Progression: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do ProgressionManager implement. Wallet, CharacterLevels, EquipmentInventory, EquipmentLoadout
// và StageProgress nằm bên trong miền, không được bind vào Zenject.
// Progression giữ dữ liệu còn lại giữa các lượt chơi (ví, level vĩnh viễn, trang bị, mốc vượt màn) và chỉ phụ thuộc Save.
// Mỗi vai trò ứng với một nhóm nơi dùng thực tế. Nâng level/sao đi qua ICharacterUpgrades (màn Character), túi đồ và
// đồ đang mặc đi qua IEquipment (màn Equipment), nhận đồ đi qua IEquipmentGrant (panel test; sau này thưởng Stage/loot).
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
/// Tổng bonus chỉ số vĩnh viễn của một nhân vật Player: level vĩnh viễn + chỉ số dự phòng của sao (sao không có bước skill) + trang bị.
/// Dùng bởi PlayerSystem, màn Character và màn Equipment.
/// </summary>
/// <remarks>PlayerSystem chỉ cần một con số tổng, nên vai trò này gộp sẵn thay vì lộ hai system riêng.</remarks>
public interface IPermanentBonuses
{
    /// <summary>Bonus cộng thẳng của <paramref name="type"/>; type không có cấu hình level thì ném lỗi.</summary>
    StatBonus GetBonus(CharacterType type);

    /// <summary>Level/sao của một nhân vật, đồ đang mặc hoặc level của một món đang mặc vừa đổi: Player cần tính lại nguồn bonus.</summary>
    event Action BonusesChanged;
}

#endregion

#region Character Progress (Combat)

/// <summary>
/// Tiến trình vĩnh viễn ảnh hưởng skill của Player: level và sao của nhân vật, đồ và set đang mặc.
/// Dùng bởi Combat (CharacterSkillSystem) lúc spawn để tính skill nào mở và mỗi skill cấp mấy.
/// </summary>
/// <remarks>
/// Chỉ trả dữ liệu, không đọc skill: luật "level/sao → skill" nằm trong CharacterDataSO, luật "level đồ → skill" trong
/// EquipmentDataSO.TryGetSkill và skill của set trong EquipmentSetData, nên code Progression (miền dưới) không xử lý skill của Combat (miền trên).
/// </remarks>
public interface ICharacterProgress
{
    /// <summary>Level vĩnh viễn; nhân vật chưa có bản ghi là 1.</summary>
    int GetLevel(CharacterType type);

    /// <summary>Số sao đã mở; nhân vật chưa có bản ghi là 0.</summary>
    int GetStars(CharacterType type);

    /// <summary>Ghi đồ đang mặc theo thứ tự ô (Vũ khí → Nhẫn) vào <paramref name="result"/> (list được xóa trước). Đồ dùng chung mọi nhân vật.</summary>
    void GetEquippedItems(List<EquipmentItem> result);

    /// <summary>Ghi các set đang bật (đủ số món) vào <paramref name="result"/> (list được xóa trước).</summary>
    void GetActiveSets(List<EquipmentSetData> result);
}

#endregion

#region Character Upgrades (Character screen UI)

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
/// Kết quả hỏi giá một lần nâng: cấp hiện tại/tối đa, tiến độ thanh của cấp hiện tại, danh sách giá và lý do bị chặn.
/// Giá là danh sách để sau này một lần nâng có thể tốn hai loại tiền.
/// </summary>
public readonly struct UpgradeQuote
{
    public readonly int Current;
    public readonly int Max;
    /// <summary>Điểm đã nạp vào thanh của cấp hiện tại; 0 với loại nâng cấp không có thanh.</summary>
    public readonly int Progress;
    /// <summary>Điểm cần để đầy thanh; 0 khi đã ở trần hoặc loại nâng cấp không có thanh.</summary>
    public readonly int ProgressRequired;
    public readonly IReadOnlyList<CurrencyCost> Costs;
    public readonly UpgradeBlock Block;

    /// <summary>True khi không có lý do nào chặn.</summary>
    public bool CanUpgrade => Block == UpgradeBlock.None;

    public UpgradeQuote(int current, int max, int progress, int progressRequired,
        IReadOnlyList<CurrencyCost> costs, UpgradeBlock block)
    {
        Current = current;
        Max = max;
        Progress = progress;
        ProgressRequired = progressRequired;
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

#region Equipment (màn Equipment)

/// <summary>
/// Túi đồ và đồ đang mặc (dùng chung mọi nhân vật). Dùng bởi màn Equipment: đọc, mặc/tháo, hỏi giá và nâng cấp.
/// Mọi thay đổi được lưu ngay; saved=false nghĩa là IO lỗi, RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
/// </summary>
public interface IEquipment
{
    /// <summary>Mọi bản đồ đang sở hữu (cả đồ đang mặc), theo thứ tự nhận.</summary>
    IReadOnlyList<EquipmentItem> Items { get; }

    /// <summary>Đồ đang mặc ở ô; null nếu ô trống.</summary>
    EquipmentItem GetEquipped(EquipmentSlot slot);

    /// <summary>Số món của set đang mặc (ví dụ "Samurai 2/4").</summary>
    int CountEquipped(EquipmentSet set);

    /// <summary>Mặc vào ô của món; ô đang có món khác thì đổi chỗ. Trả về có đổi hay không.</summary>
    bool Equip(EquipmentItem item, out bool saved);

    /// <summary>Tháo đồ ở ô về túi. Trả về có đổi hay không.</summary>
    bool Unequip(EquipmentSlot slot, out bool saved);

    /// <summary>Tháo mọi món trong một lần ghi file. Trả về có đổi hay không.</summary>
    bool UnequipAll(out bool saved);

    /// <summary>Level hiện tại/tối đa, giá nâng và lý do bị chặn của một món.</summary>
    UpgradeQuote GetQuote(EquipmentItem item);

    /// <summary>Nâng một level nếu đủ điều kiện (luôn kiểm tra lại). Trả về đã nâng hay chưa.</summary>
    bool TryUpgrade(EquipmentItem item, out bool saved);

    /// <summary>Phát sau khi túi đồ hoặc đồ đang mặc đổi (đã đổi RAM và đã thử lưu).</summary>
    event Action Changed;
}

#endregion

#region Equipment Grant (panel test; sau này thưởng Stage/loot)

/// <summary>
/// Trao một bản trang bị mới cho người chơi (dòng phụ được roll ngay theo độ hiếm). Dùng bởi nút Gacha của panel test;
/// thưởng Stage/loot sau này dùng cùng vai trò.
/// </summary>
public interface IEquipmentGrant
{
    /// <summary>
    /// Thêm một bản đồ của <paramref name="item"/> (phải có trong Equipment Database) với độ hiếm <paramref name="rarity"/>.
    /// Trả về bản đồ vừa nhận; <paramref name="saved"/> = false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// </summary>
    EquipmentItem Grant(EquipmentDataSO item, EquipmentRarity rarity, out bool saved);
}

#endregion
