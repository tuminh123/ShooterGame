using System;
using UnityEngine;
using Zenject;

// * Class thuần quản lý player; Zenject gọi Initialize để tạo nhân vật khi vào game.
public sealed class PlayerManager : IInitializable, IDisposable
{

    #region Player Configuration & Dependencies

    // * Cấu hình từ installer, phân biệt prefab với instance đang chơi.
    public sealed class Settings
    {
        public readonly Player Prefab;  // * Prefab nhân vật kéo vào GameSceneInstaller.

        // * Chỉ lưu cấu hình, chưa tạo GameObject trong bước dựng container.
        public Settings(Player prefab)
        {
            Prefab = prefab;
        }
    }

    private readonly DiContainer container;  // * Tạo prefab và inject dependency trước khi bật.
    private readonly Settings settings;      // * Cấu hình prefab cho scene hiện tại.
    private readonly SaveManager saves;
    private readonly LevelManager levels;
    private readonly PlayerEquipmentService equipment;  // * Cung cấp bonus và thông báo khi trang bị thay đổi.
    private bool disposed;                   // * Chặn tạo player sau khi dịch vụ kết thúc.

    #endregion

    #region Current Player & Events

    public Player CurrentPlayer { get; private set; }  // * Player đang chơi hoặc null.
    public CharacterRuntimeData RuntimeData => CurrentPlayer != null ? CurrentPlayer.RuntimeData : null;
    public Transform PlayerTransform => CurrentPlayer != null ? CurrentPlayer.transform : null;  // * Mục tiêu world-space.
    public event Action<Player> PlayerChanged;  // * Player mới, hoặc null khi player bị gỡ.

    #endregion

    #region Service Initialization

    // * Nhận dependency từ Zenject; constructor chưa tạo nhân vật.
    public PlayerManager(DiContainer container, Settings settings, SaveManager saves,
        LevelManager levels, PlayerEquipmentService equipment)
    {
        this.container = container;
        this.settings = settings;
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.levels.CharacterLevelChanged += OnCharacterLevelChanged;
        this.levels.RunLevelChanged += OnRunLevelChanged;
        this.levels.RunReset += RefreshRuntime;
        this.equipment.EquipmentChanged += RefreshRuntime;
    }

    // * Zenject gọi khi scene bắt đầu; vị trí xuất hiện luôn là Vector3.zero.
    public void Initialize()
    {
        SpawnPlayer();
    }

    #endregion

    #region Player Lifecycle

    // * Chỉ tạo khi chưa có player; gọi lại trả về cùng instance để tránh tạo trùng.
    public Player SpawnPlayer()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PlayerManager));
        if (CurrentPlayer != null)
            return CurrentPlayer;
        if (settings == null || settings.Prefab == null)
            throw new InvalidOperationException("Assign Player Prefab in GameSceneInstaller.");
        CharacterType type = saves.GetSnapshot().selectedCharacterType;
        if (type == CharacterType.None)
            type = settings.Prefab.CharacterType;
        // * Hiện installer có một prefab; không gán data của nhân vật khác lên cùng hình ảnh.
        if (type != settings.Prefab.CharacterType)
            throw new InvalidOperationException($"The selected character {type} needs a matching Player Prefab.");
        var runtime = new CharacterRuntimeData(levels.GetCharacter(type),
            levels.GetPermanentLevel(type), levels.RunLevel,
            levels.GetPermanentBonus(type), levels.RunBonus, equipment.TotalBonus);
        CurrentPlayer = container.InstantiatePrefabForComponent<Player>(
            settings.Prefab, Vector3.zero, Quaternion.identity, null);
        CurrentPlayer.InitializeRuntime(runtime);
        CurrentPlayer.Destroyed += OnPlayerDestroyed;
        PlayerChanged?.Invoke(CurrentPlayer);
        return CurrentPlayer;
    }

    // * Gỡ tham chiếu và thông báo trước khi hủy; có thể SpawnPlayer lại để tạo nhân vật mới.
    public void RemovePlayer()
    {
        Player previous = CurrentPlayer;
        if (previous == null)
            return;
        previous.Destroyed -= OnPlayerDestroyed;
        CurrentPlayer = null;
        PlayerChanged?.Invoke(null);
        previous.gameObject.SetActive(false);
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(previous.gameObject);
        else
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
    }

    // * Đồng bộ trạng thái nếu có hệ thống khác Destroy player trực tiếp.
    private void OnPlayerDestroyed(Player player)
    {
        if (!ReferenceEquals(CurrentPlayer, player))
            return;
        player.Destroyed -= OnPlayerDestroyed;
        CurrentPlayer = null;
        PlayerChanged?.Invoke(null);
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
        runtime.Recalculate(levels.GetPermanentLevel(type), levels.RunLevel,
            levels.GetPermanentBonus(type), levels.RunBonus, equipment.TotalBonus);
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
    }

    #endregion

}
