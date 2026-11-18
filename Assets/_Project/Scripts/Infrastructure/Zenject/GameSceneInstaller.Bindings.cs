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

    // Level và trang bị (sẽ vào ProgressionManager ở Giai đoạn 5).
    private void BindCharactersAndLevels()
    {
        BindService<IEquipmentService, PlayerEquipmentService>().AsSingle().NonLazy();
        BindService<ICharacterLevelService, CharacterLevelService>().AsSingle().NonLazy();
        BindService<IRunLevelService, RunLevelService>().AsSingle().NonLazy();
        BindService<ILevelService, LevelManager>().AsSingle().NonLazy();
    }

    // Miền Character: chỉ bind CharacterManager. Factory, registry, runtime pool, Player, hiển thị quái và
    // tìm mục tiêu do manager tự tạo; miền khác chỉ dùng CharacterContracts.cs.
    // Joystick và settings Player là dữ liệu scene/cấu hình mà manager nhận để dựng PlayerManager.
    private void BindPlayerAndInput()
    {
        Container.BindInstance(new PlayerManager.Settings(defaultPlayerType));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
        Container.BindInterfacesTo<CharacterManager>().AsSingle().WithArguments(characterPrefab).NonLazy();
    }

    // Miền World: chỉ bind WorldManager (map nền + nền lặp theo Player); miền khác chỉ dùng WorldContracts.cs.
    // Miền Stage: chỉ bind StageManager (tự tạo EnemySpawner, Wave/Formation/Boss, BossArena, StageRewards từ cấu hình "stage");
    // miền khác chỉ dùng StageContracts.cs.
    private void BindStageAndWorld()
    {
        Container.BindInterfacesTo<WorldManager>().AsSingle().WithArguments(world).NonLazy();
        Container.BindInterfacesTo<StageManager>().AsSingle().WithArguments(stage).NonLazy();
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
        BindService<IPoolPrewarmService, PoolPrewarmService>().AsSingle().WithArguments(characterPrefab).NonLazy();
    }

    #endregion

    #region Movement, Combat & Skills

    // Miền Simulation: chỉ bind SimulationManager (job di chuyển + lưới không gian), các vai trò nằm trong SimulationContracts.cs.
    private void BindSpatialAndMovement()
    {
        Container.BindInterfacesTo<SimulationManager>().AsSingle().NonLazy();
    }

    // Miền Combat: chỉ bind CombatManager. Skill, đạn, effect và VFX do manager tự tạo bên trong,
    // nên miền khác chỉ dùng được các interface trong CombatContracts.cs.
    // Hai MemoryPool là hạ tầng của Zenject; CombatManager nhận chúng để dựng pool runtime của effect.
    private void BindCombat()
    {
        Container.BindMemoryPool<SlowEffectRuntime, ClassPool<SlowEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.slowRuntimes);
        Container.BindMemoryPool<DamageOverTimeEffectRuntime, ClassPool<DamageOverTimeEffectRuntime>>()
            .WithInitialSize(poolPrewarmSettings.damageOverTimeRuntimes);
        Container.BindInterfacesTo<CombatManager>().AsSingle().NonLazy();
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
}
