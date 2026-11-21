using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Bonus theo cấp dùng chung cho player và enemy. Phần EXP nâng cấp (nguyên liệu) chỉ dùng cho player.
[CreateAssetMenu(fileName = "CharacterLevelConfig", menuName = "Shooter Game/Levels/Character Level Config")]
public sealed class CharacterLevelConfigSO : ScriptableObject
{
    #region Character Growth Settings

    [Tooltip("CharacterType nhận bộ tăng trưởng này; phải khớp template trong Character Database.")]
    [SerializeField] private CharacterType characterType;
    [Tooltip("Trần cấp nhân vật. Cấp 1 dùng chỉ số gốc; tổng bonus ở cấp L = Bonus Per Level × (L - 1), giới hạn ở trần.")]
    [SerializeField, Min(1)] private int maxLevel = 10; // Level tối đa; level 1 dùng chỉ số gốc, chưa nhận bonus.
    [Tooltip("Bonus cộng thẳng cho mỗi cấp sau cấp 1. Tỷ lệ là điểm phần trăm: 5 = cộng 5 điểm %, không phải nhân 1.05.")]
    [SerializeField] private StatBonus bonusPerLevel;

    public int MaxLevel => maxLevel; // Mốc giới hạn cấp rõ ràng, không suy ra từ vị trí trong danh sách.
    public CharacterType CharacterType => characterType; // Khóa ghép cấu hình, đồng thời là khóa nhận diện nhân vật trong save; không cần tham chiếu character asset.
    public StatBonus BonusPerLevel => bonusPerLevel; // Trả bản sao struct, không sửa dữ liệu template từ bên ngoài.

    #endregion

    #region Upgrade Experience

    [Tooltip("Nguyên liệu (EXP vĩnh viễn) cần để lên từ cấp 1 lên 2; không phải EXP trong trận.")]
    [FormerlySerializedAs("initialUpgradeCost")]
    [SerializeField, Min(1)] private int initialRequired = 100; // Hệ số A; nhận lại giá trị Initial Upgrade Cost cũ trong asset.
    [Tooltip("Nguyên liệu cần từ cấp L lên L+1 = Initial Required + giá trị này × (L - 1). Đặt 0 để mọi cấp cần cùng lượng.")]
    [SerializeField, Min(0)] private int requiredIncreasePerLevel = 50; // Hệ số B; thanh của cấp sau dài hơn nên cần bấm nhiều lần hơn.
    [Tooltip("Lượng nguyên liệu tiêu và nạp vào thanh mỗi lần bấm Upgrade (tỉ lệ 1:1).")]
    [SerializeField, Min(1)] private int amountPerUpgrade = 25; // Một lần bấm = một lần tiêu = một lần cộng EXP, giống nhặt EXP trong trận.

    /// <summary>Hệ số A của công thức A + B × (level - 1).</summary>
    public int InitialRequired => initialRequired;
    /// <summary>Hệ số B của công thức A + B × (level - 1).</summary>
    public int RequiredIncreasePerLevel => requiredIncreasePerLevel;
    /// <summary>Lượng nguyên liệu tiêu và nạp vào thanh mỗi lần bấm Upgrade.</summary>
    public int AmountPerUpgrade => amountPerUpgrade;

    /// <summary>
    /// Nguyên liệu cần từ <paramref name="currentLevel"/> lên cấp kế tiếp = A + B × (L - 1);
    /// false nếu level không hợp lệ hoặc đã đạt trần. Cùng dạng công thức với EXP trong trận (RunLevelConfigSO).
    /// </summary>
    // Lưu ý: Trả yêu cầu riêng của một cấp, không phải tổng tích lũy; overflow int sẽ báo lỗi thay vì thành số âm.
    public bool TryGetRequired(int currentLevel, out int required)
    {
        required = 0;
        ValidateConfiguration();
        if (currentLevel < 1 || currentLevel >= maxLevel)
            return false;

        required = checked((int)(initialRequired + (long)requiredIncreasePerLevel * (currentLevel - 1)));
        return true;
    }

    #endregion

    #region Cumulative Stat Growth

    // Tổng bonus = bonus mỗi cấp × (level - 1); tính lại từ template, không cộng dồn lên kết quả cũ.
    // Lưu ý: Save cao hơn trần hiện tại chỉ nhận bonus đến MaxLevel; không tự sửa level đã lưu.
    public StatBonus GetTotalBonus(int level)
    {
        ValidateConfiguration();
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level));
        return bonusPerLevel * (Mathf.Min(level, maxLevel) - 1);
    }

    #endregion

    #region Star Perks

    /// <summary>Số sao tối đa; mỗi sao mở đúng một perk theo thứ tự.</summary>
    public const int MaxStars = 5;

    [Tooltip("Perk mở theo sao: phần tử 1 mở ở sao 1, phần tử 2 ở sao 2… Tối đa 5. Sao chưa có perk vẫn nâng được nhưng không cộng gì.")]
    [SerializeField] private StarPerk[] starPerks = Array.Empty<StarPerk>();

    /// <summary>Perk theo thứ tự sao (chỉ đọc).</summary>
    public IReadOnlyList<StarPerk> StarPerks => starPerks;

    /// <summary>Tổng bonus của các perk đã mở với <paramref name="stars"/> sao.</summary>
    public StatBonus GetStarBonus(int stars)
    {
        ValidateConfiguration();
        StatBonus total = default;
        int unlocked = Mathf.Clamp(stars, 0, starPerks.Length);
        for (int i = 0; i < unlocked; i++)
            if (starPerks[i] != null) total += starPerks[i].Bonus;
        return total;
    }

    #endregion

    #region Configuration Validation

    // Chặn cấu hình cấp/EXP nâng cấp không hợp lệ trước khi tính; không tự sửa asset trong runtime.
    private void ValidateConfiguration()
    {
        bonusPerLevel.Validate();
        if (starPerks != null && starPerks.Length > MaxStars)
            throw new InvalidOperationException($"Character level config supports at most {MaxStars} star perks.");
        if (starPerks != null)
            foreach (StarPerk perk in starPerks) perk?.Bonus.Validate();
        if (maxLevel < 1 || initialRequired < 1 || requiredIncreasePerLevel < 0 || amountPerUpgrade < 1)
            throw new InvalidOperationException("Character level config requires MaxLevel >= 1, initial required >= 1, increase >= 0 and amount per upgrade >= 1.");
    }

    #endregion
}
