using System;

/// <summary>
/// Luật bộ sưu tập nhân vật: nhận (gacha), ghép bằng mảnh và nâng sao bằng mảnh.
/// Mảnh thuộc riêng từng nhân vật (<see cref="CharacterShards"/>): gacha trúng nhân vật đã có thì thành mảnh của chính nó.
/// Số mảnh nhận mỗi lần, số mảnh để ghép và giá sao đều theo độ hiếm (luật trong <see cref="LevelDatabaseSO"/>).
/// </summary>
/// <remarks>
/// Do <see cref="ProgressionManager"/> tạo; miền khác dùng qua <see cref="ICharacterCollection"/>.
/// Dữ liệu (sở hữu, mảnh, sao) nằm trong <see cref="CharacterLevels"/> (chủ duy nhất của key character_levels);
/// class này chỉ chứa luật. Mảnh không đi qua ví nên mỗi thao tác chỉ kiểm tra, đổi RAM rồi ghi tiến trình một lần save.
/// </remarks>
public sealed class CharacterCollection
{
    #region Fields

    private readonly CharacterLevels levels;
    private readonly LevelDatabaseSO rules;

    #endregion

    #region Events

    /// <summary>Sở hữu hoặc mảnh đổi (đã thử lưu).</summary>
    public event Action Changed;

    #endregion

    #region Constructor

    public CharacterCollection(CharacterLevels levels, LevelDatabaseSO rules)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.rules = rules != null ? rules : throw new ArgumentNullException(nameof(rules));
    }

    #endregion

    #region Queries

    /// <summary>Trạng thái bộ sưu tập của một nhân vật.</summary>
    public CharacterCollectionStatus GetStatus(CharacterType type)
    {
        LevelDatabaseSO.CollectionRule rule = RuleOf(type);
        return new CharacterCollectionStatus(levels.IsOwned(type), levels.GetShards(type), rule.UnlockShards, rule.ShardsPerDuplicate);
    }

    /// <summary>Đã sở hữu chưa.</summary>
    public bool IsOwned(CharacterType type) => levels.IsOwned(type);

    #endregion

    #region Grant / Assemble

    /// <summary>
    /// Nhận một nhân vật: chưa có thì sở hữu luôn, đã có thì cộng mảnh riêng của nó (số mảnh theo độ hiếm).
    /// <paramref name="gained"/> trả lại số mảnh vừa cộng để nơi gọi báo người chơi, không phải tự tính lại luật.
    /// </summary>
    public CharacterGrantResult Grant(CharacterType type, out CharacterShards gained, out bool saved)
    {
        CharacterGrantResult result = ApplyGrant(type, out gained);
        saved = Save(type);
        return result;
    }

    /// <summary>Như <see cref="Grant"/> nhưng chỉ đổi RAM; RewardGranter ghi cùng các phần thưởng khác rồi gọi <see cref="PublishChanged"/>.</summary>
    internal CharacterGrantResult ApplyGrant(CharacterType type, out CharacterShards gained)
    {
        bool owned = levels.IsOwned(type);
        CharacterLevelsSaveData.Entry entry = levels.EditEntry(type);
        gained = default;
        if (owned)
        {
            int amount = RuleOf(type).ShardsPerDuplicate;
            entry.shards = checked(entry.shards + amount);
            gained = new CharacterShards(type, amount);
        }
        else
        {
            entry.owned = true;
        }
        return owned ? CharacterGrantResult.Duplicate : CharacterGrantResult.NewCharacter;
    }

    /// <summary>Cộng mảnh riêng của một nhân vật, chỉ đổi RAM (xem <see cref="ApplyGrant"/>).</summary>
    internal void ApplyShards(CharacterType type, long amount)
    {
        if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount));
        CharacterLevelsSaveData.Entry entry = levels.EditEntry(type);
        entry.shards = checked(entry.shards + amount);
    }

    /// <summary>Báo bộ sưu tập đổi sau khi đã thử lưu.</summary>
    internal void PublishChanged() => Changed?.Invoke();

    /// <summary>Ghép nhân vật chưa có: trừ đủ mảnh riêng của nó rồi sở hữu. False nếu đã có hoặc chưa đủ mảnh (không đổi gì).</summary>
    public bool TryAssemble(CharacterType type, out bool saved)
    {
        saved = false;
        CharacterCollectionStatus status = GetStatus(type);
        if (!status.CanAssemble) return false;
        CharacterLevelsSaveData.Entry entry = levels.EditEntry(type);
        entry.owned = true;
        entry.shards -= status.UnlockShards;
        saved = Save(type);
        return true;
    }

    #endregion

    #region Stars

    /// <summary>Số mảnh để mở sao kế tiếp; công thức Star Cost trong LevelDatabaseSO theo độ hiếm của nhân vật.</summary>
    public long GetNextStarCost(CharacterType type)
        => rules.StarCost.Evaluate(levels.GetStars(type) + 1, levels.GetCharacter(type).Rarity);

    /// <summary>Đủ mảnh riêng của nhân vật để mở sao kế tiếp chưa.</summary>
    public bool CanAffordNextStar(CharacterType type) => levels.GetShards(type) >= GetNextStarCost(type);

    /// <summary>
    /// Mở thêm một sao bằng cách trừ mảnh riêng của nhân vật.
    /// Bên gọi kiểm tra đã sở hữu và chưa tối đa; chưa sở hữu hoặc không đủ mảnh thì trả false.
    /// </summary>
    public bool TryAddStar(CharacterType type, out bool saved)
    {
        saved = false;
        if (!levels.IsOwned(type) || !CanAffordNextStar(type)) return false;
        long cost = GetNextStarCost(type);
        CharacterLevelsSaveData.Entry entry = levels.EditEntry(type);
        entry.shards -= cost;
        entry.stars++;
        saved = Save(type);
        return true;
    }

    #endregion

    #region Private Helpers

    private LevelDatabaseSO.CollectionRule RuleOf(CharacterType type)
        => rules.GetCollectionRule(levels.GetCharacter(type).Rarity);

    // Ghi tiến trình trong một lần save rồi báo đổi. RAM đã đổi trước khi gọi.
    private bool Save(CharacterType type)
    {
        bool saved = levels.SaveProgress();
        if (!saved) levels.ReportSaveFailure(type);
        PublishChanged();
        return saved;
    }

    #endregion
}
