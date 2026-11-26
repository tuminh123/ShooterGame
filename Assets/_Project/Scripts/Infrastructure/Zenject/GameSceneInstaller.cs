using System;
using Terresquall;
using UnityEngine;
using UnityEngine.Serialization;
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

    [SerializeField] private CharacterView characterPrefab; // Một prefab vỏ duy nhất dùng cho cả Player, Enemy và boss.
    [Tooltip("Kiểu số sát thương bay lên (font, màu, cỡ, nhịp). Trống = không hiện số.")]
    [SerializeField] private DamageTextStyleSO damageTextStyle;
    [SerializeField] private CombatAssetsSO combatAssets; // Vỏ đạn dùng chung, sprite đạn và prefab effect.
    [SerializeField] private PoolPrewarmSettingsSO poolPrewarmSettings;
    [SerializeField] private CharacterType defaultPlayerType = CharacterType.BlessedGladiator; // Loại Player khi save chưa chọn nhân vật.
    [SerializeField] private VirtualJoystick joystick; // Joystick trên Canvas, truyền cho PlayerController.
    [SerializeField] private string playerSaveFile = GameConstants.SaveKeys.PlayerFile; // Tên file save người chơi; không chứa dữ liệu của lượt chơi.
    [Tooltip("Nền lặp, camera bám Player và danh mục map; WorldManager dựng từ cấu hình này.")]
    [SerializeField] private WorldManager.Settings world = new WorldManager.Settings();
    [Tooltip("Vùng sinh quái và AI quái (tách đàn, phản lực); StageManager dựng EnemySpawner từ cấu hình này.")]
    [SerializeField] private EnemySpawner.Settings stage = new EnemySpawner.Settings();
    [Tooltip("Các database mission (Daily, Weekly, từng event); để trống thì không có mission.")]
    [SerializeField] private MissionManager.Settings missions = new MissionManager.Settings();
    [Tooltip("URL hỏi giờ server cho các tính năng chọn ClockSource.Server (Mission event, login check-in).")]
    [SerializeField] private ClockManager.Settings clock = new ClockManager.Settings();
    [Tooltip("Các lịch đăng nhập (7 ngày, chu kỳ, event); thứ tự là thứ tự popup tự mở. Để trống thì không có quà đăng nhập.")]
    [SerializeField] private LoginCheckInManager.Settings loginCheckIn = new LoginCheckInManager.Settings();
    [Tooltip("Các gói bán trong shop (tài nguyên, miễn phí, tiền thật). UI ghép thẻ với gói theo ID.")]
    [SerializeField] private ShopManager.Settings shop = new ShopManager.Settings();
    [Tooltip("Các banner gacha (tướng, trang bị); thứ tự là thứ tự trên màn Gacha. Tỉ lệ nằm trong từng asset banner.")]
    [SerializeField] private GachaManager.Settings gacha = new GachaManager.Settings();

    #endregion

    [Header("Gameplay Databases")]

    #region Gameplay Databases

    [SerializeField] private CharacterDatabaseSO characterDatabase; // Danh mục template nhân vật.
    [SerializeField] private LevelDatabaseSO levelDatabase; // Config level vĩnh viễn theo nhân vật và config level trận.
    [SerializeField] private EquipmentDatabaseSO equipmentDatabase; // Mẫu đồ, set, luật trang bị và đồ khởi đầu.
    [SerializeField] private LootDatabaseSO lootDatabase; // Catalog EXP/vàng: prefab, sprite, lượng, tỉ lệ.
    [SerializeField] private SkillDatabaseSO skillDatabase; // Catalog skill gameplay và quyền xuất hiện trong bảng thưởng.
    [Tooltip("Danh mục tất cả màn và màn mặc định; StageManager chọn Stage từ database này.")]
    [SerializeField] private StageDatabaseSO stageDatabase;

    #endregion

    #region Game Flow Settings

    [SerializeField, FormerlySerializedAs("autoStartRun")] private bool autoStartBattle = false; // Loading → Home; chỉ vào trận khi nút Play gọi StartBattle.

    #endregion

    #region Fields

    private ISaveMaintenance saves; // Thử ghi lại save khi ứng dụng pause/mất focus/thoát.
    private IClockSync clockSync; // Hỏi lại giờ server khi ứng dụng quay lại từ nền.

    #endregion

    #region Installation

    /// <summary>Kiểm tra cấu hình → bind dữ liệu → bind Manager theo chiều phụ thuộc → thứ tự lifecycle (danh sách).</summary>
    public override void InstallBindings()
    {
        ValidateDependencies();
        BindData();
        BindDomainManagers();
        ConfigureLifecycleOrder();
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
        missions.Validate();
        clock.Validate();
        loginCheckIn.Validate();
        shop.Validate();
        gacha.Validate();
    }

    // Dùng đúng asset đã gán trong Inspector; không tạo bản sao hoặc giữ trạng thái trận trên DataSO.
    // Cấu hình của Manager luôn đi qua class Settings riêng của nó (BindInstance), không dùng WithArguments:
    // Zenject ghép WithArguments theo KIỂU dữ liệu, thêm một tham số cùng kiểu là có thể ghép nhầm mà không báo lỗi.
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
        Container.BindInstance(new CharacterManager.Settings(characterPrefab, damageTextStyle));
        Container.BindInstance(new GameManager.Settings(characterPrefab, autoStartBattle));
        Container.BindInstance(world); // WorldManager.Settings
        Container.BindInstance(stage); // EnemySpawner.Settings, StageManager dựng EnemySpawner từ đây
        Container.BindInstance(missions); // MissionManager.Settings
        Container.BindInstance(clock); // ClockManager.Settings
        Container.BindInstance(loginCheckIn); // LoginCheckInManager.Settings
        Container.BindInstance(shop); // ShopManager.Settings
        Container.BindInstance(gacha); // GachaManager.Settings
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
    }

    /// <summary>
    /// Một dòng cho mỗi miền, theo chiều phụ thuộc từ dưới lên (Save → … → Game). Mỗi Manager chỉ lộ contract
    /// trong <c>XxxContracts.cs</c> của nó; system con không có binding nào.
    /// <c>NonLazy</c>: tạo Manager ngay lúc khởi động thay vì chờ có ai inject. Cần vì nhiều Manager đăng ký event
    /// trong constructor (ví dụ Battle nghe quái chết); nếu tạo trễ thì bỏ lỡ sự kiện hoặc không bao giờ được tạo.
    /// </summary>
    private void BindDomainManagers()
    {
        Container.BindInterfacesTo<SaveManager>().AsSingle().NonLazy(); // Save: đọc/ghi + migration
        Container.BindInterfacesTo<ClockManager>().AsSingle().NonLazy(); // Clock: giờ máy / giờ server, không phụ thuộc miền nào
        Container.BindInterfacesTo<ProgressionManager>().AsSingle().NonLazy(); // Progression: ví, level vĩnh viễn, trang bị, mốc vượt màn
        Container.BindInterfacesTo<PoolManager>().AsSingle().NonLazy(); // Pool: LeanPool + holder
        Container.BindInterfacesTo<SimulationManager>().AsSingle().NonLazy(); // Simulation: job di chuyển + lưới không gian
        Container.BindInterfacesTo<CharacterManager>().AsSingle().NonLazy(); // Character
        Container.BindInterfacesTo<WorldManager>().AsSingle().NonLazy(); // World: map nền
        Container.BindInterfacesTo<CombatManager>().AsSingle().NonLazy(); // Combat: skill, đạn, effect, VFX

        Container.BindInterfacesTo<StageManager>().AsSingle().NonLazy(); // Stage: wave, đội hình, boss
        Container.BindInterfacesTo<BattleManager>().AsSingle().NonLazy(); // Battle: đồng hồ, level trận, loot, chọn skill
        Container.BindInterfacesTo<GameManager>().AsSingle().NonLazy(); // Game: luồng scene, pause, prewarm
        Container.BindInterfacesTo<LoginCheckInManager>().AsSingle().NonLazy(); // Login Check-in: quà đăng nhập theo lịch, chỉ UI dùng
        Container.BindInterfacesTo<ShopManager>().AsSingle().NonLazy(); // Shop: gói mua bằng tài nguyên/tiền thật, chỉ UI dùng
        Container.BindInterfacesTo<GachaManager>().AsSingle().NonLazy(); // Gacha: banner tướng/trang bị, pity, chỉ UI dùng
        Container.BindInterfacesTo<MissionManager>().AsSingle().NonLazy(); // Mission: nghe mọi miền dưới, chỉ UI dùng
        Container.BindInterfacesTo<NotificationManager>().AsSingle().NonLazy(); // Notification: tầng trên cùng, chấm báo cho UI
    }

    // Binding của tầng UI (ScreenTransitionController…) nằm ở UIInstaller, chạy sau installer này.
    #endregion

    #region Lifecycle Order

    // Đọc từ trên xuống = thứ tự chạy. Thêm Manager có lifecycle = chèn một dòng đúng chỗ, không cần nghĩ số.
    // Thứ tự bên trong một miền do Manager của nó tự gọi. Số thứ tự Zenject do ConfigureLifecycleOrder sinh ra.

    // Battle, Combat, Stage… không có Initialize: chúng đăng ký event ngay trong constructor.
    private static readonly Type[] InitializeOrder =
    {
        typeof(SaveManager),        // Mở save + migration trước mọi miền đọc dữ liệu.
        typeof(ClockManager),       // Bắt đầu hỏi giờ server sớm nhất có thể; trả lời đến sau thì Mission tự mở nhóm.
        typeof(ProgressionManager), // Ví, level vĩnh viễn, trang bị, mốc vượt màn.
        typeof(CharacterManager),   // Nhân vật đang chọn (đọc Progression).
        typeof(WorldManager),       // Map ban đầu trước khi GameManager vào Home.
        typeof(MissionManager),     // Đọc tiến độ sau Progression, trước khi GameManager vào Home.
        typeof(LoginCheckInManager), // Đọc tiến độ lịch đăng nhập trước khi vào Home (popup tự mở dựa vào nó).
        typeof(ShopManager),        // Đọc số lần đã mua các gói trước khi vào Home.
        typeof(GachaManager),       // Đọc bộ đếm pity trước khi vào Home.
        typeof(NotificationManager), // Sau mọi miền nó đọc: báo mọi chấm ở Tick đầu tiên.
        typeof(GameManager),        // Prewarm/spawn sau khi mọi miền đã sẵn sàng.
    };

    private static readonly Type[] TickOrder =
    {
        typeof(BattleManager),         // Đồng hồ trận đầu frame.
        typeof(CombatManager),      // Bên trong: effect → skill → đạn.
        typeof(StageManager),
        typeof(CharacterManager),
        typeof(WorldManager),       // Nền theo Player sau khi nhân vật đã tick.
        typeof(NotificationManager), // Cuối frame: báo UI các chấm đã đổi, mỗi giây kiểm tra sang ngày mới.
    };

    private static readonly Type[] LateTickOrder =
    {
        typeof(SimulationManager),  // Áp dụng di chuyển sau khi mọi Tick đã gửi lệnh.
        typeof(WorldManager),       // Camera bám Player sau khi di chuyển đã áp dụng.
        typeof(StageManager),       // Bù chuyển động camera cho đội hình sau khi đã di chuyển.
        typeof(CharacterManager),   // Animation visual nhân vật (CharacterVisualHandle).
        typeof(CombatManager),      // Vùng/lưỡi skill bám chủ, rồi VFX kiểm tra đã phát xong.
        typeof(BattleManager),         // Nhặt loot sau khi đã áp dụng chuyển động.
        // UI (SkillChoiceController, GameplayController) chạy sau toàn bộ danh sách này; thứ tự ở UIInstaller.
    };

    // Dừng game → thu hồi entity/VFX → trả pool → giải phóng save và buffer Jobs.
    private static readonly Type[] DisposeOrder =
    {
        typeof(NotificationManager), // Đầu tiên: tầng trên cùng, gỡ listener khỏi mọi miền nó đọc khi chúng còn sống.
        typeof(MissionManager),     // Gỡ listener khỏi mọi miền dưới khi chúng còn sống.
        typeof(LoginCheckInManager), // Gỡ listener khỏi Clock khi Clock còn sống.
        typeof(ShopManager),        // Gỡ listener khỏi Clock khi Clock còn sống.
        typeof(GameManager),        // Hủy prewarm, gỡ listener, trả timeScale.
        typeof(StageManager),
        typeof(WorldManager),       // Sau Stage (Stage dùng World), trước Character (World nghe Player) và Pool (chunk về pool).
        typeof(BattleManager),         // Trước Character (Battle nghe PlayerChanged/CharacterDied) và Pool (pickup về pool).
        typeof(CharacterManager),   // Thu hồi mọi nhân vật trước khi dọn Combat và Pool. Thứ tự nội bộ xem CharacterManager.Dispose.
        typeof(CombatManager),      // Nhân vật despawn còn gọi Combat dọn skill/effect; skill, đạn, VFX còn trả visual về Pool.
        typeof(PoolManager),
        typeof(ProgressionManager), // Sau mọi miền gameplay (còn đọc/ghi ví khi dọn).
        typeof(ClockManager),       // Sau Mission (còn nghe Changed): hủy request hỏi giờ đang chờ.
        typeof(SaveManager),        // Thử ghi lần cuối phần còn chờ.
        typeof(SimulationManager),  // Cuối: mọi miền đã gỡ đăng ký movement/hitbox. Bên trong: movement rồi spatial.
    };

    /// <summary>
    /// Chuyển 4 danh sách thành thứ tự của Zenject. Số do vị trí trong danh sách sinh ra, không ai viết tay.
    /// Zenject chạy Initialize/Tick/LateTick số nhỏ trước nhưng Dispose số lớn trước, nên Dispose đảo ngược số.
    /// </summary>
    private void ConfigureLifecycleOrder()
    {
        for (int i = 0; i < InitializeOrder.Length; i++)
            Container.BindInitializableExecutionOrder(InitializeOrder[i], (i + 1) * 10);
        for (int i = 0; i < TickOrder.Length; i++)
            Container.BindTickableExecutionOrder(TickOrder[i], (i + 1) * 10);
        for (int i = 0; i < LateTickOrder.Length; i++)
            Container.BindLateTickableExecutionOrder(LateTickOrder[i], (i + 1) * 10);
        for (int i = 0; i < DisposeOrder.Length; i++)
            Container.BindDisposableExecutionOrder(DisposeOrder[i], (DisposeOrder.Length - i) * 10);
    }

    #endregion

    #region Application Save Lifecycle

    // Resolve sau khi cài đặt xong; lưu dữ liệu khi ứng dụng mất focus, tạm dừng hoặc thoát.
    public override void Start()
    {
        saves = Container.Resolve<ISaveMaintenance>();
        clockSync = Container.Resolve<IClockSync>();
    }

    // Chỉ hỏi lại giờ khi thật sự quay lại từ nền (pause), không phải mỗi lần mất focus (kéo thanh thông báo).
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            saves?.SaveIfDirty();
        else
            clockSync?.Resync();
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
