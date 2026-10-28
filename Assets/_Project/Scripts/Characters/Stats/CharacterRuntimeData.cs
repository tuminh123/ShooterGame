using System;
using System.Collections.Generic;
using UnityEngine;

// * Dữ liệu riêng của một nhân vật đang chơi; chỉ sao chép giá trị, không sửa template SO.
public sealed class CharacterRuntimeData
{
    private readonly Dictionary<long, StatBonus> temporaryBonuses = new Dictionary<long, StatBonus>();
    private readonly Dictionary<long, float> speedMultipliers = new Dictionary<long, float>();
    private long nextModifierId;
    #region Character Identity

    public CharacterType CharacterType { get; }  // * Type được sao chép từ template khi tạo nhân vật.
    public CharacterTeam Team { get; } // * Phe riêng của cá thể, sao chép từ template; boss thuộc Enemy.

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
    public StatBonus SpawnBonus { get; } // * Bonus chốt tại lúc spawn; quái đang sống không nhận các mốc tăng sức mạnh tiếp theo.

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
        StatBonus equipmentBonus,
        StatBonus spawnBonus = default)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        CharacterType = template.CharacterType;
        Team = template.Team;
        DisplayName = template.DisplayName;
        BaseStats = template.BaseStats;
        SpawnBonus = spawnBonus;

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
        RebuildFinalStats();

        // * Giữ HP hiện tại; nếu MaxHealth giảm thì HP không được vượt giới hạn mới.
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, FinalStats.maxHealth);

        Changed?.Invoke();
    }

    #endregion

    public IDisposable AddTemporaryBonus(StatBonus bonus)
    {
        long id = checked(++nextModifierId);
        temporaryBonuses.Add(id, bonus);
        RefreshModifiers();
        return new StatModifierLease(() => { if (temporaryBonuses.Remove(id)) RefreshModifiers(); });
    }

    public IDisposable AddSpeedMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f || multiplier > 1f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        long id = checked(++nextModifierId);
        speedMultipliers.Add(id, multiplier);
        RefreshModifiers();
        return new StatModifierLease(() => { if (speedMultipliers.Remove(id)) RefreshModifiers(); });
    }

    private void RebuildFinalStats()
    {
        StatBonus total = PermanentBonus + RunBonus + EquipmentBonus + SpawnBonus;
        foreach (StatBonus bonus in temporaryBonuses.Values) total += bonus;
        BaseStats stats = total.ApplyTo(BaseStats);
        float speedMultiplier = 1f;
        // Strongest slow wins; independent lifetimes restore weaker effects when it expires.
        foreach (float multiplier in speedMultipliers.Values)
            speedMultiplier = Mathf.Min(speedMultiplier, multiplier);
        stats.moveSpeed *= speedMultiplier;
        FinalStats = stats;
    }

    private void RefreshModifiers()
    {
        RebuildFinalStats();
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, FinalStats.maxHealth);
        Changed?.Invoke();
    }

    #region Health Changes

    // * Character tính đầu ra trước khi bắn; crit theo bậc cộng dồn trong tài liệu chỉ số.
    public DamageInfo CreateDamage(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        float chance = Mathf.Max(0f, FinalStats.critChance);
        int tier = Mathf.FloorToInt(chance);
        if (UnityEngine.Random.value < chance - tier)
            tier++;
        float critMultiplier = 1f + tier * (Mathf.Max(1f, FinalStats.critDamageMultiplier) - 1f);
        return new DamageInfo(Mathf.Max(0f, FinalStats.damage) * multiplier * critMultiplier, Team, tier);
    }

    // * Luật nhận damage thuộc Character; nguồn đánh không biết defense hoặc HP của mục tiêu.
    public DamageResult ReceiveDamage(DamageInfo damage)
    {
        if (!IsAlive || damage.SourceTeam == Team || damage.Amount <= 0f)
            return default;
        float before = CurrentHealth;
        float received = damage.Amount * (100f / (100f + Mathf.Max(0f, FinalStats.defense)));
        ApplyHealthChange(-received);
        return new DamageResult(before - CurrentHealth, !IsAlive);
    }

    // * Số âm trừ HP, số dương hồi HP; hàm này chỉ thay đổi HP, không tự quyết định despawn.
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
