using System;
using System.Collections.Generic;
using UnityEngine;

// Trạng thái của một lượt spawn; max lấy từ chỉ số, current được tính bằng max trừ lượng đã mất.
public sealed class CharacterRuntimeData
{
    #region Runtime State

    private readonly Dictionary<long, StatModifier[]> modifiers = new Dictionary<long, StatModifier[]>(8); // Các nguồn modifier chung, mỗi nguồn có token riêng.
    private long nextModifierId; // Không dùng lại ID trong cùng một lượt spawn.
    private StatBonus externalBonus; // Tổng bonus level/trang bị do service cung cấp.
    private StatBonus receivedBonus; // HP/giáp được cấp trực tiếp trong lượt spawn, giữ khi tính lại stats.
    private float lostHealth; // HP đã mất; bonus tăng max không xóa lượng damage này.
    private float lostShield; // Giáp đã mất; max 100, mất 70 thì current là 30.
    private bool defeated; // Đã chết thì thay bonus/cleanup không được tự hồi sinh.

    #endregion

    #region Public State And Events

    public CharacterType CharacterType { get; private set; }
    public CharacterTeam Team { get; private set; }
    public string DisplayName { get; private set; }
    public BaseStats BaseStats { get; private set; }
    public ulong Generation { get; private set; } // Phân biệt các lượt pool dùng lại cùng runtime.
    public BaseStats FinalStats { get; private set; }
    public float MaxHealth => FinalStats.maxHealth;
    public float MaxShield => FinalStats.defense;
    public float CurrentHealth => defeated ? 0f : Mathf.Max(0f, MaxHealth - lostHealth);
    public float CurrentShield => Mathf.Max(0f, MaxShield - lostShield);
    public bool IsAlive => CurrentHealth > 0f;
    public event Action Changed; // Phát sau khi toàn bộ trạng thái đã nhất quán để UI đọc current/max mới.

    #endregion

    #region Spawn And Pool Lifecycle

    // Khởi tạo trực tiếp từ template; không thay đổi asset gốc.
    public CharacterRuntimeData(CharacterDataSO template, StatBonus bonus = default)
        => InitializeSpawn(template, bonus);

    // Pool có thể tạo runtime rảnh trước khi có dữ liệu nhân vật.
    internal CharacterRuntimeData() { }

    // Mỗi lượt spawn bắt đầu đầy HP/giáp, không mang damage hoặc bonus nhận trực tiếp từ lượt cũ.
    internal void InitializeSpawn(CharacterDataSO template, StatBonus bonus)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        template.ValidateStats();
        ResetForPool();
        Generation = checked(Generation + 1);
        CharacterType = template.CharacterType;
        Team = template.Team;
        DisplayName = template.DisplayName;
        BaseStats = template.BaseStats;
        externalBonus = bonus;
        RebuildFinalStats();
        NotifyChanged();
    }

    // Chỉ dùng khi dựng lại skill ban đầu; nhận skill giữa trận không gọi hàm refill này.
    internal void RefillSpawnVitals()
    {
        if (lostHealth == 0f && lostShield == 0f && !defeated) return;
        lostHealth = lostShield = 0f;
        defeated = false;
        NotifyChanged();
    }

    // View phải unbind trước khi trả pool; xóa listener để không cập nhật UI của lượt spawn cũ.
    internal void ResetForPool()
    {
        Changed = null;
        modifiers.Clear();
        nextModifierId = 0;
        externalBonus = receivedBonus = default;
        lostHealth = lostShield = 0f;
        defeated = false;
        FinalStats = default;
        BaseStats = default;
        DisplayName = null;
        CharacterType = default;
        Team = default;
    }

    #endregion

    #region Stat Modifiers

    // Service level/trang bị cung cấp bonus cộng trực tiếp; modifier phần trăm áp dụng sau bộ bonus này.
    internal void SetExternalBonus(StatBonus bonus)
    {
        bonus.Validate();
        StatBonus previous = externalBonus;
        externalBonus = bonus;
        try { RebuildFinalStats(); }
        catch { externalBonus = previous; throw; }
        NotifyChanged();
    }

    // Điểm vào đơn giản cho một stat: buff, debuff và slow đều dùng chung kiểu dữ liệu.
    public StatModifierLease AddModifier(StatModifier modifier) => AddModifiers(modifier);

    // Một nguồn có thể đổi nhiều stat trong cùng thao tác, phát Changed một lần sau khi tính xong.
    public StatModifierLease AddModifiers(params StatModifier[] values)
    {
        StatModifier[] snapshot = SnapshotModifiers(values);
        ulong generation = Generation;
        long id = checked(++nextModifierId);
        modifiers.Add(id, snapshot);
        try { RebuildFinalStats(); }
        catch { modifiers.Remove(id); throw; }
        NotifyChanged();
        return new StatModifierLease(
            () => RemoveModifiers(id, generation),
            next => UpdateModifiers(id, generation, next));
    }

    // Caller không được thay đổi mảng config đang được runtime sử dụng.
    private static StatModifier[] SnapshotModifiers(StatModifier[] values)
    {
        if (values == null || values.Length == 0) throw new ArgumentException("A modifier source must contain at least one stat.");
        var snapshot = (StatModifier[])values.Clone();
        foreach (StatModifier modifier in snapshot) modifier.Validate();
        return snapshot;
    }

    // Thay toàn bộ modifier của cùng nguồn tại chỗ; không có trạng thái mất buff ở giữa.
    private void UpdateModifiers(long id, ulong generation, StatModifier[] values)
    {
        if (Generation != generation || !modifiers.TryGetValue(id, out StatModifier[] previous)) return;
        StatModifier[] snapshot = SnapshotModifiers(values);
        modifiers[id] = snapshot;
        try { RebuildFinalStats(); }
        catch { modifiers[id] = previous; throw; }
        NotifyChanged();
    }

    // Token cũ không được gỡ nguồn của spawn mới đã dùng lại cùng runtime.
    private void RemoveModifiers(long id, ulong generation)
    {
        if (Generation != generation || !modifiers.TryGetValue(id, out StatModifier[] previous)) return;
        modifiers.Remove(id);
        try { RebuildFinalStats(); }
        catch { modifiers.Add(id, previous); throw; }
        NotifyChanged();
    }

    #endregion

    #region Stat Calculation

    // Calculator áp dụng cùng công thức cho mọi stat; runtime chỉ quản lý tài nguyên đã mất và thông báo.
    private void RebuildFinalStats()
    {
        BaseStats stats = StatCalculator.Calculate(BaseStats, externalBonus, modifiers.Values);
        // HP/giáp cấp trực tiếp là lượng đã chốt, không bị nhân lại mỗi lần tính buff/debuff.
        stats.maxHealth += receivedBonus.maxHealth;
        stats.defense += receivedBonus.defense;
        stats.Validate();
        FinalStats = stats;
        if (lostHealth >= stats.maxHealth) defeated = true;
        // Không để giáp âm ẩn nuốt mất những lần được cấp giáp tiếp theo.
        lostHealth = Mathf.Min(lostHealth, stats.maxHealth);
        lostShield = Mathf.Min(lostShield, stats.defense);
    }

    // Kiểm tra float cho các thao tác tài nguyên và damage.
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion

    #region HP And Shield Mutation

    // Cấp giáp tăng max và current cùng lượng; không mở thêm một capacity riêng.
    internal void AddShield(float amount)
    {
        if (!Finite(amount) || amount < 0f) throw new ArgumentOutOfRangeException(nameof(amount));
        SetVitals(CurrentHealth, CurrentShield + amount);
    }

    // Damage tăng lượng đã mất; mọi phần cộng thêm tăng max tương ứng, không reset damage cũ.
    internal void SetVitals(float health, float shield)
    {
        if (!Finite(health) || !Finite(shield)) throw new ArgumentOutOfRangeException(nameof(health));
        if (defeated) return;
        health = Mathf.Max(0f, health);
        shield = Mathf.Max(0f, shield);
        float healthAdded = Mathf.Max(0f, health - CurrentHealth);
        float shieldAdded = Mathf.Max(0f, shield - CurrentShield);
        if (health == CurrentHealth && shield == CurrentShield) return;

        if (healthAdded > 0f || shieldAdded > 0f)
        {
            StatBonus previous = receivedBonus;
            receivedBonus.maxHealth += healthAdded;
            receivedBonus.defense += shieldAdded;
            try { RebuildFinalStats(); }
            catch { receivedBonus = previous; throw; }
        }
        lostHealth = Mathf.Max(0f, MaxHealth - health);
        lostShield = Mathf.Max(0f, MaxShield - shield);
        if (health <= 0f) defeated = true;
        NotifyChanged();
    }

    #endregion

    #region Change Notifications

    // Runtime không biết UI cụ thể; một listener lỗi không chặn những UI/listener còn lại.
    private void NotifyChanged()
    {
        Action listeners = Changed;
        if (listeners == null) return;
        ulong generation = Generation;
        foreach (Action listener in listeners.GetInvocationList())
        {
            if (generation != Generation) break; // Callback vừa trả pool/spawn lại thì dừng thông báo của lượt cũ.
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion

    #region Damage Calculation

    // Tính damage từ final stats hiện tại; không thay đổi trạng thái HP/giáp.
    public DamageInfo CreateDamage(float multiplier = 1f)
        => CreateDamage(FinalStats, Team, multiplier);

    // Volley chụp stats một lần, từng viên vẫn roll critical riêng.
    internal static DamageInfo CreateDamage(BaseStats stats, CharacterTeam team, float multiplier = 1f)
    {
        if (!Finite(multiplier) || multiplier < 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
        float chance = Mathf.Min(stats.critChance, 100f);
        float critBonus = stats.critDamageMultiplier + Mathf.Max(0f, stats.critChance - 100f) * 0.25f;
        bool critical = chance >= 100f || (chance > 0f && UnityEngine.Random.value * 100f < chance);
        float amount = stats.damage * multiplier * (critical ? 1f + critBonus / 100f : 1f);
        return new DamageInfo(amount, team, critical);
    }

    #endregion
}
