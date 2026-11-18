using System;
using System.Collections.Generic;
using Terresquall;
using UnityEngine;

/// <summary>
/// Quản lý Player: spawn/thu hồi theo luồng trận (GameManager gọi), áp bonus từ level vĩnh viễn và trang bị,
/// và lưu nhân vật đang chọn.
/// </summary>
/// <remarks>
/// Do <see cref="CharacterManager"/> tạo; miền khác dùng qua <see cref="IPlayerReader"/>/<see cref="IPlayerLifecycle"/>.
/// Bonus level trong trận không thuộc đây: miền Run (nằm trên Character) tự thêm một nguồn riêng vào
/// <see cref="StatSheet"/> của Player khi nhận <see cref="PlayerChanged"/> (xem RunLevel).
/// </remarks>
public sealed class PlayerSystem : IDisposable, IPlayerReader, IPlayerLifecycle
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
    private readonly ISaveStore saves; // Chỉ đọc/ghi lựa chọn nhân vật qua key riêng.
    private PlayerSelectionSaveData selection;
    private readonly IReadOnlyDictionary<CharacterType, CharacterDataSO> characters; // Template theo type (CharacterDatabaseSO).
    private readonly IPermanentBonuses permanentBonuses; // * Level vĩnh viễn + trang bị (miền Progression), kèm thông báo khi đổi.
    // Nguồn bonus của Player (level vĩnh viễn + trang bị) trong StatSheet của lượt spawn hiện tại.
    // Cập nhật tại chỗ khi level/trang bị đổi; không cộng dồn thêm nguồn mới.
    private StatModifierLease playerBonus;
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
        selection = saves.Load(GameConstants.SaveKeys.PlayerSelection, new PlayerSelectionSaveData(), PlayerSelectionSaveData.Validate);
    }

    // Ghi lựa chọn cho lần spawn tiếp theo; không đổi nhân vật giữa một trận đang chơi.
    public bool SelectCharacter(CharacterType type)
    {
        EnsureSelectionLoaded();
        if (type == CharacterType.None || GetCharacter(type).Team != CharacterTeam.Player)
            throw new ArgumentOutOfRangeException(nameof(type));
        selection.selectedCharacterType = type;
        return saves.Save(GameConstants.SaveKeys.PlayerSelection, selection.Clone());
    }

    private void EnsureSelectionLoaded()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PlayerSystem));
        if (selection == null || !saves.IsLoaded) throw new InvalidOperationException("Player selection has not loaded.");
    }

    #region Service Initialization

    // * CharacterManager tạo và truyền dependency; constructor chưa tạo nhân vật.
    public PlayerSystem(
        CharacterFactory characterFactory,
        VirtualJoystick joystick,
        Settings settings,
        CharacterDatabaseSO characterDatabase,
        ISaveStore saves,
        IPermanentBonuses permanentBonuses)
    {
        this.characterSpawn = characterFactory ?? throw new ArgumentNullException(nameof(characterFactory));
        this.characterDespawn = characterFactory;
        this.joystick = joystick;
        this.settings = settings;
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        if (characterDatabase == null) throw new ArgumentNullException(nameof(characterDatabase));
        characters = characterDatabase.CreateLookup();
        this.permanentBonuses = permanentBonuses ?? throw new ArgumentNullException(nameof(permanentBonuses));
        this.permanentBonuses.BonusesChanged += RefreshRuntime;
    }

    #endregion

    #region Player Lifecycle

    // * Chỉ tạo khi chưa có player; gọi lại trả về cùng instance để tránh tạo trùng.
    public CharacterView SpawnPlayer()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PlayerSystem));
        if (CurrentPlayer != null)
            return CurrentPlayer;
        CharacterType type = SelectedCharacterType;
        if (type == CharacterType.None)
            type = settings.DefaultType;
        CharacterDataSO template = GetCharacter(type);
        if (template.Team != CharacterTeam.Player)
            throw new InvalidOperationException($"Selected character {type} must belong to the Player team.");
        CharacterView player = characterSpawn.Spawn(template, default, new PlayerController(joystick), Vector3.zero);
        if (player == null)
            throw new InvalidOperationException("Could not spawn the Player shell.");
        try
        {
            // Bonus là một nguồn có lease (không phải bonus lúc spawn), để RefreshRuntime cập nhật tại chỗ.
            // Thêm ngay sau Spawn, khi Player chưa nhận sát thương nên HP vẫn đầy theo max mới.
            playerBonus = player.RuntimeData.Stats.AddSource(CurrentBonus(type).ToModifiers());
        }
        catch
        {
            // Bonus không hợp lệ (NaN…): không để lại Player thiếu bonus trên scene.
            characterDespawn.Despawn(player);
            throw;
        }
        CurrentPlayer = player;
        CurrentPlayer.Released += OnPlayerReleased;
        PlayerChanged?.Invoke(CurrentPlayer); // Phát sau khi bonus đã áp, để UI đọc đúng chỉ số cuối.
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
        // Không Dispose: gỡ nguồn sẽ tính lại chỉ số và phát Changed trên một Player đang bị thu hồi.
        // Runtime về pool sẽ xóa mọi nguồn và vô hiệu hóa lease này.
        playerBonus = null;
    }

    #endregion

    #region Runtime Stat Updates

    // Level vĩnh viễn hoặc trang bị đổi: thay nguồn bonus của Player tại chỗ (không cộng thêm nguồn mới).
    private void RefreshRuntime()
    {
        CharacterRuntimeData runtime = RuntimeData;
        if (disposed || runtime == null || playerBonus == null)
            return;
        playerBonus.Update(CurrentBonus(runtime.CharacterType).ToModifiers());
    }

    // Tổng bonus cộng thẳng của Player: level vĩnh viễn + trang bị (level trận là nguồn riêng của miền Run).
    private StatBonus CurrentBonus(CharacterType type) => permanentBonuses.GetBonus(type);

    // Type không có trong database là lỗi cấu hình (save cũ hoặc enum mới chưa có asset): báo rõ thay vì spawn sai.
    private CharacterDataSO GetCharacter(CharacterType type)
    {
        if (!characters.TryGetValue(type, out CharacterDataSO character))
            throw new KeyNotFoundException($"Unknown character type: {type}.");
        return character;
    }

    #endregion

    #region Scene Cleanup

    // * Dọn player và subscription khi scene kết thúc; gọi lại vẫn an toàn.
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        permanentBonuses.BonusesChanged -= RefreshRuntime;
        RemovePlayer();
        PlayerChanged = null;
        selection = null;
    }

    #endregion
}
