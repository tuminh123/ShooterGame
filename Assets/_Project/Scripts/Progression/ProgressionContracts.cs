using System;
using System.Collections.Generic;

// Ranh giới của miền Progression: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do ProgressionManager implement. Wallet, CharacterLevels, EquipmentInventory, EquipmentLoadout
// và StageProgress nằm bên trong miền, không được bind vào Zenject.
// Progression giữ dữ liệu còn lại giữa các lượt chơi (ví, level vĩnh viễn, trang bị, mốc vượt màn) và chỉ phụ thuộc Save.
// Mỗi vai trò ứng với một nhóm nơi dùng thực tế. Nâng level/sao đi qua ICharacterUpgrades (màn Character), túi đồ và
// đồ đang mặc đi qua IEquipment (màn Equipment), nhận đồ đi qua IEquipmentGrant (panel test; sau này thưởng Stage/loot).
// Xem Docs/Architecture/Refactor-Architecture.md.

#region Wallet (Home UI, Battle, Stage)

/// <summary>
/// Ví tiền vĩnh viễn. Dùng bởi Home UI (đọc số dư), BattleManager (vàng nhặt trong trận).
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

    /// <summary>Đủ tiền cho mọi mục của <paramref name="costs"/> chưa (nhiều mục cùng loại được cộng gộp). Chỉ đọc.</summary>
    bool CanAfford(IReadOnlyList<CurrencyCost> costs);
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
    /// <summary>Mảnh cần để nâng (nâng sao trả bằng mảnh riêng của nhân vật, không đi qua ví). Type = None khi không tốn mảnh.</summary>
    public readonly CharacterShards ShardCost;
    /// <summary>Mảnh nhân vật đang có, để UI hiện "cần/có"; chỉ có nghĩa khi <see cref="HasShardCost"/>.</summary>
    public readonly long ShardsOwned;
    public readonly UpgradeBlock Block;

    /// <summary>True khi lần nâng này tốn mảnh riêng của nhân vật.</summary>
    public bool HasShardCost => ShardCost.Type != CharacterType.None;

    /// <summary>True khi không có lý do nào chặn.</summary>
    public bool CanUpgrade => Block == UpgradeBlock.None;

    /// <summary>Lần nâng trả bằng tiền trong ví (level, trang bị).</summary>
    public UpgradeQuote(int current, int max, int progress, int progressRequired,
        IReadOnlyList<CurrencyCost> costs, UpgradeBlock block)
    {
        Current = current;
        Max = max;
        Progress = progress;
        ProgressRequired = progressRequired;
        Costs = costs ?? Array.Empty<CurrencyCost>();
        ShardCost = default;
        ShardsOwned = 0;
        Block = block;
    }

    /// <summary>Lần nâng trả bằng mảnh riêng của nhân vật (sao); không có thanh tiến độ và không tốn tiền ví.</summary>
    public UpgradeQuote(int current, int max, CharacterShards shardCost, long shardsOwned, UpgradeBlock block)
    {
        Current = current;
        Max = max;
        Progress = 0;
        ProgressRequired = 0;
        Costs = Array.Empty<CurrencyCost>();
        ShardCost = shardCost;
        ShardsOwned = shardsOwned;
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

    /// <summary>
    /// (nhân vật, loại, số cấp vừa tăng) — chỉ phát khi level/sao THẬT SỰ tăng, khác <see cref="Upgraded"/> phát mỗi lần bấm.
    /// Dùng bởi Mission.
    /// </summary>
    event Action<CharacterType, CharacterUpgradeKind, int> CharacterRaised;
}

#endregion

#region Character Collection (màn Character, panel test; sau này gacha thật)

/// <summary>
/// Một lượng mảnh của một nhân vật cụ thể. Mảnh thuộc riêng từng nhân vật (không dùng chung theo độ hiếm),
/// nên mọi nơi nói về mảnh (giá, kết quả gacha) đều mang theo nhân vật nó thuộc về.
/// </summary>
public readonly struct CharacterShards
{
    public readonly CharacterType Type;
    public readonly long Amount;

    public CharacterShards(CharacterType type, long amount)
    {
        Type = type;
        Amount = amount;
    }
}

/// <summary>Trạng thái bộ sưu tập của một nhân vật: đã có chưa, mảnh riêng đang có, số mảnh cần để ghép và nhận mỗi lần trùng.</summary>
public readonly struct CharacterCollectionStatus
{
    public readonly bool Owned;
    /// <summary>Mảnh riêng của nhân vật này (nhận khi gacha trùng).</summary>
    public readonly long Shards;
    /// <summary>Số mảnh để ghép (chỉ có nghĩa khi chưa sở hữu).</summary>
    public readonly int UnlockShards;
    /// <summary>Số mảnh nhận được mỗi lần gacha trúng nhân vật đã có.</summary>
    public readonly int ShardsPerDuplicate;

    /// <summary>Chưa sở hữu và đủ mảnh để ghép.</summary>
    public bool CanAssemble => !Owned && Shards >= UnlockShards;

    public CharacterCollectionStatus(bool owned, long shards, int unlockShards, int shardsPerDuplicate)
    {
        Owned = owned;
        Shards = shards;
        UnlockShards = unlockShards;
        ShardsPerDuplicate = shardsPerDuplicate;
    }
}

/// <summary>
/// Bộ sưu tập nhân vật Player: sở hữu, nhận mảnh khi gacha trùng, ghép bằng mảnh.
/// Mảnh thuộc riêng từng nhân vật; số mảnh mỗi lần trùng và để ghép theo độ hiếm (luật trong LevelDatabaseSO).
/// Dùng bởi màn Character và panel test.
/// </summary>
/// <remarks>Mỗi thao tác đổi RAM rồi ghi tiến trình trong một lần save; saved = false thì không gọi lại lệnh.</remarks>
public interface ICharacterCollection
{
    /// <summary>Trạng thái bộ sưu tập của <paramref name="type"/>.</summary>
    CharacterCollectionStatus GetStatus(CharacterType type);

    /// <summary>
    /// Nhận một nhân vật (gacha): chưa có thì sở hữu luôn, đã có thì thành mảnh riêng của nhân vật đó.
    /// <paramref name="gained"/> là mảnh vừa nhận (Amount = 0 khi là nhân vật mới), để nơi gọi báo người chơi mà không tự tính lại luật.
    /// </summary>
    CharacterGrantResult Grant(CharacterType type, out CharacterShards gained, out bool saved);

    /// <summary>Ghép nhân vật chưa có bằng mảnh riêng của nó; false nếu đã có hoặc chưa đủ mảnh.</summary>
    bool TryAssemble(CharacterType type, out bool saved);

    /// <summary>Phát sau khi sở hữu hoặc mảnh của bộ sưu tập đổi (đã thử lưu).</summary>
    event Action Changed;
}

#endregion

#region Stage Progress (StageRewards)

/// <summary>Mốc vượt màn lần đầu và thưởng kết quả màn. Dùng bởi StageRewards (miền Stage).</summary>
public interface IStageProgress
{
    /// <summary>Màn đã từng được thắng (đã nhận thưởng lần đầu) chưa.</summary>
    bool HasFirstCleared(string stageId);

    /// <summary>
    /// Trao thưởng kết quả; <paramref name="markCleared"/> thì đánh dấu màn đã thắng trong <b>cùng một lần ghi file</b>,
    /// để hai thứ không lệch nhau. Trả về kết quả thực tế; <paramref name="saved"/> = false thì không gọi lại.
    /// </summary>
    RewardResult GrantRewards(string stageId, IReadOnlyList<IReward> rewards, bool markCleared, out bool saved);
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

    /// <summary>Một món vừa lên một level; tách khỏi <see cref="Changed"/> (cũng phát khi nhận/mặc/tháo). Dùng bởi Mission.</summary>
    event Action<EquipmentItem> EquipmentLeveledUp;
}

#endregion

#region Reward Grant (Mission, Stage)

/// <summary>Trao một gói thưởng nhiều loại (tiền, nhân vật, mảnh, trang bị) trong một lần ghi file. Dùng bởi MissionManager và StageRewards (kiểm tra).</summary>
public interface IRewardGrant
{
    /// <summary>Kiểm tra gói trao được không; ném lỗi nếu cấu hình sai, không đổi gì.</summary>
    void ValidateRewards(IReadOnlyList<IReward> rewards);

    /// <summary>
    /// Trao gói và ghi cùng <paramref name="progress"/> (snapshot "đã nhận" của bên gọi). Trả về kết quả thực tế;
    /// <paramref name="saved"/> = false thì RAM đã đổi và snapshot chờ ghi lại — không gọi lại lệnh.
    /// Không gọi từ trong callback của ví hoặc túi đồ.
    /// </summary>
    RewardResult GrantRewards(IReadOnlyList<IReward> rewards, SaveEntry progress, out bool saved);

    /// <summary>
    /// Mua gói: trừ <paramref name="costs"/>, trao <paramref name="rewards"/> và ghi <paramref name="progress"/> trong MỘT lần ghi.
    /// Thiếu tiền thì ném lỗi và không đổi gì — bên gọi kiểm tra <see cref="IWallet.CanAfford"/> trước. Dùng bởi Shop.
    /// </summary>
    RewardResult PurchaseRewards(IReadOnlyList<CurrencyCost> costs, IReadOnlyList<IReward> rewards, SaveEntry progress, out bool saved);
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
