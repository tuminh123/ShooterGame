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

    [Header("Player Damage Feedback")]

    #region Gameplay Databases

    [SerializeField] private CharacterDatabaseSO characterDatabase;  // * Danh mục template nhân vật, dùng để dựng dictionary theo ID.
    [SerializeField] private LevelDatabaseSO levelDatabase;          // * Config vĩnh viễn theo nhân vật và config trận dùng chung.
    [SerializeField] private EquipmentDatabaseSO equipmentDatabase;  // * Tra bonus từ ID trang bị đã lưu.
    [SerializeField] private LootDatabaseSO lootDatabase; // Catalog EXP/vàng: prefab, sprite, lượng, tỉ lệ và kích thước.
    [SerializeField] private SkillDatabaseSO skillDatabase; // Catalog riêng cho skill gameplay và quyền xuất hiện trong bảng thưởng.

    #endregion

    #region Game Flow Settings

    [SerializeField] private bool autoStartRun = true; // Scene hiện chưa có Home UI; tắt để chờ GameManager.StartRun từ nút Play sau này.
    [SerializeField] private StageDataSO stageData;

    #endregion

    #region Service Bindings

    // * Một instance mỗi dịch vụ; Zenject quản lý Initialize, LateTick và Dispose.

    public override void InstallBindings()
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
        if (stageData == null) throw new InvalidOperationException("Assign Stage Data in GameSceneInstaller.");
        stageData.Validate();
        SignalBusInstaller.Install(Container);
        Container.BindInterfacesAndSelfTo<GameplayPauseService>().AsSingle();
        Container.BindInstance(new SaveManager.Settings(playerSaveFile));
        Container.BindInterfacesAndSelfTo<SaveManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<SaveMigration>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<SaveMigration>(-180);
        Container.BindInterfacesAndSelfTo<CurrencyService>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<CurrencyService>(-150);
        Container.BindInterfacesAndSelfTo<StageProgressService>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<StageProgressService>(-140);
        Container.BindDisposableExecutionOrder<CurrencyService>(0); // Gỡ listener trước khi SaveManager ghi và kết thúc.
        Container.Bind<CharacterDatabaseSO>().FromInstance(characterDatabase).AsSingle();
        Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<EquipmentDatabaseSO>().FromInstance(equipmentDatabase).AsSingle();
        Container.Bind<LootDatabaseSO>().FromInstance(lootDatabase).AsSingle();
        Container.Bind<SkillDatabaseSO>().FromInstance(skillDatabase).AsSingle();
        Container.BindInterfacesAndSelfTo<CharacterManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<EnemyVisualManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<PlayerEquipmentService>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<PlayerEquipmentService>(-150);
        Container.BindInterfacesAndSelfTo<CharacterLevelService>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<CharacterLevelService>(-150);
        Container.BindInterfacesAndSelfTo<RunLevelService>().AsSingle().NonLazy();
        Container.Bind<LevelManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterFactory>().AsSingle().WithArguments(characterPrefab);
        Container.BindInstance(new PlayerManager.Settings(defaultPlayerType));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerManager>().AsSingle().NonLazy();
        Container.BindInstance(stageData);
        Container.Bind<EnemySpawnManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<GroundChunkManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<BossHandleSummon>().AsSingle();
        Container.BindInterfacesAndSelfTo<StageManager>().AsSingle().NonLazy();
        Container.BindTickableExecutionOrder<StageManager>(200); // Sau projectile/timer gameplay, kiểm tra dọn hết màn.
        Container.BindDisposableExecutionOrder<StageManager>(250); // Sau GameManager, trước factory và pool.
        Container.BindInitializableExecutionOrder<PlayerManager>(-150);
        Container.Bind<UIManager>().FromComponentInHierarchy().AsSingle(); // Điều phối trên Canvas; controller từng panel tự nhận dependency qua scene injection.
        Container.BindInterfacesAndSelfTo<GameManager>().AsSingle().WithArguments(autoStartRun).NonLazy();
        Container.BindInitializableExecutionOrder<GameManager>(100); // Sau Save/RunSession/Loot đã đăng ký; manager tự chuẩn bị tài nguyên rồi spawn Player.
        Container.BindDisposableExecutionOrder<GameManager>(300); // Trả token/listener trước các service mà luồng game sử dụng.
        Container.BindInterfacesAndSelfTo<RunSessionManager>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<RunSessionManager>(-50); // Sau khi Player được tạo, trước hệ thống nhặt đồ.
        Container.BindDisposableExecutionOrder<RunSessionManager>(205); // Sau LootManager, trước khi dọn Player/timer.
        Container.BindInterfacesAndSelfTo<LootManager>().AsSingle().NonLazy();
        Container.BindLateTickableExecutionOrder<LootManager>(100); // Nhặt bằng vị trí Player sau bước MovementManager.
        Container.BindDisposableExecutionOrder<LootManager>(210); // Dọn vật phẩm trước khi PoolManager kết thúc.
        Container.BindInterfacesAndSelfTo<PoolManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterRuntimePool>().AsSingle();
        Container.BindInstance(poolPrewarmSettings);
        Container.BindInterfacesAndSelfTo<PoolPrewarmService>().AsSingle().WithArguments(characterPrefab).NonLazy();
        Container.BindDisposableExecutionOrder<CharacterRuntimePool>(90); // Sau factory/view cleanup và PoolManager.
        Container.BindInstance(combatAssets);
        Container.Bind<ITimeSource>().To<GameplayTimeSource>().AsSingle();
        Container.BindInterfacesAndSelfTo<TimerManager>().AsSingle().NonLazy();
        Container.BindTickableExecutionOrder<TimerManager>(-300); // Tính/nhận kết quả trước các hệ thống thực thi gameplay.
        Container.Bind<ISpatialSearchFactory>().To<SpatialSearchFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<SpatialManager>().AsSingle();
        Container.Bind<ICharacterSearchFactory>().To<CharacterSearchFactory>().AsSingle();
        Container.Bind<ICharacterSearch>().To<CharacterSearch>().AsSingle();
        Container.BindInterfacesAndSelfTo<CombatSpawner>().AsSingle();
        Container.BindDisposableExecutionOrder<CombatSpawner>(160); // Thu hồi VFX trước khi PoolManager được giải phóng.
        Container.Bind<ShootBehaviorFactory>().AsSingle();
        Container.Bind<ProjectileMoveFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<ProjectileManager>().AsSingle();
        Container.BindTickableExecutionOrder<ProjectileManager>(100);
        Container.Bind<SkillFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<SkillManager>().AsSingle().NonLazy();
        Container.BindTickableExecutionOrder<SkillManager>(-100);
        // Pool class thuần: chỉ tạo thêm runtime khi số effect đồng thời vượt dung lượng đã có.
        Container.BindMemoryPool<SlowEffectRuntime, ClassPool<SlowEffectRuntime>>().WithInitialSize(poolPrewarmSettings.slowRuntimes);
        Container.BindMemoryPool<DamageOverTimeEffectRuntime, ClassPool<DamageOverTimeEffectRuntime>>().WithInitialSize(poolPrewarmSettings.damageOverTimeRuntimes);
        Container.Bind<EffectRuntimePool>().AsSingle();
        Container.BindInterfacesAndSelfTo<EffectManager>().AsSingle().NonLazy();
        // Dọn effect trước khi Zenject giải phóng các memory pool.
        Container.BindDisposableExecutionOrder<EffectManager>(150);
        Container.BindInterfacesAndSelfTo<MovementManager>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<SaveManager>(-200);
        // * Thu hồi nhân vật theo thứ tự visual → vỏ trước khi pool tổng quát và buffer Jobs được giải phóng.
        Container.BindDisposableExecutionOrder<CharacterFactory>(200);
        Container.BindDisposableExecutionOrder<EnemyVisualManager>(225); // Gỡ callback camera trước khi thu hồi nhân vật.
        Container.BindDisposableExecutionOrder<CharacterManager>(125);
        Container.BindDisposableExecutionOrder<PoolManager>(100);
        Container.BindDisposableExecutionOrder<PlayerManager>(50);
        Container.BindDisposableExecutionOrder<SkillManager>(-75);
        Container.BindDisposableExecutionOrder<SaveManager>(-50);
        Container.BindDisposableExecutionOrder<MovementManager>(-100);
        Container.BindDisposableExecutionOrder<ProjectileManager>(175);
        // Các bên sở hữu hủy handle trước khi bộ đếm và snapshot native được giải phóng.
        Container.BindDisposableExecutionOrder<TimerManager>(-200);
        Container.BindDisposableExecutionOrder<SpatialManager>(-250);
    }

    #endregion

    // Resolve after installation; SaveManager is bound by this installer itself.
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

}
