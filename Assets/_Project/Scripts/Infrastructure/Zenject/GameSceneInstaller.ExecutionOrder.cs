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
        Container.BindInitializableExecutionOrder<CharacterManager>(-150); // Đọc nhân vật đang chọn từ save (PlayerManager bên trong).
        Container.BindInitializableExecutionOrder<StageProgressService>(-140);
        Container.BindInitializableExecutionOrder<RunSessionManager>(-50);
        Container.BindInitializableExecutionOrder<WorldManager>(50); // Map ban đầu trước khi GameManager vào Home.
        Container.BindInitializableExecutionOrder<GameManager>(100); // Prewarm/spawn sau khi các service đã sẵn sàng.
    }

    #endregion

    #region Frame Update Order

    // Tick chạy số nhỏ trước. Cooldown/duration thuộc runtime; Zenject gọi trực tiếp từng manager.
    private void ConfigureFrameOrder()
    {
        Container.BindTickableExecutionOrder<RunSessionManager>(-250);
        // Effect → skill → đạn: thứ tự nội bộ do CombatManager.Tick tự gọi (trước đây là -300/-100/100).
        Container.BindTickableExecutionOrder<CombatManager>(-100);
        Container.BindTickableExecutionOrder<StageManager>(200);
        Container.BindTickableExecutionOrder<CharacterManager>(300);
        Container.BindTickableExecutionOrder<WorldManager>(400); // Nền theo Player sau khi nhân vật đã tick (cùng thứ tự với GroundChunkManager cũ).
        Container.BindLateTickableExecutionOrder<SimulationManager>(0); // Áp dụng di chuyển sau khi mọi Tick đã gửi lệnh.
        Container.BindLateTickableExecutionOrder<StageManager>(200); // Bù chuyển động camera cho đội hình sau khi đã di chuyển (giữ thứ tự cũ của EnemySpawnManager).
        Container.BindLateTickableExecutionOrder<LootManager>(600); // Nhặt đồ sau khi đã áp dụng chuyển động.
    }

    #endregion

    #region Disposal Order

    // Dispose chạy số lớn trước: dừng game → thu hồi entity/VFX → trả pool → giải phóng buffer Jobs/save.
    private void ConfigureDisposalOrder()
    {
        Container.BindDisposableExecutionOrder<GameManager>(300);
        Container.BindDisposableExecutionOrder<StageManager>(250);
        // Sau Stage (Stage dùng World), trước Character (World nghe Player) và Pool (chunk trả về pool).
        Container.BindDisposableExecutionOrder<WorldManager>(230);
        Container.BindDisposableExecutionOrder<LootManager>(210);
        Container.BindDisposableExecutionOrder<RunSessionManager>(205);
        // Thu hồi mọi nhân vật (trả visual và vỏ) trước khi dọn Combat và pool. Thứ tự nội bộ xem CharacterManager.Dispose.
        Container.BindDisposableExecutionOrder<CharacterManager>(200);
        // Sau CharacterManager: nhân vật despawn còn gọi Combat để dọn skill/effect của chính nó.
        // Trước PoolManager: skill, đạn và VFX còn trả visual về pool. Thứ tự nội bộ xem CombatManager.Dispose.
        Container.BindDisposableExecutionOrder<CombatManager>(175);
        Container.BindDisposableExecutionOrder<PoolManager>(100);
        Container.BindDisposableExecutionOrder<CurrencyService>(0);
        Container.BindDisposableExecutionOrder<SaveManager>(-50);
        // Gần cuối: mọi miền gameplay đã gỡ đăng ký movement/hitbox xong. Bên trong: movement rồi spatial (xem SimulationManager.Dispose).
        Container.BindDisposableExecutionOrder<SimulationManager>(-100);
    }

    #endregion

}
