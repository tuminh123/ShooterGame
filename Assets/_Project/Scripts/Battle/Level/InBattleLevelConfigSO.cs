using System;
using UnityEngine;

/// <summary>Level trong trận: một bộ bonus cố định mỗi cấp, EXP tính theo công thức; asset không giữ tiến trình lượt chơi.</summary>
[CreateAssetMenu(fileName = "InBattleLevelConfig", menuName = "Shooter Game/Levels/In-Battle Level Config")]
public sealed class InBattleLevelConfigSO : ScriptableObject
{
    #region Battle Growth Settings

    [Tooltip("Trần cấp trong một lượt chơi. Bắt đầu cấp 1; trần 20 cho tối đa 19 lần lên cấp.")]
    [SerializeField, Min(1)] private int maxLevel = 20; // Trần level trong trận, chỉnh qua Inspector; bắt đầu level 1 nên trần 20 cho tối đa 19 lần lên cấp.
    [Tooltip("Bonus cộng cố định mỗi lần lên cấp trong trận; để 0 nếu chỉ muốn nhận thưởng từ bảng chọn skill. Không bao gồm bonus skill được chọn.")]
    [SerializeField] private StatBonus bonusPerLevel;

    /// <summary>Tới cấp này thì InBattleLevelProgress bỏ EXP dư.</summary>
    public int MaxLevel => maxLevel;
    /// <summary>Trả bản sao bonus cố định mỗi lần lên cấp.</summary>
    public StatBonus BonusPerLevel => bonusPerLevel;

    #endregion

    #region Experience Formula

    [Tooltip("EXP cần cho mỗi mốc Lv → Lv+1 trong trận. Chọn kiểu công thức (Linear, Geometric...) rồi chỉnh tham số; độc lập với giá nâng level vĩnh viễn.")]
    [SerializeReference] private ILevelFormula experienceFormula = new LinearFormula(10, 5); // Mặc định 10, 15, 20... EXP.

    /// <summary>Công thức EXP đang chọn (Inspector dùng để hiện bảng xem trước).</summary>
    public ILevelFormula ExperienceFormula => experienceFormula;

    /// <summary>
    /// EXP cần để lên từ <paramref name="currentLevel"/> lên cấp kế tiếp (đọc từ công thức EXP); false khi cấp không hợp lệ hoặc đạt trần.
    /// Lưu ý: Trả yêu cầu riêng cho lần lên cấp, không phải tổng EXP cả trận; vượt int sẽ báo lỗi tràn số thay vì thành số âm.
    /// </summary>
    public bool TryGetNextLevel(int currentLevel, out int experienceRequired, out StatBonus bonus)
    {
        experienceRequired = 0;
        bonus = default;
        ValidateConfiguration();
        if (currentLevel < 1 || currentLevel >= maxLevel)
            return false;

        experienceRequired = checked((int)experienceFormula.Evaluate(currentLevel));
        bonus = bonusPerLevel;
        return true;
    }

    #endregion

    #region Cumulative Stat Growth

    /// <summary>Tổng thưởng cố định = bonus mỗi cấp × (level - 1); không bao gồm phần thưởng được chọn qua UI.</summary>
    public StatBonus GetTotalBonus(int level)
    {
        ValidateConfiguration();
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level));
        return bonusPerLevel * (Mathf.Min(level, maxLevel) - 1);
    }

    #endregion

    #region Configuration Validation

    // Kiểm tra tham số công thức, không sửa template dùng chung.
    private void ValidateConfiguration()
    {
        if (maxLevel < 1)
            throw new InvalidOperationException("Battle level config requires MaxLevel >= 1.");
        if (experienceFormula == null)
            throw new InvalidOperationException("Battle level config needs an Experience Formula.");
        experienceFormula.Validate();
    }

    #endregion
}
