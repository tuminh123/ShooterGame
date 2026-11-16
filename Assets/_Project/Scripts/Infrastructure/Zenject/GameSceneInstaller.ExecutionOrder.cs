using Zenject;

// Thứ tự lifecycle tập trung; binding qua interface không thay đổi lịch thực thi.
public sealed partial class GameSceneInstaller
{
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

}
