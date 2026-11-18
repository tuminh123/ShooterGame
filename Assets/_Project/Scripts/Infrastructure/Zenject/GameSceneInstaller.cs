using System;
using Terresquall;
using UnityEngine;
using Zenject;

// * Điểm cấu hình prefab player, joystick và các dịch vụ class thuần cho scene.
public sealed partial class GameSceneInstaller : MonoInstaller
{
    private ISaveFlush saves;

    #region Scene References

    [SerializeField] private CharacterView characterPrefab; // * Một prefab vỏ duy nhất dùng cho cả Player, Enemy và boss.
    [SerializeField] private CombatAssetsSO combatAssets; // Shared bullet shell, bullet sprites and effect prefabs.
    [SerializeField] private PoolPrewarmSettingsSO poolPrewarmSettings;
    [SerializeField] private CharacterType defaultPlayerType = CharacterType.BlessedGladiator; // * Loại Player khi save chưa chọn nhân vật.
    [SerializeField] private VirtualJoystick joystick;  // * Joystick trên Canvas, inject cho Player được tạo.
    [SerializeField] private string playerSaveFile = GameConstants.SaveKeys.PlayerFile;  // * Tên file save người chơi; không chứa dữ liệu của lượt chơi.
    [Tooltip("Nền lặp theo Player và danh mục map; WorldManager dựng từ cấu hình này.")]
    [SerializeField] private WorldManager.Settings world = new WorldManager.Settings();
    [Tooltip("Vùng sinh quái và AI quái (tách đàn, phản lực); StageManager dựng EnemySpawner từ cấu hình này.")]
    [SerializeField] private EnemySpawner.Settings stage = new EnemySpawner.Settings();

    #endregion

    #region Editor Gizmos

    // Vùng sinh quái trước đây vẽ trên component EnemySpawnManager; giờ cấu hình nằm ở installer nên vẽ tại đây.
    private void OnDrawGizmosSelected() => stage?.DrawGizmos();

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
        BindCombat();
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

    #region Application Save Lifecycle

    // Resolve sau cài đặt; lưu dữ liệu khi ứng dụng mất focus, tạm dừng hoặc thoát.
    public override void Start() => saves = Container.Resolve<ISaveService>().GetSaveFlush();

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
