using System;
using System.Collections.Generic;
using UnityEngine;

// * Danh mục template nhân vật; CharacterType là khóa, tên hiển thị không tham gia tra cứu.
[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Shooter Game/Data/Character Database")]
public sealed class CharacterDatabaseSO : ScriptableObject
{
    #region Character Templates

    [SerializeField] private List<CharacterDataSO> characters = new List<CharacterDataSO>();  // * Mỗi nhân vật dùng type khác None, được khai báo duy nhất trong enum.

    #endregion

    #region Runtime Lookup Creation

    // * Dựng dictionary type → template cho service; không lưu cache trên asset.
    // ! Thiếu template, type không hợp lệ hoặc trùng type sẽ báo lỗi thay vì ghi đè.
    public Dictionary<CharacterType, CharacterDataSO> CreateLookup()
    {
        var lookup = new Dictionary<CharacterType, CharacterDataSO>(characters.Count);
        foreach (CharacterDataSO character in characters)
        {
            if (character == null || character.CharacterType == CharacterType.None ||
                !Enum.IsDefined(typeof(CharacterType), character.CharacterType))
                throw new InvalidOperationException("Character database contains a missing template or invalid type.");
            if (lookup.ContainsKey(character.CharacterType))
                throw new InvalidOperationException($"Duplicate character type: {character.CharacterType}.");
            lookup.Add(character.CharacterType, character);
        }
        return lookup;
    }

    #endregion
}
