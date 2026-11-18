using System;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền World: chọn map nền theo key và cho nền lặp chạy theo Player.
/// Binding Zenject duy nhất của miền; miền khác chỉ dùng <see cref="IWorldMap"/> (World/WorldContracts.cs).
/// </summary>
/// <remarks>
/// Tự tạo <see cref="GroundChunks"/>. Đọc Player qua <see cref="IPlayerReader"/> (miền Character, nằm dưới World).
/// Stage gọi <see cref="SetMap"/> khi bắt đầu lượt chơi; World không biết Stage.
/// </remarks>
public sealed class WorldManager : IWorldMap, IInitializable, ITickable, IDisposable
{
    #region Settings

    /// <summary>Cấu hình nền trong scene, gán trên GameSceneInstaller.</summary>
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Mẫu chunk nền (prefab có SpriteRenderer ở root). Các chunk hiển thị là bản sao lấy từ pool.")]
        [SerializeField] private GameObject groundTemplate;
        [Tooltip("Cha chứa các chunk cho gọn Hierarchy. Nên giữ scale 1 để kích thước khớp khoảng cách lưới.")]
        [SerializeField] private Transform chunkParent;
        [Tooltip("Kho sprite nền theo key; StageDataSO chỉ cần chọn key.")]
        [SerializeField] private MapDatabaseSO mapDatabase;
        [Tooltip("Map dùng trước khi có màn nào chọn map (ví dụ ở Home). Để trống thì giữ sprite có sẵn trên prefab.")]
        [SerializeField] private string initialMapKey = GameConstants.MapKeys.Desert;
        [Tooltip("Cạnh một chunk theo world unit; phải khớp kích thước hiển thị của sprite nền.")]
        [SerializeField, Min(0.1f)] private float chunkSize = 4f;
        [Tooltip("Số vòng chunk quanh chunk trung tâm. 2 tạo lưới 5 x 5 = 25 chunk.")]
        [SerializeField, Min(0)] private int visibleRadius = 2;

        public GameObject GroundTemplate => groundTemplate;
        public Transform ChunkParent => chunkParent;
        public MapDatabaseSO MapDatabase => mapDatabase;
        public string InitialMapKey => initialMapKey;
        public float ChunkSize => chunkSize;
        public int VisibleRadius => visibleRadius;

        /// <summary>Thiếu cấu hình thì báo ngay lúc dựng container, không để nền hỏng giữa trận.</summary>
        internal void Validate()
        {
            if (groundTemplate == null || mapDatabase == null || chunkParent == null)
                throw new InvalidOperationException("Assign Ground Template, Chunk Parent and Map Database in GameSceneInstaller > World.");
            if (groundTemplate.GetComponent<SpriteRenderer>() == null)
                throw new InvalidOperationException("The ground template needs a SpriteRenderer on its root.");
            if (chunkSize <= 0f || visibleRadius < 0)
                throw new InvalidOperationException("World chunk size must be positive and visible radius must not be negative.");
        }
    }

    #endregion

    #region State

    private readonly Settings settings;
    private readonly IPlayerReader players;
    private readonly GroundChunks ground;
    private bool initialized; // Trước Initialize chưa dựng nền (pool và scene chưa sẵn sàng hoàn toàn).
    private bool disposed;

    /// <inheritdoc/>
    public string CurrentMapKey { get; private set; }

    #endregion

    #region Lifecycle

    public WorldManager(Settings settings, IPlayerReader players, IGameObjectPool pools)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.players = players ?? throw new ArgumentNullException(nameof(players));
        settings.Validate();
        ground = new GroundChunks(pools, settings.GroundTemplate, settings.ChunkParent,
            settings.ChunkSize, settings.VisibleRadius);
        players.PlayerChanged += OnPlayerChanged;
    }

    /// <summary>Đặt map ban đầu (nếu chưa ai chọn map) rồi dựng nền quanh Player hiện tại, nếu đã có.</summary>
    public void Initialize()
    {
        if (CurrentMapKey == null && !string.IsNullOrWhiteSpace(settings.InitialMapKey))
            SetMap(settings.InitialMapKey);
        initialized = true;
        OnPlayerChanged(players.CurrentPlayer);
    }

    /// <summary>Nền đi theo Player; chỉ dựng lại khi Player sang chunk khác.</summary>
    public void Tick() => ground.Tick();

    /// <summary>Gỡ đăng ký Player và trả chunk về pool. Chạy trước Character và Pool (xem ExecutionOrder).</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        players.PlayerChanged -= OnPlayerChanged;
        ground.Dispose();
    }

    #endregion

    #region Map

    /// <inheritdoc/>
    public bool SetMap(string key)
    {
        if (!settings.MapDatabase.TryGetMap(key, out Sprite sprite))
        {
            Debug.LogWarning($"WorldManager: cannot load map '{key}'. Check the map database, key and sprite.");
            return false;
        }
        CurrentMapKey = key;
        ground.SetSprite(sprite);
        return true;
    }

    // Player mới thì dựng nền quanh nó; Player bị gỡ thì nền đứng yên tới khi có Player tiếp theo.
    private void OnPlayerChanged(CharacterView player)
    {
        if (initialized && !disposed)
            ground.Follow(player != null ? player.transform : null);
    }

    #endregion
}
