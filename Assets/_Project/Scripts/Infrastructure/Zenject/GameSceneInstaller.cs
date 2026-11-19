using System;
using Terresquall;
using UnityEngine;
using Zenject;

/// <summary>
/// Composition root của scene: gán asset/scene reference, bind <b>một Manager cho mỗi miền</b> và khai báo
/// thứ tự Initialize/Tick/LateTick/Dispose ở một chỗ.
/// </summary>
/// <remarks>
/// Mỗi Manager tự tạo các system con bằng <c>new</c>; container chỉ biết Manager và các contract của nó
/// (<c>BindInterfacesTo</c> nên không lộ kiểu concrete). Thêm miền mới = thêm một dòng bind + thứ tự lifecycle nếu cần.
/// Trước Giai đoạn 6 class này là <c>partial</c> chia 3 file (Bindings/ExecutionOrder); giờ đã đủ gọn để gộp lại.
/// </remarks>
public sealed class GameSceneInstaller : MonoInstaller
{
    #region Scene References

    [SerializeField] private CharacterView characterPrefab; // * Một prefab vỏ duy nhất dùng cho cả Player, Enemy và boss.
    [SerializeField] private CombatAssetsSO combatAssets; // Vỏ đạn dùng chung, sprite đạn và prefab effect.
    [SerializeField] private PoolPrewarmSettingsSO poolPrewarmSettings;
    [SerializeField] private CharacterType defaultPlayerType = CharacterType.BlessedGladiator; // * Loại Player khi save chưa chọn nhân vật.
    [SerializeField] private VirtualJoystick joystick;  // * Joystick trên Canvas, truyền cho PlayerController.
    [SerializeField] private string playerSaveFile = GameConstants.SaveKeys.PlayerFile;  // * Tên file save người chơi; không chứa dữ liệu của lượt chơi.
    [Tooltip("Nền lặp theo Player và danh mục map; WorldManager dựng từ cấu hình này.")]
    [SerializeField] private WorldManager.Settings world = new WorldManager.Settings();
    [Tooltip("Vùng sinh quái và AI quái (tách đàn, phản lực); StageManager dựng EnemySpawner từ cấu hình này.")]
    [SerializeField] private EnemySpawner.Settings stage = new EnemySpawner.Settings();

    #endregion

    [Header("Gameplay Databases")]

    #region Gameplay Databases

    [SerializeField] private CharacterDatabaseSO characterDatabase;  // * Danh mục template nhân vật.
    [SerializeField] private LevelDatabaseSO levelDatabase;          // * Config level vĩnh viễn theo nhân vật và config level trận.
    [SerializeField] private EquipmentDatabaseSO equipmentDatabase;  // * Tra bonus từ ID trang bị đã lưu.
    [SerializeField] private LootDatabaseSO lootDatabase; // Catalog EXP/vàng: prefab, sprite, lượng, tỉ lệ.
    [SerializeField] private SkillDatabaseSO skillDatabase; // Catalog skill gameplay và quyền xuất hiện trong bảng thưởng.
    [Tooltip("Danh mục tất cả màn và màn mặc định; StageManager chọn Stage từ database này.")]
    [SerializeField] private StageDatabaseSO stageDatabase;

    #endregion

    #region Game Flow Settings

    [SerializeField] private bool autoStartRun = false; // Loading → Home; chỉ vào trận khi nút Play gọi StartRun.

    #endregion

    #region Fields

    private ISaveMaintenance saves; // Thử ghi lại save khi ứng dụng pause/mất focus/thoát.

    #endregion

    #region Installation

    /// <summary>Kiểm tra cấu hình → bind dữ liệu → bind Manager theo chiều phụ thuộc → thứ tự lifecycle.</summary>
    public override void InstallBindings()
    {
        ValidateDependencies();
        BindData();
        BindDomainManagers();
        BindSceneServices();
        ConfigureInitializationOrder();
        ConfigureFrameOrder();
        ConfigureDisposalOrder();
    }

    // Chặn thiếu reference/cấu hình trước khi tạo service, tránh container khởi tạo dở dang.
    private void ValidateDependencies()
    {
        if (characterPrefab == null || combatAssets == null || joystick == null)
            throw new InvalidOperationException("Assign Character prefab, Combat Assets and Joystick in GameSceneInstaller.");
        combatAssets.Validate();
        if (characterDatabase == null || levelDatabase == null)
            throw new InvalidOperationException("Assign Character Database and Level Database in GameSceneInstaller.");
        if (equipmentDatabase == null)
            throw new InvalidOperationException("Assign Equipment Database in GameSceneInstaller.");
        if (lootDatabase == null)
            throw new InvalidOperationException("Assign Loot Database in GameSceneInstaller.");
        if (skillDatabase == null)
            throw new InvalidOperationException("Assign Skill Database in GameSceneInstaller.");
        skillDatabase.Validate();
        if (poolPrewarmSettings == null)
            throw new InvalidOperationException("Assign Pool Prewarm Settings in GameSceneInstaller.");
        poolPrewarmSettings.Validate();
        if (stageDatabase == null)
            throw new InvalidOperationException("Assign Stage Database in GameSceneInstaller.");
        stageDatabase.Validate();
    }

    // Dùng đúng asset đã gán trong Inspector; không tạo bản sao hoặc giữ trạng thái trận trên DataSO.
    // Settings là dữ liệu cấu hình mà Manager nhận qua constructor.
    private void BindData()
    {
        Container.Bind<CharacterDatabaseSO>().FromInstance(characterDatabase).AsSingle();
        Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<EquipmentDatabaseSO>().FromInstance(equipmentDatabase).AsSingle();
        Container.Bind<LootDatabaseSO>().FromInstance(lootDatabase).AsSingle();
        Container.Bind<SkillDatabaseSO>().FromInstance(skillDatabase).AsSingle();
        Container.Bind<StageDatabaseSO>().FromInstance(stageDatabase).AsSingle();
        Container.BindInstance(combatAssets);
        Container.BindInstance(poolPrewarmSettings);
        Container.BindInstance(new SaveManager.Settings(playerSaveFile));
        Container.BindInstance(new PlayerSystem.Settings(defaultPlayerType));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
    }

    /// <summary>
    /// Một dòng cho mỗi miền, theo chiều phụ thuộc từ dưới lên (Save → … → Game). Mỗi Manager chỉ lộ contract
    /// trong <c>XxxContracts.cs</c> của nó; system con không có binding nào.
    /// </summary>
    private void BindDomainManagers()
    {
        Container.BindInterfacesTo<SaveManager>().AsSingle().NonLazy();          // Save: đọc/ghi + migration
        Container.BindInterfacesTo<ProgressionManager>().AsSingle().NonLazy();   // Progression: ví, level vĩnh viễn, trang bị, mốc vượt màn
        Container.BindInterfacesTo<PoolManager>().AsSingle().NonLazy();         // Pool: LeanPool + holder
        Container.BindInterfacesTo<SimulationManager>().AsSingle().NonLazy();   // Simulation: job di chuyển + lưới không gian
        Container.BindInterfacesTo<CharacterManager>().AsSingle().WithArguments(characterPrefab).NonLazy(); // Character
        Container.BindInterfacesTo<WorldManager>().AsSingle().WithArguments(world).NonLazy();               // World: map nền

        // Combat nhận hai MemoryPool của Zenject để dựng pool runtime effect (kèm số lượng tạo sẵn).
        Container.BindMemoryPool<SlowEffectRuntime, ClassPool<SlowEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.SlowRuntimes);
        Container.BindMemoryPool<DamageOverTimeEffectRuntime, ClassPool<DamageOverTimeEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.DamageOverTimeRuntimes);
        Container.BindInterfacesTo<CombatManager>().AsSingle().NonLazy();       // Combat: skill, đạn, effect, VFX

        Container.BindInterfacesTo<StageManager>().AsSingle().WithArguments(stage).NonLazy(); // Stage: wave, đội hình, boss
        Container.BindInterfacesTo<RunManager>().AsSingle().NonLazy();          // Run: đồng hồ, level trận, loot, chọn skill
        Container.BindInterfacesTo<GameManager>().AsSingle()
            .WithArguments(autoStartRun, characterPrefab).NonLazy();            // Game: luồng scene, pause, prewarm
    }

    // UI là MonoBehaviour trong scene và được SceneContext tự inject; chỉ dịch vụ UI dùng chung mới cần binding.
    private void BindSceneServices()
    {
        Container.BindInterfacesTo<ScreenTransitionController>().FromComponentInHierarchy().AsSingle(); // UIContracts.cs
    }

    #endregion

    #region Lifecycle Order

    // Initialize chạy số nhỏ trước: mở save + migration → đọc tiến trình → nhân vật đang chọn → map → bắt đầu game.
    // Run, Combat, Stage… không có Initialize: chúng đăng ký event ngay trong constructor.
    private void ConfigureInitializationOrder()
    {
        Container.BindInitializableExecutionOrder<SaveManager>(-200);
        Container.BindInitializableExecutionOrder<ProgressionManager>(-160);
        Container.BindInitializableExecutionOrder<CharacterManager>(-150);
        Container.BindInitializableExecutionOrder<WorldManager>(50); // Map ban đầu trước khi GameManager vào Home.
        Container.BindInitializableExecutionOrder<GameManager>(100); // Prewarm/spawn sau khi mọi miền đã sẵn sàng.
    }

    // Tick/LateTick chạy số nhỏ trước. Thứ tự bên trong một miền do Manager của nó tự gọi.
    private void ConfigureFrameOrder()
    {
        Container.BindTickableExecutionOrder<RunManager>(-250); // Đồng hồ trận đầu frame.
        Container.BindTickableExecutionOrder<CombatManager>(-100); // Bên trong: effect → skill → đạn.
        Container.BindTickableExecutionOrder<StageManager>(200);
        Container.BindTickableExecutionOrder<CharacterManager>(300);
        Container.BindTickableExecutionOrder<WorldManager>(400); // Nền theo Player sau khi nhân vật đã tick.
        Container.BindLateTickableExecutionOrder<SimulationManager>(0); // Áp dụng di chuyển sau khi mọi Tick đã gửi lệnh.
        Container.BindLateTickableExecutionOrder<StageManager>(200); // Bù chuyển động camera cho đội hình sau khi đã di chuyển.
        Container.BindLateTickableExecutionOrder<RunManager>(600); // Nhặt loot sau khi đã áp dụng chuyển động.
    }

    // Dispose chạy số lớn trước: dừng game → thu hồi entity/VFX → trả pool → giải phóng save và buffer Jobs.
    private void ConfigureDisposalOrder()
    {
        Container.BindDisposableExecutionOrder<GameManager>(300); // Hủy prewarm, gỡ listener, trả timeScale.
        Container.BindDisposableExecutionOrder<StageManager>(250);
        // Sau Stage (Stage dùng World), trước Character (World nghe Player) và Pool (chunk trả về pool).
        Container.BindDisposableExecutionOrder<WorldManager>(230);
        // Trước Character (Run nghe PlayerChanged/CharacterDied) và Pool (pickup trả về pool).
        Container.BindDisposableExecutionOrder<RunManager>(210);
        // Thu hồi mọi nhân vật trước khi dọn Combat và pool. Thứ tự nội bộ xem CharacterManager.Dispose.
        Container.BindDisposableExecutionOrder<CharacterManager>(200);
        // Sau Character: nhân vật despawn còn gọi Combat để dọn skill/effect. Trước Pool: skill, đạn và VFX còn trả visual.
        Container.BindDisposableExecutionOrder<CombatManager>(175);
        Container.BindDisposableExecutionOrder<PoolManager>(100);
        Container.BindDisposableExecutionOrder<ProgressionManager>(0); // Sau mọi miền gameplay (còn đọc/ghi ví khi dọn).
        Container.BindDisposableExecutionOrder<SaveManager>(-50); // Thử ghi lần cuối phần còn chờ.
        // Gần cuối: mọi miền đã gỡ đăng ký movement/hitbox. Bên trong: movement rồi spatial.
        Container.BindDisposableExecutionOrder<SimulationManager>(-100);
    }

    #endregion

    #region Application Save Lifecycle

    // Resolve sau khi cài đặt xong; lưu dữ liệu khi ứng dụng mất focus, tạm dừng hoặc thoát.
    public override void Start() => saves = Container.Resolve<ISaveMaintenance>();

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            saves?.SaveIfDirty();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            saves?.SaveIfDirty();
    }

    private void OnApplicationQuit() => saves?.SaveIfDirty();

    #endregion

    #region Editor Gizmos

    // Vùng sinh quái cấu hình ở installer nên vẽ tại đây.
    private void OnDrawGizmosSelected() => stage?.DrawGizmos();

    #endregion
}
