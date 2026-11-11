using System;
using Terresquall;
using UnityEngine;
using Zenject;

// * Class thuần quản lý Player; GameManager gọi SpawnPlayer/RemovePlayer theo luồng trận, Zenject quản lý Dispose.
public sealed class PlayerManager : IInitializable, IDisposable
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

    private readonly CharacterFactory factory; // * Lấy cùng prefab vỏ với Enemy, gắn dữ liệu/hành vi trước khi bật.
    private readonly VirtualJoystick joystick; // * Truyền cho PlayerController thuần; manager không đọc input.
    private readonly Settings settings; // * Loại Player mặc định khi hồ sơ chưa chọn nhân vật.
    private readonly SaveManager saves; // Chỉ đọc/ghi lựa chọn nhân vật qua key riêng.
    private PlayerSelectionSaveData selection;
    private readonly LevelManager levels; // * Tính bonus Player và thông báo khi level thay đổi.
    private readonly PlayerEquipmentService equipment;  // * Cung cấp bonus và thông báo khi trang bị thay đổi.
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
        if (!saves.IsLoaded || selection != null) return;
        selection = saves.Load(GameConstants.SaveKeys.PlayerSelection, new PlayerSelectionSaveData(), ValidateSelectionData);
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
        if (type == CharacterType.None || levels.GetCharacter(type).Team != CharacterTeam.Player)
            throw new ArgumentOutOfRangeException(nameof(type));
        selection.selectedCharacterType = type;
        return saves.Save(GameConstants.SaveKeys.PlayerSelection, selection.Clone());
    }

    private void EnsureSelectionLoaded()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PlayerManager));
        if (selection == null || !saves.IsLoaded) throw new InvalidOperationException("Player selection has not loaded.");
    }

    #region Service Initialization

    // * Nhận dependency từ Zenject; constructor chưa tạo nhân vật.
    public PlayerManager(CharacterFactory factory, VirtualJoystick joystick,
        Settings settings, SaveManager saves, LevelManager levels, PlayerEquipmentService equipment)
    {
        this.factory = factory;
        this.joystick = joystick;
        this.settings = settings;
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.levels.CharacterLevelChanged += OnCharacterLevelChanged;
        this.levels.RunLevelChanged += OnRunLevelChanged;
        this.levels.RunReset += RefreshRuntime;
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
        CharacterDataSO template = levels.GetCharacter(type);
        if (template.Team != CharacterTeam.Player)
            throw new InvalidOperationException($"Selected character {type} must belong to the Player team.");
        StatBonus bonus = levels.GetPermanentBonus(type) + levels.RunBonus + equipment.TotalBonus;
        CurrentPlayer = factory.Spawn(template, bonus, new PlayerController(joystick), Vector3.zero);
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
        factory.Despawn(previous);
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
        runtime.SetExternalBonus(levels.GetPermanentBonus(type) + levels.RunBonus + equipment.TotalBonus);
    }

    #endregion

    #region Scene Cleanup

    // * Dọn player và subscription khi scene kết thúc; gọi lại vẫn an toàn.
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        levels.CharacterLevelChanged -= OnCharacterLevelChanged;
        levels.RunLevelChanged -= OnRunLevelChanged;
        levels.RunReset -= RefreshRuntime;
        equipment.EquipmentChanged -= RefreshRuntime;
        RemovePlayer();
        PlayerChanged = null;
        selection = null;
    }

    #endregion

}
