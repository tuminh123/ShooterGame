using System;
using UnityEngine;

// * Bonus theo cấp dùng chung cho player và enemy. Chi phí nâng cấp chỉ dùng cho player.
[CreateAssetMenu(fileName = "CharacterLevelConfig", menuName = "Shooter Game/Levels/Character Level Config")]
public sealed class CharacterLevelConfigSO : ScriptableObject
{
    #region Character Growth Settings

    [Tooltip("CharacterType nhận bộ tăng trưởng này; phải khớp template trong Character Database.")]
    [SerializeField] private CharacterType characterType;  // * Type nhân vật nhận config này; service ghép với type trên CharacterDataSO khi init.
    [Tooltip("Trần cấp nhân vật. Cấp 1 dùng chỉ số gốc; tổng bonus ở cấp L = Bonus Per Level × (L - 1), giới hạn ở trần.")]
    [SerializeField, Min(1)] private int maxLevel = 10;  // * Level tối đa; level 1 dùng chỉ số gốc, chưa nhận bonus.
    [Tooltip("Bonus cộng thẳng cho mỗi cấp sau cấp 1. Tỷ lệ là điểm phần trăm: 5 = cộng 5 điểm %, không phải nhân 1.05.")]
    [SerializeField] private StatBonus bonusPerLevel;   // * Một bộ bonus cố định, cộng lại mỗi cấp tăng thêm.

    public int MaxLevel => maxLevel;             // * Mốc giới hạn cấp rõ ràng, không suy ra từ vị trí trong danh sách.
    public CharacterType CharacterType => characterType;  // * Khóa ghép cấu hình, đồng thời là khóa nhận diện nhân vật trong save; không cần tham chiếu character asset.
    public StatBonus BonusPerLevel => bonusPerLevel;  // * Trả bản sao struct, không sửa dữ liệu template từ bên ngoài.

    #endregion

    #region Upgrade Resource Cost

    [Tooltip("Chi phí nâng vĩnh viễn từ cấp 1 lên 2; không phải EXP trong trận.")]
    [SerializeField, Min(1)] private int initialUpgradeCost = 100;           // * Tài nguyên cần để nâng từ level 1 lên 2; không phải EXP trong trận.
    [Tooltip("Chi phí từ cấp L lên L+1 = làm tròn lên(Initial Cost × hệ số^(L - 1)). 1.2 tăng khoảng 20% mỗi cấp; tối thiểu 1.")]
    [SerializeField, Min(1f)] private double upgradeCostMultiplier = 1.2d;  // * Mỗi cấp nhân chi phí với hệ số này; 1 = không tăng, 1.2 = tăng khoảng 20%.

    public int InitialUpgradeCost => initialUpgradeCost;              // * Chi phí lần nâng cấp đầu tiên.
    public double UpgradeCostMultiplier => upgradeCostMultiplier;     // * Quyết định chi phí tăng nhanh đến đâu ở cấp cao.

    // * Tính ceil(chi phí đầu × hệ số^(level hiện tại - 1)); false nếu level không hợp lệ hoặc đã đạt trần.
    // ! Chỉ trả chi phí, chưa chọn loại tiền, kiểm tra ví hoặc trừ tài nguyên; vượt phạm vi long/decimal sẽ báo overflow.
    public bool TryGetUpgradeCost(int currentLevel, out long cost)
    {
        cost = 0;
        ValidateConfiguration();
        if (currentLevel < 1 || currentLevel >= maxLevel)
            return false;

        // * Dùng decimal cho phép nhân chi phí để hạn chế sai số nhị phân làm ceil(120) thành 121.
        decimal value = initialUpgradeCost;
        decimal multiplier = (decimal)upgradeCostMultiplier;
        for (int i = 1; i < currentLevel; i++)
            value *= multiplier;

        cost = checked((long)decimal.Ceiling(value));
        return true;
    }

    #endregion

    #region Cumulative Stat Growth

    // * Tổng bonus = bonus mỗi cấp × (level - 1); tính lại từ template, không cộng dồn lên kết quả cũ.
    // ! Save cao hơn trần hiện tại chỉ nhận bonus đến MaxLevel; không tự sửa level đã lưu.
    public StatBonus GetTotalBonus(int level)
    {
        ValidateConfiguration();
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level));
        return bonusPerLevel * (Mathf.Min(level, maxLevel) - 1);
    }

    #endregion

    #region Configuration Validation

    // * Chặn cấu hình cấp/chi phí không hợp lệ trước khi tính; không tự sửa asset trong runtime.
    private void ValidateConfiguration()
    {
        bonusPerLevel.Validate();
        if (maxLevel < 1 || initialUpgradeCost < 1 ||
            double.IsNaN(upgradeCostMultiplier) || double.IsInfinity(upgradeCostMultiplier) ||
            upgradeCostMultiplier < 1d)
            throw new InvalidOperationException("Character level config requires MaxLevel >= 1, cost >= 1 and a finite multiplier >= 1.");
    }

    #endregion
}
