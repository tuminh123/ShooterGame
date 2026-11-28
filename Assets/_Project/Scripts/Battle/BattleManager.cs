using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Battle: sở hữu và điều phối một lượt chơi — đồng hồ, thống kê, level/EXP trong trận và loot.
/// Đây là class <b>duy nhất</b> của Battle được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// BattleManager
/// ├── BattleTimer         đồng hồ gameplay của lượt
/// ├── BattleStatistics    vàng nhặt được, số quái bị hạ
/// ├── InBattleLevel         level/EXP trận + nguồn bonus chỉ số trên Player
/// ├── LootSystem       rơi/nhặt EXP và vàng
/// └── SkillChoice      lượt chọn skill khi lên cấp (roll + áp dụng qua Combat)
/// </code>
/// Thay cho RunSessionManager, RunLevelService, LootManager và phần "run" của LevelManager cũ.
/// Các system con được tạo bằng <c>new</c> trong constructor và không gọi nhau; manager nối dây:
/// quái chết → thống kê + rơi loot; nhặt EXP → InBattleLevel; nhặt vàng → ví + thống kê; lên cấp → SkillChoice.
/// Chiều phụ thuộc: Battle → Combat / Character (gọi xuống trực tiếp). Miền khác chỉ thấy <c>BattleContracts.cs</c>.
/// </remarks>
public sealed class BattleManager : DomainManager, IBattleProgress, IBattleReset, ISkillChoice, ITickable, ILateTickable, IDisposable
{
    #region Fields

    private readonly BattleTimer clock;
    private readonly BattleStatistics statistics;
    private readonly InBattleLevel level;
    private readonly LootSystem loot;
    private readonly SkillChoice choices;
    private readonly IPlayerReader players;
    private readonly ICharacterDeathEvents characterDeaths;
    private readonly ICombatLifecycle combat; // Dọn skill gameplay khi reset lượt (trước đây SkillSystem tự nghe BattleReset).
    private readonly IWallet wallet; // Ví vĩnh viễn (miền Progression): vàng được lưu ngay khi nhặt.
    private readonly IAudio audio; // Âm + rung khi lên cấp: Battle sở hữu sự kiện này nên tự phát.
    private bool resetting; // Chặn ResetBattle lồng nhau từ callback của chính lần reset.
    private bool disposed;
    private float magnetTimer; // Đếm tới lần hút toàn bộ EXP kế tiếp (Magnet Stone Lv5).

    /// <summary>Hệ số tầm nhặt đủ lớn để hút mọi loot trên màn (lần hút theo nhịp).</summary>
    private const float MagnetPulseRadiusMultiplier = 1000f;

    #endregion

    #region Properties / Events (IBattleProgress)

    public int Level => level.Level;
    public int Experience => level.Experience;
    public int ExperienceToNextLevel => level.ExperienceToNextLevel;
    public int Gold => statistics.Gold;
    public int ElapsedSeconds => clock.ElapsedSeconds;
    public int EnemiesDefeated => statistics.EnemiesDefeated;

    // Event chuyển tiếp: đăng ký vào event của Manager thực chất là đăng ký thẳng vào system con
    // (add/remove), Manager không giữ danh sách listener riêng.
    /// <inheritdoc/>
    public event Action<int, int> LevelUp
    {
        add => level.LevelUp += value;
        remove => level.LevelUp -= value;
    }

    /// <inheritdoc/>
    public event Action<int, int> ExperienceChanged
    {
        add => level.ExperienceChanged += value;
        remove => level.ExperienceChanged -= value;
    }

    public event Action<int> GoldChanged
    {
        add => statistics.GoldChanged += value;
        remove => statistics.GoldChanged -= value;
    }

    public event Action<int> TimeChanged
    {
        add => clock.TimeChanged += value;
        remove => clock.TimeChanged -= value;
    }

    public event Action<int> EnemiesDefeatedChanged
    {
        add => statistics.EnemiesDefeatedChanged += value;
        remove => statistics.EnemiesDefeatedChanged -= value;
    }

    /// <inheritdoc/>
    public event Action BattleReset;

    #endregion

    #region Constructor / Lifecycle

    /// <summary>Dựng miền Battle và nối dây giữa các system con. Không phát event nào trong constructor.</summary>
    /// <param name="levelDatabase">Lấy <see cref="LevelDatabaseSO.InBattleLevelConfig"/> (cấu hình level trận dùng chung).</param>
    public BattleManager(
        LevelDatabaseSO levelDatabase,
        LootDatabaseSO lootDatabase,
        IGameObjectPool pools,
        IPlayerReader playerReader, // Hai vai trò Character đều do CharacterManager implement (CharacterContracts.cs).
        ICharacterDeathEvents characterDeaths,
        ICombatLifecycle combatLifecycle,
        ISkillGrant skillGrant, // Ba vai trò Combat đều do CombatManager implement (CombatContracts.cs).
        IOwnedSkillReader ownedSkills,
        SkillDatabaseSO skillDatabase,
        IWallet wallet,
        IAudio audio)
    {
        if (levelDatabase == null) throw new ArgumentNullException(nameof(levelDatabase));
        if (skillDatabase == null) throw new ArgumentNullException(nameof(skillDatabase));
        players = playerReader ?? throw new ArgumentNullException(nameof(playerReader));
        this.characterDeaths = characterDeaths ?? throw new ArgumentNullException(nameof(characterDeaths));
        combat = combatLifecycle ?? throw new ArgumentNullException(nameof(combatLifecycle));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        this.audio = audio ?? throw new ArgumentNullException(nameof(audio));

        clock = new BattleTimer();
        statistics = new BattleStatistics();
        loot = new LootSystem(lootDatabase, pools);
        try
        {
            // InBattleLevel và SkillChoice đăng ký PlayerChanged ngay khi tạo; nếu bước sau lỗi thì phải gỡ lại.
            level = new InBattleLevel(levelDatabase.InBattleLevelConfig, playerReader);
            choices = new SkillChoice(skillDatabase.CreateBattleChoiceCatalog(), skillGrant, ownedSkills, playerReader);
        }
        catch
        {
            loot.Dispose();
            level?.Dispose();
            throw;
        }

        loot.Collected += OnLootCollected;
        // Đăng ký trong constructor, trước mọi UI: khi UI nhận LevelUp thì số lượt chờ đã được cộng.
        level.LevelUp += choices.AddLevels;
        level.LevelUp += OnLevelUp;
        characterDeaths.CharacterDied += OnCharacterDied;
    }

    /// <summary>Đồng hồ chạy ở đầu frame (trước Combat), chỉ khi Player đang sống.</summary>
    public override void Tick()
    {
        if (disposed || !IsPlayerActive(out _)) return;
        clock.Tick(Time.deltaTime);
    }

    /// <summary>Nhặt loot sau khi Simulation đã áp vị trí mới (LateTick sau SimulationManager).</summary>
    public override void LateTick()
    {
        if (disposed || !IsPlayerActive(out CharacterView player)) return;
        // Tầm nhặt là bonus chiến đấu của Player (Magnet Stone…): Battle tự đọc, Combat không biết gì về loot.
        CombatBonuses bonuses = player.RuntimeData.FinalBonuses;
        loot.Collect(player.transform.position, 1f + bonuses.pickupRange / 100f);
        // Hút toàn bộ theo nhịp: bonus là số giây giữa hai lần hút; 0 = không có.
        if (bonuses.magnetPulse <= 0f) { magnetTimer = 0f; return; }
        magnetTimer += UnityEngine.Time.deltaTime;
        if (magnetTimer < bonuses.magnetPulse) return;
        magnetTimer = 0f;
        loot.Collect(player.transform.position, MagnetPulseRadiusMultiplier);
    }

    /// <summary>
    /// Thứ tự: gỡ listener miền khác → loot (trả pickup về pool, Pool dispose sau) → level (gỡ PlayerChanged) →
    /// listener của các system.
    /// </summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        characterDeaths.CharacterDied -= OnCharacterDied;
        loot.Collected -= OnLootCollected;
        level.LevelUp -= choices.AddLevels;
        level.LevelUp -= OnLevelUp;
        DisposeSafely(choices);
        DisposeSafely(loot);
        DisposeSafely(level);
        clock.ClearListeners();
        statistics.ClearListeners();
        BattleReset = null;
    }

    #endregion

    #region IBattleReset

    /// <inheritdoc/>
    public void ResetBattle()
    {
        if (disposed) throw new ObjectDisposedException(nameof(BattleManager));
        if (resetting) throw new InvalidOperationException("Không được reset lượt chơi từ trong callback reset.");
        resetting = true;
        try
        {
            // Skill gameplay dọn trước: chúng có thể giữ modifier chỉ số/effect trên Player.
            // Lỗi dọn skill không được để phần còn lại của Battle kẹt ở lượt cũ (finally vẫn reset).
            combat.ClearBattleSkills();
        }
        finally
        {
            try
            {
                choices.Cancel(); // Lượt chọn skill của trận cũ không được mang sang trận mới.
                loot.Clear();
                clock.Reset();
                statistics.Reset();
                level.Reset();
                BattleReset?.Invoke(); // Sau cùng: bên nghe (bảng chọn skill…) đọc được giá trị đầu lượt.
            }
            finally { resetting = false; }
        }
    }

    #endregion

    #region ISkillChoice

    public int PendingChoices => choices.Pending;
    public bool CanChoose => choices.CanChoose;
    public CharacterView ChoiceOwner => choices.Owner;

    /// <inheritdoc/>
    public event Action<int, int> PendingChanged
    {
        add => choices.PendingChanged += value;
        remove => choices.PendingChanged -= value;
    }

    /// <inheritdoc/>
    public event Action ChoicesCanceled
    {
        add => choices.Canceled += value;
        remove => choices.Canceled -= value;
    }

    /// <inheritdoc/>
    public void Roll(List<SkillChoiceOffer> result, bool reroll) => choices.Roll(result, reroll);

    /// <inheritdoc/>
    public bool Choose(SkillChoiceOffer offer) => choices.Choose(offer);

    /// <inheritdoc/>
    public void CancelChoices() => choices.Cancel();

    /// <inheritdoc/>
    public void RetryFailedChoices() => choices.RetryFailed();

    #endregion

    #region Wiring Between Systems

    // CharacterDied chỉ phát khi chết thật; despawn khi dọn trận không tính và không rơi loot.
    private void OnCharacterDied(CharacterView character)
    {
        if (disposed || character == null || character.RuntimeData == null || character.RuntimeData.Team != CharacterTeam.Enemy) return;
        statistics.AddDefeat();
        loot.DropAt(character.transform.position); // Chốt vị trí ngay, trước khi quái được trả về pool.
    }

    // Nhận nhiều cấp trong một lần nhặt vẫn chỉ kêu một lần (một event cho cả bước nhảy cấp).
    private void OnLevelUp(int previousLevel, int newLevel)
    {
        audio.Play(AudioId.LevelUp);
        audio.Play(AudioId.HapticMedium);
    }

    private void OnLootCollected(PickupType type, int amount)
    {
        switch (type)
        {
            case PickupType.Experience:
                level.AddExperience(amount);
                break;
            case PickupType.Gold:
                // Add đã cập nhật ví và thử lưu; IO lỗi giữ snapshot để retry, không cộng thưởng lần nữa.
                wallet.Add(CurrencyType.Gold, amount);
                statistics.AddGold(amount); // Chỉ thống kê, không quyết toán lại cuối trận.
                break;
            default:
                throw new InvalidOperationException("Battle chưa hỗ trợ loại loot: " + type);
        }
    }

    #endregion

    #region Private Helpers

    // Đồng hồ và nhặt loot chỉ chạy khi có Player còn sống và không pause (deltaTime = 0 khi timeScale = 0).
    private bool IsPlayerActive(out CharacterView player)
    {
        player = players.CurrentPlayer;
        return Time.deltaTime > 0f && player != null && player.isActiveAndEnabled &&
               player.RuntimeData != null && player.RuntimeData.IsAlive;
    }

    // System có thể null nếu constructor lỗi giữa chừng.
    private static void DisposeSafely(IDisposable system)
    {
        if (system == null) return;
        try { system.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    #endregion
}
