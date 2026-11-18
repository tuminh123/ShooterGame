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
/// └── EnemyVisibility                    bật/tắt visual quái theo camera
/// </code>
/// Các system con được tạo bằng <c>new</c> ngay trong constructor, đọc từ trên xuống.
/// Miền khác chỉ thấy các interface trong <c>CharacterContracts.cs</c>.
/// </remarks>
public sealed class CharacterManager :
    ICharacterSpawn,
    ICharacterDespawn,
    ICharacterRegistry,
    ICharacterDeathEvents,
    IPlayerReader,
    IPlayerLifecycle,
    ICharacterCapacity,
    IInitializable,
    ITickable,
    IDisposable
{
    #region Owned Systems

    private readonly CharacterRuntimePool runtimes;
    private readonly CharacterRegistry registry;
    private readonly CharacterFactory factory;
    private readonly PlayerSystem players;
    private readonly EnemyVisibility enemyVisibility;
    private bool disposed;

    #endregion

    #region Construction

    /// <summary>Dựng miền Character. Thứ tự tạo đi theo chiều phụ thuộc: system nào được dùng thì phải tạo trước.</summary>
    /// <param name="characterPrefab">Prefab vỏ dùng chung cho Player, quái và boss (installer truyền vào).</param>
    public CharacterManager(
        CharacterView characterPrefab,
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
            var context = new CharacterContext(movementRegistration, movementCommand, spatialRegistration,
                pools, search, runtimes);
            factory = new CharacterFactory(characterPrefab, pools, registry, context);
            players = new PlayerSystem(factory, joystick, playerSettings, characterDatabase, saves, permanentBonuses);
            enemyVisibility = new EnemyVisibility(registry);
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
    public void Initialize()
    {
        if (disposed) return;
        players.Initialize();
    }

    /// <summary>Tick mọi nhân vật (controller rồi skill vốn có). Chạy sau Combat và Stage trong frame.</summary>
    public void Tick()
    {
        if (disposed) return;
        registry.Tick();
    }

    #endregion

    #region ICharacterSpawn, ICharacterDespawn, ICharacterCapacity

    /// <inheritdoc/>
    public CharacterView Spawn(CharacterDataSO template, StatBonus bonus, ICharacterController controller, Vector3 position)
        => factory.Spawn(template, bonus, controller, position);

    /// <inheritdoc/>
    public void Despawn(CharacterView character) => factory.Despawn(character);

    /// <inheritdoc/>
    public void DespawnAll() => factory.DespawnAll();

    /// <inheritdoc/>
    public void PrewarmRuntimes(int count) => runtimes.Prewarm(count);

    /// <inheritdoc/>
    public int AvailableRuntimes => runtimes.AvailableCount;

    #endregion

    #region ICharacterRegistry, ICharacterDeathEvents

    /// <inheritdoc/>
    public IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters => registry.Characters;

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
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DisposeSafely(enemyVisibility);
        DisposeSafely(factory);
        DisposeSafely(players);
        DisposeSafely(registry);
        DisposeSafely(runtimes);
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
