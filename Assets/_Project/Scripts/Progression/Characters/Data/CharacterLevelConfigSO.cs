using System;
using System.Collections.Generic;
using UnityEngine;

// Bonus theo cấp dùng chung cho player và enemy. Amount Per Upgrade chỉ dùng cho player; Gold cần cho mỗi mốc nằm trong LevelDatabaseSO.
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

    #region Upgrade Per Click

    // Gold cần cho mỗi mốc level KHÔNG nằm ở đây: công thức chung theo độ hiếm nằm trong LevelDatabaseSO (Permanent Level Cost),
    // để mọi nhân vật cùng độ hiếm có cùng bảng giá. Config này chỉ giữ cảm giác bấm riêng của nhân vật.
    [Tooltip("Số lần bấm Upgrade để đầy thanh một mốc. Giá mỗi lần bấm = Gold cần cho mốc ÷ số này (làm tròn lên), " +
             "nên giá trên nút tăng theo level và độ hiếm. Ví dụ mốc cần 200, 4 lần → mỗi lần 50 Gold.")]
    [SerializeField, Min(1)] private int clicksPerLevel = 4; // Mọi mốc bấm cùng số lần: level cao hay nhân vật hiếm không bắt bấm nhiều hơn, chỉ đắt hơn.

    /// <summary>Số lần bấm Upgrade để đầy thanh một mốc.</summary>
    public int ClicksPerLevel => clicksPerLevel;

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

    #region Stars

    /// <summary>
    /// Số sao tối đa của mọi nhân vật. Phần thưởng của từng sao nằm trong <see cref="CharacterDataSO"/>:
    /// các bước tiến trình skill (điều kiện Stars), hoặc chỉ số dự phòng (Star Stats) khi sao không có bước skill nào.
    /// </summary>
    public const int MaxStars = 5;

    #endregion

    #region Configuration Validation

    // Chặn cấu hình cấp/EXP nâng cấp không hợp lệ trước khi tính; không tự sửa asset trong runtime.
    private void ValidateConfiguration()
    {
        bonusPerLevel.Validate();
        if (maxLevel < 1 || clicksPerLevel < 1)
            throw new InvalidOperationException("Character level config requires MaxLevel >= 1 and clicks per level >= 1.");
    }

    #endregion
}
