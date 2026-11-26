using System;
using System.Collections.Generic;
using UnityEngine;

// Họ strategy "pool" của gacha, chọn thẳng trong GachaBannerSO bằng [SerializeReference] (giống IReward / IShopPrice).
// Đây là chỗ DUY NHẤT gacha tướng và gacha trang bị khác nhau: đổi "bậc vừa quay ra" thành một phần thưởng.
// Pool chỉ MÔ TẢ phần thưởng; việc trao (nhân vật trùng thành mảnh, roll dòng phụ) vẫn do Progression làm.

/// <summary>Nội dung quay được của một banner.</summary>
public interface IGachaPool
{
    /// <summary>Kiểm tra tham số; ném lỗi nếu cấu hình sai.</summary>
    void Validate();

    /// <summary>Pool có món nào ở bậc này không. Bậc trống bị bỏ khỏi bảng tỉ lệ (không bao giờ quay ra).</summary>
    bool HasTier(GachaTier tier);

    /// <summary>Chọn ngẫu nhiên một món ở bậc <paramref name="tier"/> (đã chắc <see cref="HasTier"/>).</summary>
    IReward Pick(GachaTier tier, System.Random random);
}

#region Character

/// <summary>Gacha tướng: bậc = độ hiếm của nhân vật. Nhân vật đã có tự thành mảnh khi trao (luật của Progression).</summary>
[Serializable]
public sealed class CharacterGachaPool : IGachaPool
{
    [Tooltip("Nhân vật Player có thể quay ra; bậc lấy theo Rarity của từng nhân vật.")]
    [SerializeField] private List<CharacterDataSO> characters = new List<CharacterDataSO>();

    /// <inheritdoc/>
    public void Validate()
    {
        if (characters.Count == 0) throw new InvalidOperationException("Character gacha pool needs at least one character.");
        foreach (CharacterDataSO character in characters) RewardChecks.RequirePlayer(character);
    }

    /// <inheritdoc/>
    public bool HasTier(GachaTier tier)
    {
        foreach (CharacterDataSO character in characters)
            if (character != null && (int)character.Rarity == (int)tier) return true;
        return false;
    }

    /// <inheritdoc/>
    public IReward Pick(GachaTier tier, System.Random random)
    {
        var matches = new List<CharacterDataSO>();
        foreach (CharacterDataSO character in characters)
            if (character != null && (int)character.Rarity == (int)tier) matches.Add(character);
        return new CharacterReward(matches[random.Next(matches.Count)]);
    }
}

#endregion

#region Equipment

/// <summary>Gacha trang bị: bậc = độ hiếm của món vừa quay (mọi mẫu đồ có đủ mọi độ hiếm), dòng phụ roll như đồ thường.</summary>
[Serializable]
public sealed class EquipmentGachaPool : IGachaPool
{
    [Tooltip("Mẫu đồ có thể quay ra (phải có trong Equipment Database).")]
    [SerializeField] private List<EquipmentDataSO> items = new List<EquipmentDataSO>();
    [SerializeField, Min(1)] private int level = 1;

    /// <inheritdoc/>
    public void Validate()
    {
        if (items.Count == 0 || items.Contains(null)) throw new InvalidOperationException("Equipment gacha pool needs items and no empty slot.");
        if (level < 1) throw new InvalidOperationException("Equipment gacha level must be at least 1.");
    }

    /// <inheritdoc/>
    public bool HasTier(GachaTier tier) => items.Count > 0;

    /// <inheritdoc/>
    public IReward Pick(GachaTier tier, System.Random random)
        => new EquipmentReward(items[random.Next(items.Count)], (EquipmentRarity)(int)tier, level, 1, new RolledSubstats());
}

#endregion
