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
    [SerializeField] private CharacterType defaultPlayerType = CharacterType.BlessedGladiator; // * Loại Player khi save chưa chọn nhân vật.
    [SerializeField] private VirtualJoystick joystick;  // * Joystick trên Canvas, inject cho Player được tạo.
    [SerializeField] private string playerSaveFile = GameConstants.SaveKeys.PlayerFile;  // * Tên file save người chơi; không chứa dữ liệu của lượt chơi.

    #endregion

    [Header("Player Damage Feedback")]
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.12f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

    #region Gameplay Databases

    [SerializeField] private CharacterDatabaseSO characterDatabase;  // * Danh mục template nhân vật, dùng để dựng dictionary theo ID.
    [SerializeField] private LevelDatabaseSO levelDatabase;          // * Config vĩnh viễn theo nhân vật và config trận dùng chung.
    [SerializeField] private EquipmentDatabaseSO equipmentDatabase;  // * Tra bonus từ ID trang bị đã lưu.

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
        SignalBusInstaller.Install(Container);
        Container.BindInstance(new SaveManager.Settings(playerSaveFile));
        Container.BindInterfacesAndSelfTo<SaveManager>().AsSingle().NonLazy();
        Container.Bind<CharacterDatabaseSO>().FromInstance(characterDatabase).AsSingle();
        Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<EquipmentDatabaseSO>().FromInstance(equipmentDatabase).AsSingle();
        Container.BindInterfacesAndSelfTo<CharacterManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerEquipmentService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterLevelService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<RunLevelService>().AsSingle().NonLazy();
        Container.Bind<LevelManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterFactory>().AsSingle().WithArguments(characterPrefab);
        Container.BindInstance(new PlayerManager.Settings(defaultPlayerType, damageFlashDuration, damageFlashColor));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<PoolManager>().AsSingle().NonLazy();
        Container.BindInstance(combatAssets);
        Container.Bind<ITimeSource>().To<GameplayTimeSource>().AsSingle();
        Container.BindInterfacesAndSelfTo<TimerManager>().AsSingle().NonLazy();
        Container.BindTickableExecutionOrder<TimerManager>(-300); // Tính/nhận kết quả trước các hệ thống thực thi gameplay.
        Container.Bind<ISpatialSearchFactory>().To<SpatialSearchFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<SpatialManager>().AsSingle();
        Container.Bind<ICharacterSearchFactory>().To<CharacterSearchFactory>().AsSingle();
        Container.Bind<ICharacterSearch>().To<CharacterSearch>().AsSingle();
        Container.Bind<CombatSpawner>().AsSingle();
        Container.Bind<ShootBehaviorFactory>().AsSingle();
        Container.Bind<ProjectileMoveFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<ProjectileManager>().AsSingle();
        Container.BindTickableExecutionOrder<ProjectileManager>(100);
        Container.Bind<SkillFactory>().AsSingle();
        Container.BindInterfacesAndSelfTo<SkillManager>().AsSingle().NonLazy();
        Container.BindTickableExecutionOrder<SkillManager>(-100);
        // Pool class thuần: chỉ tạo thêm runtime khi số effect đồng thời vượt dung lượng đã có.
        Container.BindMemoryPool<SlowEffectRuntime, ClassPool<SlowEffectRuntime>>().WithInitialSize(16);
        Container.BindMemoryPool<DamageOverTimeEffectRuntime, ClassPool<DamageOverTimeEffectRuntime>>().WithInitialSize(32);
        Container.Bind<EffectRuntimePool>().AsSingle();
        Container.BindInterfacesAndSelfTo<EffectManager>().AsSingle().NonLazy();
        // Dọn effect trước khi Zenject giải phóng các memory pool.
        Container.BindDisposableExecutionOrder<EffectManager>(150);
        Container.BindInterfacesAndSelfTo<MovementManager>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<PlayerManager>(-100);
        Container.BindInitializableExecutionOrder<SaveManager>(-200);
        // * Thu hồi nhân vật theo thứ tự visual → vỏ trước khi pool tổng quát và buffer Jobs được giải phóng.
        Container.BindDisposableExecutionOrder<CharacterFactory>(200);
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
