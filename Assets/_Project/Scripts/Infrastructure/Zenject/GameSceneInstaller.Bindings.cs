using System;
using System.Linq;
using Terresquall;
using UnityEngine;
using Zenject;

// Đăng ký một implementation cho toàn bộ vai trò; không công khai concrete service.
public sealed partial class GameSceneInstaller
{
    #region Shared Data & Persistent Services

    // Hạ tầng sự kiện Zenject và token pause/slow motion dùng chung giữa gameplay và UI.
    private void BindSignalsAndPause()
    {
        SignalBusInstaller.Install(Container);
        BindService<IGameplayPauseService, GameplayPauseService>().AsSingle();
    }

    // Lưu/đọc dữ liệu, chuyển phiên bản save, quản lý ví và tiến trình vượt màn.
    private void BindSaveAndProgress()
    {
        Container.BindInstance(new SaveManager.Settings(playerSaveFile));
        BindService<ISaveService, SaveManager>().AsSingle().NonLazy();
        Container.BindInterfacesTo<SaveMigration>().AsSingle().NonLazy();
        BindService<ICurrencyService, CurrencyService>().AsSingle().NonLazy();
        BindService<IStageProgress, StageProgressService>().AsSingle().NonLazy();
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
        BindService<ICharacterSystem, CharacterManager>().AsSingle();
        Container.BindInterfacesTo<EnemyVisualManager>().AsSingle().NonLazy();
        BindService<IEquipmentService, PlayerEquipmentService>().AsSingle().NonLazy();
        BindService<ICharacterLevelService, CharacterLevelService>().AsSingle().NonLazy();
        BindService<IRunLevelService, RunLevelService>().AsSingle().NonLazy();
        BindService<ILevelService, LevelManager>().AsSingle().NonLazy();
        BindService<ICharacterFactory, CharacterFactory>().AsSingle().WithArguments(characterPrefab);
    }

    // Nhân vật Player hiện tại và joystick có sẵn trong scene; settings chọn Player mặc định.
    private void BindPlayerAndInput()
    {
        Container.BindInstance(new PlayerManager.Settings(defaultPlayerType));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
        BindService<IPlayerSystem, PlayerManager>().AsSingle().NonLazy();
    }

    // Điều phối màn/wave/boss và nền; spawner/nền là component scene, không tạo GameObject mới ở đây.
    private void BindStageAndWorld()
    {
        BindSceneSystem<IEnemySpawnService, EnemySpawnManager>();
        BindSceneSystem<IWorldMapService, GroundChunkManager>();
        BindService<IBossSummonService, BossHandleSummon>().AsSingle();
        BindService<IStageService, StageManager>().AsSingle().NonLazy();
    }

    #endregion

    #region Game Flow, Rewards & Pooling

    // GameManager mở/đóng lượt chơi; UIManager điều phối các panel có sẵn trên Canvas.
    private void BindGameFlowAndUI()
    {
        BindService<IUINavigation, UIManager>().FromComponentInHierarchy().AsSingle();
        BindService<IScreenTransitionService, ScreenTransitionController>().FromComponentInHierarchy().AsSingle();
        BindService<IGameFlow, GameManager>().AsSingle().WithArguments(autoStartRun).NonLazy();
    }

    // Thống kê thời gian/vàng/quái của lượt chơi và vòng đời rơi/nhặt loot.
    private void BindRunSessionAndLoot()
    {
        BindService<IRunSession, RunSessionManager>().AsSingle().NonLazy();
        Container.BindInterfacesTo<LootManager>().AsSingle().NonLazy();
    }

    // Pool vỏ/visual GameObject, dữ liệu nhân vật và bước chuẩn bị tài nguyên trước khi spawn Player.
    private void BindPoolsAndPrewarm()
    {
        BindService<IPoolSystem, PoolManager>().AsSingle().NonLazy();
        BindService<ICharacterRuntimePool, CharacterRuntimePool>().AsSingle();
        BindService<IPoolPrewarmService, PoolPrewarmService>().AsSingle().WithArguments(characterPrefab).NonLazy();
    }

    #endregion

    #region Movement, Combat & Skills

    // Job di chuyển và truy vấn không gian; factory chọn chiến lược tìm mục tiêu cho các hệ gameplay.
    private void BindSpatialAndMovement()
    {
        BindService<ISpatialSearchFactory, SpatialSearchFactory>().AsSingle();
        BindService<ISpatialManager, SpatialManager>().AsSingle();
        Container.Bind<ICharacterSearchFactory>().To<CharacterSearchFactory>().AsSingle();
        BindService<ICharacterSearch, CharacterSearch>().AsSingle();
        BindService<IMovementSystem, MovementManager>().AsSingle().NonLazy();
    }

    // Tạo/thu hồi đạn và VFX; factory chọn kiểu bắn/quỹ đạo, manager cập nhật toàn bộ đạn.
    private void BindShootingAndProjectiles()
    {
        BindService<ICombatSpawnSystem, CombatSpawner>().AsSingle();
        BindService<IShootBehaviorFactory, ShootBehaviorFactory>().AsSingle();
        BindService<IProjectileMoveFactory, ProjectileMoveFactory>().AsSingle();
        BindService<IProjectileSystem, ProjectileManager>().AsSingle();
    }

    // Skill gameplay, hiệu ứng độc/đốt/làm chậm và pool runtime class thuần của từng loại effect.
    private void BindSkillsAndEffects()
    {
        BindService<ISkillFactory, SkillFactory>(typeof(ISkillServices)).AsSingle();
        BindService<ISkillSystem, SkillManager>().AsSingle().NonLazy();
        Container.BindMemoryPool<SlowEffectRuntime, ClassPool<SlowEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.slowRuntimes);
        Container.BindMemoryPool<DamageOverTimeEffectRuntime, ClassPool<DamageOverTimeEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.damageOverTimeRuntimes);
        BindService<IEffectRuntimePool, EffectRuntimePool>().AsSingle();
        BindService<IEffectSystem, EffectManager>().AsSingle().NonLazy();
    }

    #endregion

    #region Service Facade Binding

    // Chỉ đăng ký đầu mối tổng và lifecycle của Zenject; vai trò nhỏ lấy qua getter.
    private FromBinderNonGeneric BindService<TFacade, TImplementation>(params Type[] additionalFacades)
        where TFacade : class
        where TImplementation : class, TFacade
    {
        Type implementation = typeof(TImplementation);
        Type[] lifecycle = { typeof(IInitializable), typeof(ITickable), typeof(ILateTickable),
            typeof(IFixedTickable), typeof(IDisposable) };
        var contracts = new[] { typeof(TFacade) }.Concat(additionalFacades)
            .Concat(lifecycle.Where(contract => contract.IsAssignableFrom(implementation))).Distinct();
        return Container.Bind(contracts).To<TImplementation>();
    }

    #endregion

    #region Scene Contract Binding

    // Chỉ bind hợp đồng gameplay: component scene tự đăng ký TickableManager trong Start.
    // BindInterfacesTo ở đây sẽ đăng ký thêm ITickable/ILateTickable và gây cập nhật hai lần.
    private void BindSceneSystem<TContract, TComponent>()
        where TContract : class
        where TComponent : Component, TContract
    {
        Container.Bind<TContract>().To<TComponent>().FromComponentInHierarchy().AsSingle();
    }

    #endregion
}
