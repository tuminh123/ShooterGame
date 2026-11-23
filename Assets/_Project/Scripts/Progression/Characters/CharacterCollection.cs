using System;
using System.Collections.Generic;

/// <summary>
/// Luật bộ sưu tập nhân vật: nhận (gacha), ghép bằng mảnh, phân rã bản trùng, và nâng sao bằng bản trùng + mảnh.
/// Mảnh là tiền theo độ hiếm (<see cref="LevelDatabaseSO.ShardOf"/>), chung mọi nhân vật cùng độ hiếm.
/// </summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo; miền khác dùng qua <see cref="ICharacterCollection"/>.
/// Dữ liệu (sở hữu, bản trùng, sao) nằm trong <see cref="CharacterLevels"/> (chủ duy nhất của key character_levels);
/// class này chỉ chứa luật. Mỗi thao tác kiểm tra trước, đổi RAM, rồi ghi ví và tiến trình trong <b>một lần save</b>
/// (<see cref="Wallet.ApplyWith{T}"/>), để mảnh và nhân vật không bao giờ lệch nhau khi IO lỗi.
/// </remarks>
public sealed class CharacterCollection
{
    #region Fields

    private readonly CharacterLevels levels;
    private readonly Wallet wallet;
    private readonly LevelDatabaseSO rules;

    #endregion

    #region Events

    /// <summary>Sở hữu, bản trùng hoặc mảnh đổi (đã thử lưu).</summary>
    public event Action Changed;

    #endregion

    #region Constructor

    public CharacterCollection(CharacterLevels levels, Wallet wallet, LevelDatabaseSO rules)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        this.rules = rules != null ? rules : throw new ArgumentNullException(nameof(rules));
    }

    #endregion

    #region Queries

    /// <summary>Trạng thái bộ sưu tập của một nhân vật.</summary>
    public CharacterCollectionStatus GetStatus(CharacterType type)
    {
        CharacterRarity rarity = levels.GetCharacter(type).Rarity;
        LevelDatabaseSO.CollectionRule rule = rules.GetCollectionRule(rarity);
        CurrencyType shard = LevelDatabaseSO.ShardOf(rarity);
        return new CharacterCollectionStatus(levels.IsOwned(type), levels.GetCopies(type), shard, wallet.GetBalance(shard),
            rule.UnlockShards, rule.ShardsPerCopy);
    }

    /// <summary>Đã sở hữu chưa.</summary>
    public bool IsOwned(CharacterType type) => levels.IsOwned(type);

    #endregion

    #region Grant / Assemble / Dismantle

    /// <summary>Nhận một nhân vật: chưa có thì sở hữu luôn, đã có thì thêm một bản trùng. Không đụng ví nên chỉ ghi tiến trình.</summary>
    public CharacterGrantResult Grant(CharacterType type, out bool saved)
    {
        bool owned = levels.IsOwned(type);
        CharacterLevelsSaveData.Entry entry = levels.EditEntry(type);
        if (owned) entry.copies = checked(entry.copies + 1);
        else entry.owned = true;
        saved = SaveWith(type, null);
        return owned ? CharacterGrantResult.Duplicate : CharacterGrantResult.NewCharacter;
    }

    /// <summary>Ghép nhân vật chưa có: trừ đủ mảnh cùng độ hiếm rồi sở hữu. False nếu đã có hoặc chưa đủ mảnh (không đổi gì).</summary>
    public bool TryAssemble(CharacterType type, out bool saved)
    {
        saved = false;
        CharacterCollectionStatus status = GetStatus(type);
        if (!status.CanAssemble) return false;
        levels.EditEntry(type).owned = true;
        saved = SaveWith(type, Delta(status.ShardCurrency, -status.UnlockShards));
        return true;
    }

    /// <summary>Phân rã <paramref name="count"/> bản trùng: mỗi bản thành <c>ShardsPerCopy</c> mảnh cùng độ hiếm.</summary>
    public bool TryDismantle(CharacterType type, int count, out bool saved)
    {
        saved = false;
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        CharacterCollectionStatus status = GetStatus(type);
        if (status.Copies < count) return false;
        CharacterLevelsSaveData.Entry entry = levels.EditEntry(type);
        entry.copies -= count;
        saved = SaveWith(type, Delta(status.ShardCurrency, checked((long)count * status.ShardsPerCopy)));
        return true;
    }

    #endregion

    #region Stars

    /// <summary>Số mảnh (cùng độ hiếm) để mở sao kế tiếp; công thức Star Cost trong LevelDatabaseSO.</summary>
    public long GetNextStarCost(CharacterType type)
        => rules.StarCost.Evaluate(levels.GetStars(type) + 1, levels.GetCharacter(type).Rarity);

    /// <summary>
    /// Đủ để mở sao kế tiếp chưa: mảnh trong ví + giá trị các bản trùng của chính nhân vật này
    /// (ra trùng là "nâng cấp được luôn", không cần phân rã trước).
    /// </summary>
    public bool CanAffordNextStar(CharacterType type)
    {
        CharacterCollectionStatus status = GetStatus(type);
        long available = status.Shards + (long)status.Copies * status.ShardsPerCopy;
        return available >= GetNextStarCost(type);
    }

    /// <summary>
    /// Mở thêm một sao: dùng bản trùng của chính nhân vật trước (mỗi bản = <c>ShardsPerCopy</c> mảnh, giá trị dư vào ví mảnh),
    /// thiếu thì trừ mảnh. Bên gọi kiểm tra đã sở hữu, chưa tối đa và <see cref="CanAffordNextStar"/>; sai thì trả false.
    /// </summary>
    public bool TryAddStar(CharacterType type, out bool saved)
    {
        saved = false;
        if (!levels.IsOwned(type) || !CanAffordNextStar(type)) return false;
        CharacterCollectionStatus status = GetStatus(type);
        long cost = GetNextStarCost(type);
        // Số bản trùng vừa đủ (làm tròn lên) để trả giá, nhưng không vượt số đang có.
        int copiesUsed = (int)Math.Min(status.Copies, (cost + status.ShardsPerCopy - 1) / status.ShardsPerCopy);
        long shardDelta = (long)copiesUsed * status.ShardsPerCopy - cost; // Âm = trừ ví mảnh, dương = giá trị bản trùng dư.
        CharacterLevelsSaveData.Entry entry = levels.EditEntry(type);
        entry.copies -= copiesUsed;
        entry.stars++;
        saved = SaveWith(type, Delta(status.ShardCurrency, shardDelta));
        return true;
    }

    #endregion

    #region Private Helpers

    private static Dictionary<CurrencyType, long> Delta(CurrencyType currency, long amount)
        => new Dictionary<CurrencyType, long> { { currency, amount } };

    // Ghi tiến trình (và ví nếu có thay đổi) trong một lần save, rồi báo đổi. RAM đã đổi trước khi gọi.
    private bool SaveWith(CharacterType type, Dictionary<CurrencyType, long> walletDeltas)
    {
        bool saved = walletDeltas != null
            ? wallet.ApplyWith(walletDeltas, GameConstants.SaveKeys.CharacterLevels, levels.CreateSnapshot())
            : levels.SaveProgress();
        if (!saved) levels.ReportSaveFailure(type);
        Changed?.Invoke();
        return saved;
    }

    #endregion
}
