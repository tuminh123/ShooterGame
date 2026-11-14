using System;
using Terresquall;
using UnityEngine;
using Zenject;

// * Điểm cấu hình prefab player, joystick và các dịch vụ class thuần cho scene.
public sealed class GameSceneInstaller : MonoInstaller
{
    private SaveManager saves;

    #region Scene References

    [SerializeField] private CharacterView characterPrefab; // * Một prefab vỏ duy nhất dùng cho cả Player, Enemy và boss.
    [SerializeField] private CombatAssetsSO combatAssets; // Shared bullet shell, bullet sprites and effect prefabs.
    [SerializeField] private PoolPrewarmSettingsSO poolPrewarmSettings;
    [SerializeField] private CharacterType defaultPlayerType = CharacterType.BlessedGladiator; // * Loại Player khi save chưa chọn nhân vật.
    [SerializeField] private VirtualJoystick joystick;  // * Joystick trên Canvas, inject cho Player được tạo.
    [SerializeField] private string playerSaveFile = GameConstants.SaveKeys.PlayerFile;  // * Tên file save người chơi; không chứa dữ liệu của lượt chơi.

    #endregion

    [Header("Gameplay Databases")]

    #region Gameplay Databases

    [SerializeField] private CharacterDatabaseSO characterDatabase;  // * Danh mục template nhân vật, dùng để dựng dictionary theo ID.
    [SerializeField] private LevelDatabaseSO levelDatabase;          // * Config vĩnh viễn theo nhân vật và config trận dùng chung.
    [SerializeField] private EquipmentDatabaseSO equipmentDatabase;  // * Tra bonus từ ID trang bị đã lưu.
    [SerializeField] private LootDatabaseSO lootDatabase; // Catalog EXP/vàng: prefab, sprite, lượng, tỉ lệ và kích thước.
    [SerializeField] private SkillDatabaseSO skillDatabase; // Catalog riêng cho skill gameplay và quyền xuất hiện trong bảng thưởng.
    [Tooltip("Danh mục tất cả màn và màn mặc định; StageManager chọn Stage từ database này.")]
    [SerializeField] private StageDatabaseSO stageDatabase;

    #endregion

    #region Game Flow Settings

    [SerializeField] private bool autoStartRun = false; // Loading → Home; chỉ vào trận khi nút Play gọi StartRun.

    #endregion

    #region Installation Entry Point

    // Chỉ điều phối các nhóm binding; Zenject resolve dependency sau khi cài đặt xong toàn bộ.
    public override void InstallBindings()
    {
        ValidateDependencies();
        BindSignalsAndPause();
        BindSaveAndProgress();
        BindDatabases();
        BindCharactersAndLevels();
        BindPlayerAndInput();
        BindStageAndWorld();
        BindGameFlowAndUI();
        BindRunSessionAndLoot();
        BindPoolsAndPrewarm();
        BindShootingAndProjectiles();
        BindSkillsAndEffects();
        BindSpatialAndMovement();
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

    #endregion

    #region Shared Data & Persistent Services

    // Hạ tầng sự kiện Zenject và token pause/slow motion dùng chung giữa gameplay và UI.
    private void BindSignalsAndPause()
    {
        SignalBusInstaller.Install(Container);
        Container.BindInterfacesAndSelfTo<GameplayPauseService>().AsSingle();
    }

    // Lưu/đọc dữ liệu, chuyển phiên bản save, quản lý ví và tiến trình vượt màn.
    private void BindSaveAndProgress()
    {
        Container.BindInstance(new SaveManager.Settings(playerSaveFile));
        Container.BindInterfacesAndSelfTo<SaveManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<SaveMigration>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CurrencyService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<StageProgressService>().AsSingle().NonLazy();
    }

    // Dùng đúng asset đã gán trong Inspector; không tạo bản sao hoặc giữ trạng thái trận trên DataSO.
    private void BindDatabases()
    {
        Container.Bind<CharacterDatabaseSO>().FromInstance(characterDatabase).AsSingle();
        Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<EquipmentDatabaseSO>().FromInstance(equipmentDatabase).AsSingle();
        Container.Bind<LootDatabaseSO>().FromInstance(lootDatabase).AsSingle();
        Container.Bind<SkillDatabaseSO>().FromInstance(skillDatabase).AsSingle();
        Container.Bind<StageDatabaseSO>().FromInstance(stageDatabase).AsSingle();
        Container.BindInstance(combatAssets);
        Container.BindInstance(poolPrewarmSettings);
    }

    #endregion

    #region Characters, Player & Stage

    // Registry nhân vật, vòng đời spawn/despawn, hiển thị Enemy và bonus từ cấp/trang bị.
    private void BindCharactersAndLevels()
    {
        Container.BindInterfacesAndSelfTo<CharacterManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<EnemyVisualManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<PlayerEquipmentService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterLevelService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<RunLevelService>().AsSingle().NonLazy();
        Container.Bind<LevelManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterFactory>().AsSingle().WithArguments(characterPrefab);
    }

    // Nhân vật Player hiện tại và joystick có sẵn trong scene; settings chọn Player mặc định.
    private void BindPlayerAndInput()
    {
        Container.BindInstance(new PlayerManager.Settings(defaultPlayerType));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerManager>().AsSingle().NonLazy();
    }

    // Điều phối màn/wave/boss và nền; spawner/nền là component scene, không tạo GameObject mới ở đây.
    private void BindStageAndWorld()
    {
        Container.Bind<EnemySpawnManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<GroundChunkManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<BossHandleSummon>().AsSingle();
        Container.BindInterfacesAndSelfTo<StageManager>().AsSingle().NonLazy();
    }

    #endregion

    #region Game Flow, Rewards & Pooling

    // GameManager mở/đóng lượt chơi; UIManager điều phối các panel có sẵn trên Canvas.
    private void BindGameFlowAndUI()
    {
        Container.Bind<UIManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<ScreenTransitionController>().FromComponentInHierarchy().AsSingle();
        Container.BindInterfacesAndSelfTo<GameManager>().AsSingle().WithArguments(autoStartRun).NonLazy();
    }

    // Thống kê thời gian/vàng/quái của lượt chơi và vòng đời rơi/nhặt loot.
    private void BindRunSessionAndLoot()
    {
        Container.BindInterfacesAndSelfTo<RunSessionManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<LootManager>().AsSingle().NonLazy();
    }

    // Pool vỏ/visual GameObject, dữ liệu nhân vật và bước chuẩn bị tài nguyên trước khi spawn Player.
    private void BindPoolsAndPrewarm()
    {
        Container.BindInterfacesAndSelfTo<PoolManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterRuntimePool>().AsSingle();
        Container.BindInterfacesAndSelfTo<PoolPrewarmService>().AsSingle().WithArguments(characterPrefab).NonLazy();
    }

    #endregion

    #region Movement, Combat & Skills

    // Job di chuyển và truy vấn không gian; factory chọn chiến lược tìm mục tiêu cho các hệ gameplay.
    private void BindSpatialAndMovement()
    {
        Container.Bind<ISpatialSearchFactory>().To<SpatialSearchFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<SpatialManager>().AsSingle();
        Container.Bind<ICharacterSearchFactory>().To<CharacterSearchFactory>().AsSingle();
        Container.Bind<ICharacterSearch>().To<CharacterSearch>().AsSingle();
        Container.BindInterfacesAndSelfTo<MovementManager>().AsSingle().NonLazy();
    }

    // Tạo/thu hồi đạn và VFX; factory chọn kiểu bắn/quỹ đạo, manager cập nhật toàn bộ đạn.
    private void BindShootingAndProjectiles()
    {
        Container.BindInterfacesAndSelfTo<CombatSpawner>().AsSingle();
        Container.Bind<ShootBehaviorFactory>().AsSingle();
        Container.Bind<ProjectileMoveFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<ProjectileManager>().AsSingle();
    }

    // Skill gameplay, hiệu ứng độc/đốt/làm chậm và pool runtime class thuần của từng loại effect.
    private void BindSkillsAndEffects()
    {
        Container.Bind<SkillFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<SkillManager>().AsSingle().NonLazy();
        Container.BindMemoryPool<SlowEffectRuntime, ClassPool<SlowEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.slowRuntimes);
        Container.BindMemoryPool<DamageOverTimeEffectRuntime, ClassPool<DamageOverTimeEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.damageOverTimeRuntimes);
        Container.Bind<EffectRuntimePool>().AsSingle();
        Container.BindInterfacesAndSelfTo<EffectManager>().AsSingle().NonLazy();
    }

    #endregion

    #region Initialization Order

    // Initialize chạy số nhỏ trước: load/migrate save → dịch vụ tiến trình → đăng ký lượt chơi → bắt đầu game.
    private void ConfigureInitializationOrder()
    {
        Container.BindInitializableExecutionOrder<SaveManager>(-200);
        Container.BindInitializableExecutionOrder<SaveMigration>(-180);
        Container.BindInitializableExecutionOrder<CurrencyService>(-150);
        Container.BindInitializableExecutionOrder<PlayerEquipmentService>(-150);
        Container.BindInitializableExecutionOrder<CharacterLevelService>(-150);
        Container.BindInitializableExecutionOrder<PlayerManager>(-150);
        Container.BindInitializableExecutionOrder<StageProgressService>(-140);
        Container.BindInitializableExecutionOrder<RunSessionManager>(-50);
        Container.BindInitializableExecutionOrder<GameManager>(100); // Prewarm/spawn sau khi các service đã sẵn sàng.
    }

    #endregion

    #region Frame Update Order

    // Tick chạy số nhỏ trước. Cooldown/duration thuộc runtime; Zenject gọi trực tiếp từng manager.
    private void ConfigureFrameOrder()
    {
        Container.BindTickableExecutionOrder<EffectManager>(-300);
        Container.BindTickableExecutionOrder<RunSessionManager>(-250);
        Container.BindTickableExecutionOrder<SkillManager>(-100);
        Container.BindTickableExecutionOrder<ProjectileManager>(100);
        Container.BindTickableExecutionOrder<StageManager>(200);
        Container.BindTickableExecutionOrder<CharacterManager>(300);
        Container.BindLateTickableExecutionOrder<MovementManager>(0);
        Container.BindLateTickableExecutionOrder<LootManager>(600); // Nhặt đồ sau khi đã áp dụng chuyển động.
    }

    #endregion

    #region Disposal Order

    // Dispose chạy số lớn trước: dừng game → thu hồi entity/VFX → trả pool → giải phóng buffer Jobs/save.
    private void ConfigureDisposalOrder()
    {
        Container.BindDisposableExecutionOrder<GameManager>(300);
        Container.BindDisposableExecutionOrder<StageManager>(250);
        Container.BindDisposableExecutionOrder<EnemyVisualManager>(225); // Gỡ callback camera trước khi dọn nhân vật.
        Container.BindDisposableExecutionOrder<LootManager>(210);
        Container.BindDisposableExecutionOrder<RunSessionManager>(205);
        Container.BindDisposableExecutionOrder<CharacterFactory>(200); // Trả visual và vỏ trước khi dọn pool.
        Container.BindDisposableExecutionOrder<ProjectileManager>(175);
        Container.BindDisposableExecutionOrder<CombatSpawner>(160);
        Container.BindDisposableExecutionOrder<EffectManager>(150);
        Container.BindDisposableExecutionOrder<CharacterManager>(125);
        Container.BindDisposableExecutionOrder<PoolManager>(100);
        Container.BindDisposableExecutionOrder<CharacterRuntimePool>(90);
        Container.BindDisposableExecutionOrder<PlayerManager>(50);
        Container.BindDisposableExecutionOrder<CurrencyService>(0);
        Container.BindDisposableExecutionOrder<SaveManager>(-50);
        Container.BindDisposableExecutionOrder<SkillManager>(-75);
        Container.BindDisposableExecutionOrder<MovementManager>(-100);
        Container.BindDisposableExecutionOrder<SpatialManager>(-250); // Snapshot sống tới khi các bên dùng đã dọn xong.
    }

    #endregion

    #region Application Save Lifecycle

    // Resolve sau cài đặt; lưu dữ liệu khi ứng dụng mất focus, tạm dừng hoặc thoát.
    public override void Start() => saves = Container.Resolve<SaveManager>();

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
}
