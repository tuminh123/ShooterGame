using System;
using Terresquall;
using UnityEngine;
using Zenject;

// * Class thuần quản lý Player; GameManager gọi SpawnPlayer/RemovePlayer theo luồng trận, Zenject quản lý Dispose.
public sealed class PlayerManager : IInitializable, IDisposable, IPlayerSystem, IPlayerReader, IPlayerSelection, IPlayerLifecycle
{

    #region Player Configuration & Dependencies

    // * Cấu hình từ installer, phân biệt prefab với instance đang chơi.
    public sealed class Settings
    {
        public readonly CharacterType DefaultType; // * Nhân vật mặc định khi save chưa chọn; hình ảnh được tra từ CharacterDataSO.


        // * Chỉ lưu cấu hình, chưa tạo GameObject trong bước dựng container.
        public Settings(CharacterType defaultType)
        {
            DefaultType = defaultType;
        }
    }
    private readonly ICharacterSpawn characterSpawn; // * Lấy cùng prefab vỏ với Enemy, gắn dữ liệu/hành vi trước khi bật.
    private readonly ICharacterDespawn characterDespawn;
    private readonly VirtualJoystick joystick; // * Truyền cho PlayerController thuần; manager không đọc input.
    private readonly Settings settings; // * Loại Player mặc định khi hồ sơ chưa chọn nhân vật.
    private readonly ISaveReader saveReader; // Chỉ đọc/ghi lựa chọn nhân vật qua key riêng.
    private readonly ISaveWriter saveWriter;
    private PlayerSelectionSaveData selection;
    private readonly ICharacterLevelCatalog levelCatalog; // * Tính bonus Player và thông báo khi level thay đổi.
    private readonly IPermanentLevelReader permanentLevels;
    private readonly IRunProgress levelsRunProgress;
    private readonly IEquipmentReader equipment;  // * Cung cấp bonus và thông báo khi trang bị thay đổi.
    private bool disposed;                   // * Chặn tạo player sau khi dịch vụ kết thúc.

    #endregion

    #region Current Player & Events

    public CharacterView CurrentPlayer { get; private set; } // * Vỏ đang gắn hành vi Player, không phải component Player riêng.
    public CharacterRuntimeData RuntimeData => CurrentPlayer != null ? CurrentPlayer.RuntimeData : null;
    public Transform PlayerTransform => CurrentPlayer != null ? CurrentPlayer.transform : null;  // * Mục tiêu world-space.
    public event Action<CharacterView> PlayerChanged; // * Vỏ Player mới hoặc null; camera/nền không phụ thuộc kiểu controller.
    public CharacterType SelectedCharacterType
    {
        get { EnsureSelectionLoaded(); return selection.selectedCharacterType; }
    }

    #endregion

    public void Initialize()
    {
        if (!saveReader.IsLoaded || selection != null) return;
        selection = saveReader.Load(GameConstants.SaveKeys.PlayerSelection, new PlayerSelectionSaveData(), ValidateSelectionData);
    }

    internal static void ValidateSelectionData(PlayerSelectionSaveData value)
    {
        if (value == null || !Enum.IsDefined(typeof(CharacterType), value.selectedCharacterType))
            throw new InvalidOperationException("Selected character type is not defined.");
    }

    // Ghi lựa chọn cho lần spawn tiếp theo; không đổi nhân vật giữa một trận đang chơi.
    public bool SelectCharacter(CharacterType type)
    {
        EnsureSelectionLoaded();
        if (type == CharacterType.None || levelCatalog.GetCharacter(type).Team != CharacterTeam.Player)
            throw new ArgumentOutOfRangeException(nameof(type));
        selection.selectedCharacterType = type;
        return saveWriter.Save(GameConstants.SaveKeys.PlayerSelection, selection.Clone());
    }

    private void EnsureSelectionLoaded()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PlayerManager));
        if (selection == null || !saveReader.IsLoaded) throw new InvalidOperationException("Player selection has not loaded.");
    }

    #region Service Initialization

    // * Nhận dependency từ Zenject; constructor chưa tạo nhân vật.
    public PlayerManager(
        ICharacterFactory characterFactory,
        VirtualJoystick joystick,
        Settings settings,
        ISaveService saveService,
        ILevelService levelService,
        IEquipmentService equipmentService)
    {
        this.characterSpawn = characterFactory.GetCharacterSpawn();
        this.characterDespawn = characterFactory.GetCharacterDespawn();
        this.joystick = joystick;
        this.settings = settings;
        this.saveReader = saveService.GetSaveReader() ?? throw new ArgumentNullException(nameof(saveService));
        this.saveWriter = saveService.GetSaveWriter() ?? throw new ArgumentNullException(nameof(saveService));
        this.levelCatalog = levelService.GetCharacterLevelCatalog() ?? throw new ArgumentNullException(nameof(levelService));
        this.permanentLevels = levelService.GetPermanentLevelReader() ?? throw new ArgumentNullException(nameof(levelService));
        this.levelsRunProgress = levelService.GetRunProgress() ?? throw new ArgumentNullException(nameof(levelService));
        this.equipment = equipmentService.GetEquipmentReader() ?? throw new ArgumentNullException(nameof(equipmentService));
        this.permanentLevels.CharacterLevelChanged += OnCharacterLevelChanged;
        this.levelsRunProgress.RunLevelChanged += OnRunLevelChanged;
        this.levelsRunProgress.RunReset += RefreshRuntime;
        this.equipment.EquipmentChanged += RefreshRuntime;
    }

    #endregion

    #region Player Lifecycle

    // * Chỉ tạo khi chưa có player; gọi lại trả về cùng instance để tránh tạo trùng.
    public CharacterView SpawnPlayer()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PlayerManager));
        if (CurrentPlayer != null)
            return CurrentPlayer;
        CharacterType type = SelectedCharacterType;
        if (type == CharacterType.None)
            type = settings.DefaultType;
        CharacterDataSO template = levelCatalog.GetCharacter(type);
        if (template.Team != CharacterTeam.Player)
            throw new InvalidOperationException($"Selected character {type} must belong to the Player team.");
        StatBonus bonus = permanentLevels.GetPermanentBonus(type) + levelsRunProgress.RunBonus + equipment.TotalBonus;
        CurrentPlayer = characterSpawn.Spawn(template, bonus, new PlayerController(joystick), Vector3.zero);
        if (CurrentPlayer == null)
            throw new InvalidOperationException("Could not spawn the Player shell.");
        CurrentPlayer.Released += OnPlayerReleased;
        PlayerChanged?.Invoke(CurrentPlayer);
        return CurrentPlayer;
    }

    // * Gỡ tham chiếu và thông báo trước khi trả vỏ về pool; SpawnPlayer sau đó tạo runtime/hành vi mới.
    public void RemovePlayer()
    {
        CharacterView previous = CurrentPlayer;
        if (previous == null)
            return;
        DetachPlayer(previous);
        CurrentPlayer = null;
        PlayerChanged?.Invoke(null);
        characterDespawn.Despawn(previous);
    }

    // * Đồng bộ trạng thái nếu có hệ thống khác Destroy player trực tiếp.
    private void OnPlayerReleased(CharacterView player)
    {
        if (!ReferenceEquals(CurrentPlayer, player))
            return;
        DetachPlayer(player);
        CurrentPlayer = null;
        PlayerChanged?.Invoke(null);
    }

    private void DetachPlayer(CharacterView player)
    {
        player.Released -= OnPlayerReleased;
    }

    #endregion

    #region Runtime Stat Updates

    private void OnCharacterLevelChanged(CharacterType type, int previousLevel, int newLevel)
    {
        if (RuntimeData != null && RuntimeData.CharacterType == type)
            RefreshRuntime();
    }

    private void OnRunLevelChanged(int previousLevel, int newLevel)
    {
        RefreshRuntime();
    }

    private void RefreshRuntime()
    {
        CharacterRuntimeData runtime = RuntimeData;
        if (disposed || runtime == null)
            return;
        CharacterType type = runtime.CharacterType;
        runtime.SetExternalBonus(permanentLevels.GetPermanentBonus(type) + levelsRunProgress.RunBonus + equipment.TotalBonus);
    }

    #endregion

    #region Scene Cleanup

    // * Dọn player và subscription khi scene kết thúc; gọi lại vẫn an toàn.
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        permanentLevels.CharacterLevelChanged -= OnCharacterLevelChanged;
        levelsRunProgress.RunLevelChanged -= OnRunLevelChanged;
        levelsRunProgress.RunReset -= RefreshRuntime;
        equipment.EquipmentChanged -= RefreshRuntime;
        RemovePlayer();
        PlayerChanged = null;
        selection = null;
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IPlayerReader GetPlayerReader() => this;

    public IPlayerSelection GetPlayerSelection() => this;

    public IPlayerLifecycle GetPlayerLifecycle() => this;

    #endregion

}
