using System;
using UnityEngine;

// * Dữ liệu riêng của một nhân vật đang chơi; chỉ sao chép giá trị, không sửa template SO.
public sealed class CharacterRuntimeData
{
    #region Character Identity

    public CharacterType CharacterType { get; }  // * Type được sao chép từ template khi tạo nhân vật.

    public string DisplayName { get; }  // * Tên được sao chép từ template khi tạo nhân vật.

    #endregion

    #region Character Stats & Health

    public BaseStats BaseStats { get; }  // * Chỉ số gốc sao chép từ CharacterDataSO; giữ làm mốc để tính lại bonus.

    public BaseStats FinalStats { get; private set; }  // * Chỉ số thực tế sau khi cộng bonus; Player đọc tốc độ di chuyển từ đây.

    public float CurrentHealth { get; private set; }  // * HP hiện tại, tách riêng khỏi maxHealth trong bộ chỉ số.

    public bool IsAlive => CurrentHealth > 0f;  // * Nhân vật còn sống khi HP lớn hơn 0.

    #endregion

    #region Levels & Stat Bonuses

    public int PermanentLevel { get; private set; }  // * Level vĩnh viễn lấy từ hồ sơ đã load, giữ lại giữa các lượt chơi.

    public int RunLevel { get; private set; }  // * Level trong lượt hiện tại; StartNewRun đưa level này về 1.

    public StatBonus PermanentBonus { get; private set; }  // * Tổng chỉ số cộng thêm từ level vĩnh viễn: bonus mỗi cấp × (PermanentLevel - 1).

    public StatBonus RunBonus { get; private set; }  // * Tổng chỉ số cộng thêm từ level trong trận: bonus mỗi cấp × (RunLevel - 1).

    public StatBonus EquipmentBonus { get; private set; }  // * Tổng bonus của trang bị đang mặc; bằng 0 khi chưa mặc trang bị.

    #endregion

    #region Runtime Notifications

    public event Action Changed;  // * Đọc FinalStats/CurrentHealth sau thông báo để UI lấy trạng thái mới nhất.

    #endregion

    #region Runtime Initialization

    // * Tạo dữ liệu riêng từ template và các nguồn bonus; nhân vật mới bắt đầu đầy HP.
    public CharacterRuntimeData(
        CharacterDataSO template,
        int permanentLevel,
        int runLevel,
        StatBonus permanentBonus,
        StatBonus runBonus,
        StatBonus equipmentBonus)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        CharacterType = template.CharacterType;
        DisplayName = template.DisplayName;
        BaseStats = template.BaseStats;

        Recalculate(permanentLevel, runLevel, permanentBonus, runBonus, equipmentBonus);

        CurrentHealth = FinalStats.maxHealth;
    }

    #endregion

    #region Stat Recalculation

    // * Luôn tính lại từ gốc, không cộng bonus lên kết quả cũ hoặc tự hồi HP khi tăng MaxHealth.
    internal void Recalculate(
        int permanentLevel,
        int runLevel,
        StatBonus permanentBonus,
        StatBonus runBonus,
        StatBonus equipmentBonus)
    {
        if (permanentLevel < 1 || runLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(permanentLevel), "Levels must be at least 1.");
        }

        PermanentLevel = permanentLevel;
        RunLevel = runLevel;

        PermanentBonus = permanentBonus;
        RunBonus = runBonus;
        EquipmentBonus = equipmentBonus;

        // * Cộng các nguồn bonus rồi áp dụng một lần lên bản chỉ số gốc.
        FinalStats = (permanentBonus + runBonus + equipmentBonus).ApplyTo(BaseStats);

        // * Giữ HP hiện tại; nếu MaxHealth giảm thì HP không được vượt giới hạn mới.
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, FinalStats.maxHealth);

        Changed?.Invoke();
    }

    #endregion

    #region Health Changes

    // * Số âm trừ HP, số dương hồi HP; công thức sát thương sẽ thuộc combat service.
    public void ApplyHealthChange(float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        float health = Mathf.Clamp(CurrentHealth + amount, 0f, FinalStats.maxHealth);

        if (health == CurrentHealth)
        {
            return;
        }

        CurrentHealth = health;

        Changed?.Invoke();
    }

    #endregion
}
