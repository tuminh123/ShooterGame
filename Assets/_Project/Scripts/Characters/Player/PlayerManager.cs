using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Terresquall;
using UnityEngine;
using Zenject;

// * Class thuần quản lý player; Zenject gọi Initialize để tạo nhân vật khi vào game.
public sealed class PlayerManager : IInitializable, IDisposable
{

    #region Player Configuration & Dependencies

    // * Cấu hình từ installer, phân biệt prefab với instance đang chơi.
    public sealed class Settings
    {
        public readonly CharacterType DefaultType; // * Nhân vật mặc định khi save chưa chọn; hình ảnh được tra từ CharacterDataSO.

        public readonly float DamageFlashDuration;
        public readonly Color DamageFlashColor;

        // * Chỉ lưu cấu hình, chưa tạo GameObject trong bước dựng container.
        public Settings(CharacterType defaultType, float damageFlashDuration, Color damageFlashColor)
        {
            DefaultType = defaultType;
            DamageFlashDuration = Mathf.Max(0.01f, damageFlashDuration);
            DamageFlashColor = damageFlashColor;
        }
    }

    private readonly CharacterFactory factory; // * Lấy cùng prefab vỏ với Enemy, gắn dữ liệu/hành vi trước khi bật.
    private readonly VirtualJoystick joystick; // * Truyền cho PlayerController thuần; manager không đọc input.
    private readonly Settings settings; // * Loại Player mặc định khi hồ sơ chưa chọn nhân vật.
    private readonly SaveManager saves; // * Đọc nhân vật đã chọn và tiến trình đã load, không ghi HP của lượt chơi.
    private readonly LevelManager levels; // * Tính bonus Player và thông báo khi level thay đổi.
    private readonly PlayerEquipmentService equipment;  // * Cung cấp bonus và thông báo khi trang bị thay đổi.
    private IReadOnlyList<SpriteRenderer> playerSprites;
    private Color[] originalSpriteColors;
    private CancellationTokenSource damageFlashCancellation;
    private bool disposed;                   // * Chặn tạo player sau khi dịch vụ kết thúc.

    #endregion

    #region Current Player & Events

    public CharacterView CurrentPlayer { get; private set; } // * Vỏ đang gắn hành vi Player, không phải component Player riêng.
    public CharacterRuntimeData RuntimeData => CurrentPlayer != null ? CurrentPlayer.RuntimeData : null;
    public Transform PlayerTransform => CurrentPlayer != null ? CurrentPlayer.transform : null;  // * Mục tiêu world-space.
    public event Action<CharacterView> PlayerChanged; // * Vỏ Player mới hoặc null; camera/nền không phụ thuộc kiểu controller.

    #endregion

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

    // * Zenject gọi khi scene bắt đầu; vị trí xuất hiện luôn là Vector3.zero.
    public void Initialize()
    {
        // Keep the scene UI available so a failed save can be reset explicitly.
        if (saves.IsLoaded)
            SpawnPlayer();
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
        CharacterType type = saves.GetSnapshot().selectedCharacterType;
        if (type == CharacterType.None)
            type = settings.DefaultType;
        CharacterDataSO template = levels.GetCharacter(type);
        if (template.Team != CharacterTeam.Player)
            throw new InvalidOperationException($"Selected character {type} must belong to the Player team.");
        var runtime = new CharacterRuntimeData(template,
            levels.GetPermanentLevel(type), levels.RunLevel,
            levels.GetPermanentBonus(type), levels.RunBonus, equipment.TotalBonus);
        CurrentPlayer = factory.Spawn(template, runtime, new PlayerController(joystick), Vector3.zero);
        if (CurrentPlayer == null)
            throw new InvalidOperationException("Could not spawn the Player shell.");
        playerSprites = CurrentPlayer.Sprites;
        originalSpriteColors = new Color[playerSprites.Count];
        for (int i = 0; i < playerSprites.Count; i++)
            originalSpriteColors[i] = playerSprites[i].color;
        CurrentPlayer.DamageReceived += OnDamageReceived;
        CurrentPlayer.Deactivated += StopDamageFlash;
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
        player.DamageReceived -= OnDamageReceived;
        player.Deactivated -= StopDamageFlash;
        player.Released -= OnPlayerReleased;
        StopDamageFlash();
        playerSprites = null;
        originalSpriteColors = null;
    }

    #endregion

    #region Player Damage Feedback

    private void OnDamageReceived(DamageInfo damage, DamageResult result)
    {
        if (disposed || CurrentPlayer == null || !CurrentPlayer.isActiveAndEnabled || !result.Accepted)
            return;
        StopDamageFlash();
        for (int i = 0; i < playerSprites.Count; i++)
        {
            if (playerSprites[i] == null)
                continue;
            Color color = originalSpriteColors[i] * settings.DamageFlashColor;
            color.a = originalSpriteColors[i].a;
            playerSprites[i].color = color;
        }
        damageFlashCancellation = new CancellationTokenSource();
        RestoreAfterFlashAsync(damageFlashCancellation).Forget();
    }

    private async UniTask RestoreAfterFlashAsync(CancellationTokenSource cancellation)
    {
        bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(settings.DamageFlashDuration),
            ignoreTimeScale: false, cancellationToken: cancellation.Token).SuppressCancellationThrow();
        // An older task must never restore the colors of a newer flash or another spawn.
        if (!cancelled && ReferenceEquals(damageFlashCancellation, cancellation))
            StopDamageFlash();
    }

    private void StopDamageFlash()
    {
        CancellationTokenSource previous = damageFlashCancellation;
        damageFlashCancellation = null;
        if (previous != null)
        {
            previous.Cancel();
            previous.Dispose();
        }
        if (playerSprites == null || originalSpriteColors == null)
            return;
        for (int i = 0; i < playerSprites.Count; i++)
            if (playerSprites[i] != null)
                playerSprites[i].color = originalSpriteColors[i];
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
