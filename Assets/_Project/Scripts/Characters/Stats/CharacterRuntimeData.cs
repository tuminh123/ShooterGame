using System;
using UnityEngine;

/// <summary>
/// Trạng thái của một lượt spawn nhân vật: chỉ số (qua <see cref="Stats"/>), HP và giáp, và thông báo thay đổi.
/// Được pool tái sử dụng; <see cref="Generation"/> tăng mỗi lượt spawn để phân biệt các lượt dùng cùng object.
/// </summary>
/// <remarks>
/// HP/giáp được lưu dưới dạng <b>lượng đã mất</b>: current = max − đã mất. Nhờ vậy bonus tăng max không xóa
/// sát thương đã nhận, và giảm max (mất buff) có thể làm nhân vật chết giống như nhận sát thương.
/// </remarks>
public sealed class CharacterRuntimeData
{
    #region Vitals State

    private StatBonus receivedBonus; // HP/giáp được cấp trực tiếp trong lượt spawn; cộng sau công thức, không bị nhân lại.
    private float lostHealth; // HP đã mất; bonus tăng max không xóa lượng damage này.
    private float lostShield; // Giáp đã mất; max 100, mất 70 thì current là 30.
    private bool defeated; // Đã chết thì đổi bonus hay dọn dẹp cũng không được tự hồi sinh.

    #endregion

    #region Public State And Events

    /// <summary>Bảng chỉ số: thêm/gỡ mọi nguồn bonus qua <see cref="StatSheet.AddSource"/>.</summary>
    public StatSheet Stats { get; }

    public CharacterType CharacterType { get; private set; }
    public CharacterTeam Team { get; private set; }
    public string DisplayName { get; private set; }

    /// <summary>Chỉ số gốc từ template của lượt spawn.</summary>
    public BaseStats BaseStats => Stats.Base;

    /// <summary>Tăng mỗi lượt spawn; phân biệt các lượt pool dùng lại cùng runtime.</summary>
    public ulong Generation { get; private set; }

    /// <summary>Chỉ số cuối cùng sau mọi nguồn bonus và HP/giáp được cấp trực tiếp.</summary>
    public BaseStats FinalStats { get; private set; }

    public float MaxHealth => FinalStats.maxHealth;
    public float MaxShield => FinalStats.defense;
    public float CurrentHealth => defeated ? 0f : Mathf.Max(0f, MaxHealth - lostHealth);
    public float CurrentShield => Mathf.Max(0f, MaxShield - lostShield);
    public bool IsAlive => CurrentHealth > 0f;

    /// <summary>Phát sau khi toàn bộ trạng thái đã nhất quán, để UI đọc current/max mới.</summary>
    public event Action Changed;

    #endregion

    #region Spawn And Pool Lifecycle

    /// <summary>Khởi tạo trực tiếp từ template (ngoài pool); không thay đổi asset gốc.</summary>
    public CharacterRuntimeData(CharacterDataSO template, StatBonus bonus = default) : this()
        => InitializeSpawn(template, bonus);

    /// <summary>Pool tạo runtime rảnh trước khi có dữ liệu nhân vật.</summary>
    internal CharacterRuntimeData() => Stats = new StatSheet(OnStatsChanged);

    /// <summary>
    /// Bắt đầu lượt spawn: đầy HP/giáp, không mang sát thương hay bonus nhận trực tiếp từ lượt cũ.
    /// <paramref name="bonus"/> là nguồn bonus lúc spawn (ví dụ chỉ số quái theo đợt), tồn tại suốt lượt spawn.
    /// </summary>
    internal void InitializeSpawn(CharacterDataSO template, StatBonus bonus)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        template.ValidateStats();
        ResetForPool();
        Generation = checked(Generation + 1);
        CharacterType = template.CharacterType;
        Team = template.Team;
        DisplayName = template.DisplayName;
        Stats.Reset(template.BaseStats, bonus);
        RebuildFinalStats();
        NotifyChanged();
    }

    /// <summary>Hồi đầy HP/giáp sau khi skill vốn có vừa áp chỉ số lúc spawn. Nhận skill giữa trận không gọi hàm này.</summary>
    internal void RefillSpawnVitals()
    {
        if (lostHealth == 0f && lostShield == 0f && !defeated) return;
        lostHealth = lostShield = 0f;
        defeated = false;
        NotifyChanged();
    }

    /// <summary>
    /// Dọn khi trả về pool. View phải unbind trước; listener bị xóa để không cập nhật UI của lượt spawn cũ,
    /// và mọi lease chỉ số cũ bị vô hiệu hóa.
    /// </summary>
    internal void ResetForPool()
    {
        Changed = null;
        Stats.Clear();
        receivedBonus = default;
        lostHealth = lostShield = 0f;
        defeated = false;
        FinalStats = default;
        DisplayName = null;
        CharacterType = default;
        Team = default;
    }

    #endregion

    #region Stat Calculation

    // StatSheet gọi khi một nguồn bonus được thêm/cập nhật/gỡ. Nếu RebuildFinalStats ném lỗi,
    // NotifyChanged không chạy và sheet tự hoàn tác thay đổi.
    private void OnStatsChanged()
    {
        RebuildFinalStats();
        NotifyChanged();
    }

    // Công thức nằm trong StatCalculator (qua StatSheet); runtime chỉ cộng HP/giáp được cấp trực tiếp và kẹp lượng đã mất.
    private void RebuildFinalStats()
    {
        BaseStats stats = Stats.Calculate();
        // HP/giáp cấp trực tiếp là lượng đã chốt, không bị nhân lại mỗi lần tính buff/debuff.
        stats.maxHealth += receivedBonus.maxHealth;
        stats.defense += receivedBonus.defense;
        stats.Validate();
        FinalStats = stats;
        if (lostHealth >= stats.maxHealth) defeated = true;
        // Không để lượng đã mất vượt max: nếu không, giáp âm ẩn sẽ nuốt mất những lần được cấp giáp tiếp theo.
        lostHealth = Mathf.Min(lostHealth, stats.maxHealth);
        lostShield = Mathf.Min(lostShield, stats.defense);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion

    #region HP And Shield Mutation

    /// <summary>Cấp giáp: tăng cả max và current cùng một lượng.</summary>
    internal void AddShield(float amount)
    {
        if (!Finite(amount) || amount < 0f) throw new ArgumentOutOfRangeException(nameof(amount));
        SetVitals(CurrentHealth, CurrentShield + amount);
    }

    /// <summary>
    /// Đặt HP/giáp hiện tại. Giảm thì tăng lượng đã mất; tăng vượt max thì phần vượt tăng max tương ứng
    /// (không reset sát thương cũ). Về 0 HP thì nhân vật chết.
    /// </summary>
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

    // Runtime không biết UI cụ thể; một listener lỗi không chặn các listener còn lại.
    private void NotifyChanged()
    {
        Action listeners = Changed;
        if (listeners == null) return;
        ulong generation = Generation;
        foreach (Action listener in listeners.GetInvocationList())
        {
            if (generation != Generation) break; // Listener vừa trả runtime về pool và spawn lại: dừng thông báo của lượt cũ.
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion

    #region Damage Calculation

    /// <summary>Tạo một đòn sát thương từ chỉ số cuối hiện tại; không thay đổi HP/giáp.</summary>
    public DamageInfo CreateDamage(float multiplier = 1f)
        => CreateDamage(FinalStats, Team, multiplier);

    /// <summary>
    /// Tạo một đòn từ chỉ số đã chụp (một loạt đạn chụp chỉ số một lần, mỗi viên vẫn tung chí mạng riêng).
    /// Phần tỉ lệ chí mạng vượt 100% được cộng vào sát thương chí mạng (mỗi 1% vượt = +0.25% sát thương chí mạng).
    /// </summary>
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
