using System;
using System.Collections.Generic;
using Terresquall;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Character: sở hữu và điều phối spawn/thu hồi nhân vật, danh sách nhân vật, Player,
/// hiển thị quái theo camera và tìm mục tiêu. Đây là class <b>duy nhất</b> của Character được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// CharacterManager
/// ├── CharacterRuntimePool                  runtime theo lượt spawn
/// ├── CharacterRegistry                     danh sách nhân vật, tick, sự kiện chết, ăn mừng khi thua
/// ├── CharacterSearchFactory → CharacterSearch   tìm mục tiêu (qua Simulation)
/// ├── CharacterContext                      dịch vụ truyền vào mọi vỏ lúc spawn
/// ├── CharacterFactory                      ghép template + runtime + controller + vỏ
/// ├── PlayerSystem                         Player hiện tại, bonus, nhân vật đang chọn
/// ├── EnemyVisibility                    bật/tắt visual quái theo camera
/// └── DamageTextSystem                   số sát thương bay lên (1 mesh, tick trong LateTick)
/// </code>
/// Các system con được tạo bằng <c>new</c> ngay trong constructor, đọc từ trên xuống.
/// Miền khác chỉ thấy các interface trong <c>CharacterContracts.cs</c>.
/// </remarks>
public sealed class CharacterManager :
    DomainManager,
    ICharacterSpawn,
    ICharacterDespawn,
    ICharacterRegistry,
    ICharacterDeathEvents,
    IPlayerReader,
    IPlayerLifecycle,
    IPlayerSelection,
    ICharacterCapacity,
    IInitializable,
    ITickable,
    ILateTickable,
    IDisposable
{
    #region Settings

    /// <summary>Cấu hình của miền Character, installer gán từ Inspector.</summary>
    public sealed class Settings
    {
        /// <summary>Prefab vỏ dùng chung cho Player, quái và boss.</summary>
        public readonly CharacterView CharacterPrefab;

        /// <summary>Kiểu số sát thương bay lên; null = không hiện số.</summary>
        public readonly DamageTextStyleSO DamageTextStyle;

        public Settings(CharacterView characterPrefab, DamageTextStyleSO damageTextStyle)
        {
            CharacterPrefab = characterPrefab != null ? characterPrefab : throw new ArgumentNullException(nameof(characterPrefab));
            DamageTextStyle = damageTextStyle;
        }
    }

    #endregion

    #region Owned Systems

    private readonly CharacterRuntimePool runtimes;
    private readonly CharacterRegistry registry;
    private readonly CharacterFactory factory;
    private readonly PlayerSystem players;
    private readonly EnemyVisibility enemyVisibility;
    private readonly DamageTextSystem damageText; // null khi installer không gán style.
    private readonly CharacterDataSO[] playableCharacters; // Dựng một lần từ database; màn Character chỉ đọc.
    private bool disposed;

    #endregion

    #region Construction

    /// <summary>Dựng miền Character. Thứ tự tạo đi theo chiều phụ thuộc: system nào được dùng thì phải tạo trước.</summary>
    public CharacterManager(
        Settings settings,
        IGameObjectPool pools,
        IMovementRegistration movementRegistration,
        IMovementCommand movementCommand,
        ISpatialRegistration spatialRegistration,
        ISpatialResolution spatialResolution,
        ISpatialQuery spatialQuery,
        ISpatialQueryBuilder queryBuilder,
        VirtualJoystick joystick,
        PlayerSystem.Settings playerSettings,
        CharacterDatabaseSO characterDatabase,
        ISaveStore saves,
        IPermanentBonuses permanentBonuses)
    {
        try
        {
            runtimes = new CharacterRuntimePool();
            registry = new CharacterRegistry();
            var search = new CharacterSearch(new CharacterSearchFactory(spatialResolution, spatialQuery, queryBuilder));
            damageText = settings.DamageTextStyle != null ? new DamageTextSystem(settings.DamageTextStyle) : null;
            var context = new CharacterContext(movementRegistration, movementCommand, spatialRegistration,
                pools, search, runtimes, damageText);
            factory = new CharacterFactory(settings.CharacterPrefab, pools, registry, context);
            players = new PlayerSystem(factory, joystick, playerSettings, characterDatabase, saves, permanentBonuses);
            enemyVisibility = new EnemyVisibility(registry);
            playableCharacters = BuildPlayableList(characterDatabase);
        }
        catch
        {
            // PlayerSystem và EnemyVisibility đăng ký event ngay khi được tạo; lỗi ở bước sau phải gỡ lại.
            Dispose();
            throw;
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>Đọc nhân vật đang chọn từ save. Chạy sau SaveManager (xem ExecutionOrder).</summary>
    public override void Initialize()
    {
        if (disposed) return;
        players.Initialize();
    }

    /// <summary>Tick mọi nhân vật (controller rồi skill vốn có). Chạy sau Combat và Stage trong frame.</summary>
    public override void Tick()
    {
        if (disposed) return;
        registry.Tick();
    }

    /// <summary>Cập nhật animation visual của mọi nhân vật, rồi số sát thương, sau khi di chuyển đã áp dụng (xem Lifecycle Order).</summary>
    public override void LateTick()
    {
        if (disposed) return;
        registry.LateTick();
        damageText?.LateTick();
    }

    #endregion

    #region ICharacterSpawn, ICharacterDespawn, ICharacterCapacity

    /// <inheritdoc/>
    public CharacterView Spawn(CharacterDataSO template, StatBonus bonus, ICharacterController controller, Vector3 position)
        => factory.Spawn(template, bonus, controller, position);

    /// <inheritdoc/>
    public void Despawn(CharacterView character) => factory.Despawn(character);

    /// <inheritdoc/>
    // Dọn trận (về Home, chơi lại): xóa cả số đang bay, không để số đứng yên trên màn Home.
    public void DespawnAll()
    {
        factory.DespawnAll();
        damageText?.Clear();
    }

    /// <inheritdoc/>
    public void PrewarmRuntimes(int count) => runtimes.Prewarm(count);

    /// <inheritdoc/>
    public int AvailableRuntimes => runtimes.AvailableCount;

    #endregion

    #region ICharacterRegistry, ICharacterDeathEvents

    /// <inheritdoc/>
    public IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters => registry.Characters;

    // Event chuyển tiếp: đăng ký vào event của Manager thực chất là đăng ký thẳng vào system con
    // (add/remove), Manager không giữ danh sách listener riêng.
    /// <inheritdoc/>
    public event Action<CharacterView, CharacterDataSO> CharacterAdded
    {
        add => registry.CharacterAdded += value;
        remove => registry.CharacterAdded -= value;
    }

    /// <inheritdoc/>
    public event Action<CharacterView> CharacterRemoved
    {
        add => registry.CharacterRemoved += value;
        remove => registry.CharacterRemoved -= value;
    }

    /// <inheritdoc/>
    public event Action<CharacterView> CharacterDied
    {
        add => registry.CharacterDied += value;
        remove => registry.CharacterDied -= value;
    }

    /// <inheritdoc/>
    public event Action EnemyCelebrationsCompleted
    {
        add => registry.EnemyCelebrationsCompleted += value;
        remove => registry.EnemyCelebrationsCompleted -= value;
    }

    /// <inheritdoc/>
    public void PlayEnemyCelebrations() => registry.PlayEnemyCelebrations();

    #endregion

    #region IPlayerReader, IPlayerLifecycle

    /// <inheritdoc/>
    public CharacterView CurrentPlayer => players.CurrentPlayer;

    /// <inheritdoc/>
    public CharacterRuntimeData RuntimeData => players.RuntimeData;

    /// <inheritdoc/>
    public Transform PlayerTransform => players.PlayerTransform;

    /// <inheritdoc/>
    public event Action<CharacterView> PlayerChanged
    {
        add => players.PlayerChanged += value;
        remove => players.PlayerChanged -= value;
    }

    /// <inheritdoc/>
    public CharacterView SpawnPlayer() => players.SpawnPlayer();

    /// <inheritdoc/>
    public void RemovePlayer() => players.RemovePlayer();

    #endregion

    #region IPlayerSelection

    /// <inheritdoc/>
    public IReadOnlyList<CharacterDataSO> PlayableCharacters => playableCharacters;

    /// <inheritdoc/>
    public CharacterType SelectedCharacterType => players.ActiveCharacterType;

    /// <inheritdoc/>
    public event Action<CharacterType> SelectionChanged;

    /// <inheritdoc/>
    public bool SelectCharacter(CharacterType type)
    {
        if (type == players.ActiveCharacterType) return true; // Không đổi gì nên không ghi file, không phát event.
        bool saved = players.SelectCharacter(type);
        // RAM đã đổi kể cả khi ghi file lỗi, nên UI vẫn cần vẽ lại.
        SelectionChanged?.Invoke(type);
        return saved;
    }

    // Lọc phe Player, giữ thứ tự asset để thẻ trên UI không nhảy chỗ giữa các lần mở.
    private static CharacterDataSO[] BuildPlayableList(CharacterDatabaseSO database)
    {
        var list = new List<CharacterDataSO>();
        foreach (CharacterDataSO character in database.Characters)
            if (character != null && character.Team == CharacterTeam.Player) list.Add(character);
        return list.ToArray();
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Thứ tự: hiển thị quái → factory → Player → registry → runtime pool.
    /// <list type="bullet">
    /// <item>Gỡ callback camera trước khi nhân vật bị thu hồi (trước đây EnemyVisibility dispose ở 225, trước factory 200).</item>
    /// <item>Factory thu hồi mọi nhân vật còn lại, kể cả Player; mỗi vỏ còn gọi Combat để dọn skill/effect (Combat dispose sau, ở 175).</item>
    /// <item>Player gỡ event level/trang bị; Player đã bị factory thu hồi nên RemovePlayer không làm gì thêm.</item>
    /// <item>Registry và runtime pool dọn sau cùng vì vỏ còn dùng chúng trong lúc bị thu hồi.</item>
    /// </list>
    /// Mỗi bước được cô lập để lỗi của một system không chặn việc dọn các system còn lại.
    /// </summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        SelectionChanged = null;
        DisposeSafely(enemyVisibility);
        DisposeSafely(factory);
        DisposeSafely(players);
        DisposeSafely(registry);
        DisposeSafely(runtimes);
        DisposeSafely(damageText);
    }

    // System có thể null nếu constructor lỗi giữa chừng.
    private static void DisposeSafely(IDisposable system)
    {
        if (system == null) return;
        try { system.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    #endregion
}
