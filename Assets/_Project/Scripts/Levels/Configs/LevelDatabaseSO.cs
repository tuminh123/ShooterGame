using System;
using System.Collections.Generic;
using UnityEngine;

// * Tập trung config level vĩnh viễn theo nhân vật và một config level trong trận dùng chung.
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Shooter Game/Levels/Level Database")]
public sealed class LevelDatabaseSO : ScriptableObject
{
    #region Permanent Level Configurations

    [SerializeField] private List<PermanentLevelConfigSO> characterLevels = new List<PermanentLevelConfigSO>();  // * Danh sách config độc lập; mỗi config khai báo type, không nhập lại ID hoặc kéo character vào đây.

    #endregion

    #region Shared Run Configuration

    [SerializeField] private RunLevelConfigSO runLevelConfig;  // * Công thức EXP và bonus chung cho người chơi trong trận, không gắn vào template nhân vật.

    public RunLevelConfigSO RunLevelConfig => runLevelConfig;  // * Service tạo trạng thái riêng từ config dùng chung này.

    #endregion

    #region Runtime Lookup Creation

    // * Vòng duyệt thứ nhất dựng type → config; service tiếp tục duyệt danh sách nhân vật để ghép theo type.
    public Dictionary<CharacterType, PermanentLevelConfigSO> CreateTypeLookup()
    {
        var lookup = new Dictionary<CharacterType, PermanentLevelConfigSO>(characterLevels.Count);
        foreach (PermanentLevelConfigSO config in characterLevels)
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
