using System;
using UnityEngine;

// * Level trong trận: một bộ bonus cố định mỗi cấp, EXP tính theo công thức; asset không giữ tiến trình lượt chơi.
[CreateAssetMenu(fileName = "RunLevelConfig", menuName = "Shooter Game/Levels/Run Level Config")]
public sealed class RunLevelConfigSO : ScriptableObject
{
    #region Run Growth Settings

    [SerializeField, Min(1)] private int maxLevel = 10;  // * Level tối đa trong một lượt chơi; bắt đầu từ level 1.
    [SerializeField] private StatBonus bonusPerLevel;   // * Mỗi lần lên cấp nhận đúng bộ bonus này; để 0 nếu chỉ thưởng qua lựa chọn nâng cấp.

    public int MaxLevel => maxLevel;                  // * Tới cấp này thì RunLevelProgress bỏ EXP dư.
    public StatBonus BonusPerLevel => bonusPerLevel;  // * Trả bản sao bonus cố định mỗi lần lên cấp.

    #endregion

    #region Experience Formula

    [SerializeField, Min(1)] private int initialExperience = 10;        // * EXP cần từ level 1 lên 2.
    [SerializeField, Min(0)] private int experienceIncreasePerLevel = 5;  // * EXP yêu cầu tăng thêm mỗi cấp; 0 nghĩa là chi phí EXP không đổi.

    public int InitialExperience => initialExperience;                    // * Hệ số A của công thức A + B × (level - 1).
    public int ExperienceIncreasePerLevel => experienceIncreasePerLevel;  // * Hệ số B, độc lập với chi phí nâng level vĩnh viễn.

    // * EXP từ level L lên L+1 = A + B × (L - 1); false khi cấp không hợp lệ hoặc đạt trần.
    // ! Trả yêu cầu riêng cho lần lên cấp, không phải tổng EXP cả trận; overflow int sẽ được báo thay vì đổi thành số âm.
    public bool TryGetNextLevel(int currentLevel, out int experienceRequired, out StatBonus bonus)
    {
        experienceRequired = 0;
        bonus = default;
        ValidateConfiguration();
        if (currentLevel < 1 || currentLevel >= maxLevel)
            return false;

        experienceRequired = checked((int)(initialExperience + (long)experienceIncreasePerLevel * (currentLevel - 1)));
        bonus = bonusPerLevel;
        return true;
    }

    #endregion

    #region Cumulative Stat Growth

    // * Tổng thưởng cố định = bonus mỗi cấp × (level - 1); không bao gồm phần thưởng được chọn qua UI.
    public StatBonus GetTotalBonus(int level)
    {
        ValidateConfiguration();
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level));
        return bonusPerLevel * (Mathf.Min(level, maxLevel) - 1);
    }

    #endregion

    #region Configuration Validation

    // * Kiểm tra tham số công thức, không sửa template dùng chung.
    private void ValidateConfiguration()
    {
        if (maxLevel < 1 || initialExperience < 1 || experienceIncreasePerLevel < 0)
            throw new InvalidOperationException("Run level config requires MaxLevel >= 1, initial EXP >= 1 and EXP increase >= 0.");
    }

    #endregion
}
