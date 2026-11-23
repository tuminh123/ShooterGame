using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Config cấp theo CharacterType dùng chung cho player/enemy, công thức Gold nâng level vĩnh viễn theo độ hiếm,
/// luật bộ sưu tập (nhân vật khởi đầu, mảnh để ghép, mảnh nhận khi gacha trùng, mảnh nâng sao) và một config EXP trong trận của player.
/// </summary>
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Shooter Game/Levels/Level Database")]
public sealed class LevelDatabaseSO : ScriptableObject
{
    #region Character Level Configurations

    [Tooltip("Ghép cấu hình cấp theo CharacterType; mỗi type chỉ có một config, không dùng thứ tự danh sách làm ID.")]
    [SerializeField] private List<CharacterLevelConfigSO> characterLevels = new List<CharacterLevelConfigSO>();

    #endregion

    #region Permanent Level Cost

    [Tooltip("Loại tiền nâng level vĩnh viễn của mọi nhân vật Player.")]
    [SerializeField] private CurrencyType permanentLevelCurrency = CurrencyType.Gold;
    [Tooltip("Gold cần cho mỗi mốc Lv → Lv+1 = công thức chung × hệ số theo độ hiếm của nhân vật (CharacterDataSO.Rarity). " +
             "Dùng chung cho mọi nhân vật cùng độ hiếm; độ hiếm cao phải có hệ số lớn hơn.")]
    [SerializeField] private RarityScaledFormula<CharacterRarity> permanentLevelCost = new RarityScaledFormula<CharacterRarity>();

    /// <summary>Loại tiền nâng level vĩnh viễn.</summary>
    public CurrencyType PermanentLevelCurrency => permanentLevelCurrency;

    /// <summary>Công thức Gold cần cho mỗi mốc level vĩnh viễn, theo độ hiếm của nhân vật.</summary>
    public RarityScaledFormula<CharacterRarity> PermanentLevelCost => permanentLevelCost;

    #endregion

    #region Character Collection

    /// <summary>Luật bộ sưu tập của một độ hiếm nhân vật: số mảnh để ghép và số mảnh nhận mỗi lần gacha trùng.</summary>
    [Serializable]
    public sealed class CollectionRule
    {
        [SerializeField] private CharacterRarity rarity;
        [Tooltip("Số mảnh riêng của nhân vật để ghép ra nhân vật đó khi chưa sở hữu.")]
        [SerializeField, Min(1)] private int unlockShards = 30;
        [Tooltip("Số mảnh riêng nhận được mỗi lần gacha trúng nhân vật đã sở hữu.")]
        [FormerlySerializedAs("shardsPerCopy")]
        [SerializeField, Min(1)] private int shardsPerDuplicate = 10;

        public CharacterRarity Rarity => rarity;
        public int UnlockShards => unlockShards;
        public int ShardsPerDuplicate => shardsPerDuplicate;
    }

    [Tooltip("Nhân vật sở hữu sẵn từ đầu game (không cần ghép hay gacha).")]
    [SerializeField] private List<CharacterType> starterCharacters = new List<CharacterType> { CharacterType.BlessedGladiator };
    [Tooltip("Mỗi CharacterRarity một mục: số mảnh để ghép và số mảnh nhận mỗi lần gacha trùng. Chung cho mọi nhân vật cùng độ hiếm.")]
    [SerializeField] private List<CollectionRule> collectionRules = new List<CollectionRule>();
    [Tooltip("Số mảnh riêng của nhân vật để mở sao thứ n = công thức(n) × hệ số độ hiếm. Sao 1 là Evaluate(1).")]
    [SerializeField] private RarityScaledFormula<CharacterRarity> starCost = new RarityScaledFormula<CharacterRarity>();

    /// <summary>Nhân vật sở hữu sẵn từ đầu game.</summary>
    public IReadOnlyList<CharacterType> StarterCharacters => starterCharacters;

    /// <summary>Công thức số mảnh để mở sao thứ n.</summary>
    public RarityScaledFormula<CharacterRarity> StarCost => starCost;

    /// <summary>Luật ghép/phân rã của một độ hiếm; ném lỗi nếu thiếu (Validate đã chặn trước).</summary>
    public CollectionRule GetCollectionRule(CharacterRarity rarity)
    {
        foreach (CollectionRule rule in collectionRules)
        {
            if (rule != null && rule.Rarity == rarity)
                return rule;
        }
        throw new KeyNotFoundException($"Level Database is missing collection rule for {rarity}.");
    }

    // Đủ luật cho mọi độ hiếm, công thức sao hợp lệ, nhân vật khởi đầu hợp lệ và không trùng.
    private void ValidateCollection()
    {
        foreach (CharacterRarity rarity in (CharacterRarity[])Enum.GetValues(typeof(CharacterRarity)))
        {
            CollectionRule rule = GetCollectionRule(rarity);
            if (rule.UnlockShards < 1 || rule.ShardsPerDuplicate < 1)
                throw new InvalidOperationException($"Collection rule of {rarity} needs Unlock Shards >= 1 and Shards Per Duplicate >= 1.");
        }
        starCost.Validate();
        var starters = new HashSet<CharacterType>();
        foreach (CharacterType type in starterCharacters)
        {
            if (type == CharacterType.None || !Enum.IsDefined(typeof(CharacterType), type) || !starters.Add(type))
                throw new InvalidOperationException("Starter characters must be defined and unique.");
        }
    }

    #endregion

    #region Shared Run Configuration

    [Tooltip("Công thức EXP và bonus cấp dùng chung cho Player trong trận; tiến trình được tạo riêng và reset theo lượt chơi.")]
    [SerializeField] private RunLevelConfigSO runLevelConfig;

    /// <summary>Service tạo trạng thái riêng từ config dùng chung này.</summary>
    public RunLevelConfigSO RunLevelConfig => runLevelConfig;

    #endregion

    #region Runtime Lookup Creation

    /// <summary>Vòng duyệt thứ nhất dựng type → config; service tiếp tục duyệt danh sách nhân vật để ghép theo type.</summary>
    public Dictionary<CharacterType, CharacterLevelConfigSO> CreateTypeLookup()
    {
        // Kiểm tra cùng lúc với bảng tra: CharacterLevels dựng lookup lúc khởi tạo nên công thức sai báo lỗi ngay khi vào game.
        permanentLevelCost.Validate();
        ValidateCollection();
        var lookup = new Dictionary<CharacterType, CharacterLevelConfigSO>(characterLevels.Count);
        foreach (CharacterLevelConfigSO config in characterLevels)
        {
            if (config == null || config.CharacterType == CharacterType.None || !Enum.IsDefined(typeof(CharacterType), config.CharacterType))
                throw new InvalidOperationException("Level database contains a missing config or invalid character type.");
            if (lookup.ContainsKey(config.CharacterType))
                throw new InvalidOperationException($"Duplicate level config type: {config.CharacterType}.");
            lookup.Add(config.CharacterType, config);
        }
        return lookup;
    }

    #endregion
}
