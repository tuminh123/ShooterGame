using System;
using System.Collections.Generic;
using UnityEngine;

// Config cấp theo CharacterType dùng chung cho player/enemy và một config EXP trong trận của player.
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Shooter Game/Levels/Level Database")]
public sealed class LevelDatabaseSO : ScriptableObject
{
    #region Character Level Configurations

    [SerializeField] private List<CharacterLevelConfigSO> characterLevels = new List<CharacterLevelConfigSO>();  // * Danh sách config độc lập; mỗi config khai báo type, không nhập lại ID hoặc kéo character vào đây.

    #endregion

    #region Shared Run Configuration

    [SerializeField] private RunLevelConfigSO runLevelConfig;  // * Công thức EXP và bonus chung cho người chơi trong trận, không gắn vào template nhân vật.

    public RunLevelConfigSO RunLevelConfig => runLevelConfig;  // * Service tạo trạng thái riêng từ config dùng chung này.

    #endregion

    #region Runtime Lookup Creation

    // * Vòng duyệt thứ nhất dựng type → config; service tiếp tục duyệt danh sách nhân vật để ghép theo type.
    public Dictionary<CharacterType, CharacterLevelConfigSO> CreateTypeLookup()
    {
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
