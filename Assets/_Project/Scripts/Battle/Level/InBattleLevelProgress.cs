using System;

/// <summary>Tiến trình level class thuần của một lượt chơi; mỗi nhân vật/lượt chơi tạo instance riêng, không đưa vào PlayerSaveData.</summary>
public sealed class InBattleLevelProgress
{
    #region Battle State

    private readonly InBattleLevelConfigSO config; // Chỉ đọc công thức EXP và bonus cố định; không sửa asset dùng chung.

    /// <summary>Level trong trận, bắt đầu từ 1.</summary>
    public int Level { get; private set; } = 1;
    /// <summary>EXP dư của cấp hiện tại, không phải tổng EXP đã nhặt.</summary>
    public int Experience { get; private set; }
    /// <summary>Tổng thưởng cố định của các level đã đạt trong lượt này.</summary>
    public StatBonus TotalBonus => config.GetTotalBonus(Level);
    /// <summary>Đạt cấp cuối thì bỏ EXP dư.</summary>
    public bool IsMaxLevel => Level >= config.MaxLevel;

    #endregion

    #region Initialization & Experience

    /// <summary>Nhận config từ bên gọi; không bind singleton vì mỗi lượt chơi cần trạng thái riêng.</summary>
    public InBattleLevelProgress(InBattleLevelConfigSO config)
    {
        this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Thêm EXP, hỗ trợ lên nhiều cấp một lần; trả số cấp đã tăng để bên ngoài mở lựa chọn thưởng tương ứng.</summary>
    public int AddExperience(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (IsMaxLevel)
            return 0;

        long remaining = (long)Experience + amount;
        int nextLevel = Level;
        int gainedLevels = 0;
        while (config.TryGetNextLevel(nextLevel, out int required, out _))
        {
            if (remaining < required)
                break;
            remaining -= required;
            nextLevel++;
            gainedLevels++;
        }

        Level = nextLevel;
        Experience = IsMaxLevel ? 0 : (int)remaining;
        return gainedLevels;
    }

    /// <summary>Gọi khi bắt đầu lượt mới; không thay đổi level vĩnh viễn hoặc dữ liệu save.</summary>
    public void Reset()
    {
        Level = 1;
        Experience = 0;
    }

    #endregion
}
