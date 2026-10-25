using System.Collections.Generic;
using UnityEngine;
using Zenject;


// * Quản lý nền lặp bằng một lưới chunk quanh người chơi; dùng Lean Pool tái sử dụng các mảnh nền.
public class GroundChunkManager : MonoBehaviour
{

    #region Player Dependencies & Startup State

    private Transform player;              // * Tâm lưới được đồng bộ từ PlayerManager.
    private PlayerManager playerManager;   // * Nguồn player và sự kiện thay nhân vật.
    private PoolManager poolManager;       // * Spawn/thu hồi chunk và gom các pool vào holder.
    private bool started;                  // * Cho phép dựng lưới sau khi Start chuẩn bị mẫu nền.

    #endregion

    #region Ground & Map Settings

    // * Mẫu nền để tạo các bản sao: có thể là prefab hoặc đối tượng Ground trong scene.
    // * Mẫu được tắt trong Start; các bản sao từ pool mới là nền hiển thị.
    [Header("References")]
    [Tooltip("Drag the existing Ground object from the scene here. It is used as the chunk template.")]
    [SerializeField] private GameObject groundChunkPrefab;
    // * Đối tượng cha chứa các bản sao cho gọn Hierarchy; mặc định là Transform của manager.
    // ! Nên giữ scale bằng 1 để kích thước hiển thị khớp khoảng cách lưới.
    [SerializeField] private Transform chunkParent;

    [Header("Map")]
    [SerializeField] private MapDatabaseSO mapDatabase;  // * Kho tra sprite theo key; Level Data chỉ cần chọn key rồi gọi SetMap.
    // * Map dùng lúc bắt đầu; để trống để giữ sprite có sẵn trên prefab.
    // * Mặc định cho component mới là nền cát; giá trị đã lưu trong Inspector vẫn được giữ.
    [SerializeField] private string initialMapKey = GameConstants.MapKeys.Desert;

    [Header("Chunk Settings")]
    // ! Chiều dài một cạnh chunk theo world unit; phải khớp kích thước hiển thị thực của nền.
    // * Giá trị này điều khiển khoảng cách đặt chunk, không tự thay kích thước SpriteRenderer.
    [Tooltip("Must match the world size of the Ground sprite. The current Ground is 4 x 4.")]
    [SerializeField, Min(0.1f)] private float chunkSize = 4f;
    // * Số vòng chunk giữ quanh chunk trung tâm theo mỗi hướng, không phải bán kính world unit.
    // * Cạnh lưới = 2 * visibleRadius + 1; radius 2 tạo 5 x 5 = 25 chunk.
    // * Với chunkSize 4, vùng được giữ rộng 20 x 20 world unit; radius lớn hơn dùng nhiều đối tượng hơn.
    [Tooltip("2 creates a 5 x 5 grid (25 chunks) around the player.")]
    [SerializeField, Min(0)] private int visibleRadius = 2;

    #endregion

    #region Chunk & Map State

    // * Ánh xạ tọa độ nguyên của chunk sang GameObject đang hiển thị.
    private readonly Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();
    // * Tập tọa độ cần giữ ở lần cập nhật hiện tại; HashSet tránh trùng và hỗ trợ tra cứu.
    private readonly HashSet<Vector2Int> requiredChunks = new HashSet<Vector2Int>();
    private Vector2Int lastPlayerChunk;  // * Chunk chứa người chơi ở lần cập nhật trước, dùng để phát hiện đổi ô.
    private bool initialized;            // * Chỉ cho phép cập nhật sau khi tham chiếu hợp lệ và lưới đầu tiên đã được dựng.

    private Sprite currentMapSprite;  // * Sprite và key đang dùng lúc chạy; không sửa sprite trên prefab asset hoặc database.
    public string CurrentMapKey { get; private set; }

    #endregion

    #region Player Binding

    // * Nhận dependency, không phụ thuộc thứ tự Start giữa player và manager nền.
    [Inject]
    public void Construct(PlayerManager players, PoolManager pools)
    {
        if (playerManager != null)
            playerManager.PlayerChanged -= OnPlayerChanged;
        playerManager = players;
        poolManager = pools;
        if (isActiveAndEnabled)
            OnEnable();
    }

    // * Nhận player hiện tại và các lần tạo/gỡ tiếp theo.
    private void OnEnable()
    {
        if (playerManager == null)
            return;
        playerManager.PlayerChanged -= OnPlayerChanged;
        playerManager.PlayerChanged += OnPlayerChanged;
        OnPlayerChanged(playerManager.CurrentPlayer);
    }

    // * Hủy subscription khi component bị tắt hoặc scene kết thúc.
    private void OnDisable()
    {
        if (playerManager != null)
            playerManager.PlayerChanged -= OnPlayerChanged;
    }

    // * Tạm dừng nếu chưa có player, dựng lại vùng quanh nhân vật mới khi đã sẵn sàng.
    private void OnPlayerChanged(CharacterView current)
    {
        player = current != null ? current.transform : null;
        initialized = false;
        if (started && player != null)
            InitializeChunks();
    }

    #endregion

    #region Ground Initialization

    // * Chuẩn bị mẫu nền và parent; không tìm player bằng FindObjectOfType.
    private void Awake()
    {
        if (groundChunkPrefab == null)
            groundChunkPrefab = GameObject.Find("Ground");

        if (chunkParent == null)
            chunkParent = transform;
    }

    // * Kiểm tra dữ liệu, tắt mẫu nền và dựng lưới đầu tiên tại vị trí người chơi.
    private void Start()
    {
        if (groundChunkPrefab == null || poolManager == null)
        {
            Debug.LogError("GroundChunkManager needs a Ground template and Zenject injection.", this);
            enabled = false;
            return;
        }

        // * Tắt mẫu Ground; Lean Pool tạo các bản sao để hiển thị nền.
        groundChunkPrefab.SetActive(false);
        // * Giữ map nếu hệ thống khác đã gọi SetMap trước Start.
        if (currentMapSprite == null && !string.IsNullOrWhiteSpace(initialMapKey))
            SetMap(initialMapKey);
        started = true;
        InitializeChunks();
    }

    #endregion

    #region Chunk Streaming

    // * Chạy khi cả mẫu nền và player đã có, hỗ trợ player được tạo muộn hoặc tạo lại.
    private void InitializeChunks()
    {
        if (player == null)
            return;
        lastPlayerChunk = GetChunkCoordinate(player.position);
        RefreshChunks(lastPlayerChunk);
        initialized = true;
    }

    // * Mỗi frame kiểm tra tọa độ chunk; chỉ dựng lại tập cần giữ khi người chơi sang chunk khác.
    private void Update()
    {
        if (!initialized || player == null)
            return;

        Vector2Int playerChunk = GetChunkCoordinate(player.position);
        if (playerChunk == lastPlayerChunk)
            return;

        lastPlayerChunk = playerChunk;
        RefreshChunks(playerChunk);
    }

    // * centerChunk là tọa độ nguyên làm tâm: tính vùng cần giữ, thu hồi chunk xa và bổ sung chunk thiếu.
    // * Chunk vẫn thuộc vùng được giữ nguyên; vị trí mới được tính trực tiếp nên hỗ trợ vượt nhiều ô.
    private void RefreshChunks(Vector2Int centerChunk)
    {
        requiredChunks.Clear();

        for (int x = -visibleRadius; x <= visibleRadius; x++)
        {
            for (int y = -visibleRadius; y <= visibleRadius; y++)
                requiredChunks.Add(centerChunk + new Vector2Int(x, y));
        }

        // * Gom tọa độ cần xóa vào danh sách riêng để không sửa Dictionary ngay khi đang duyệt.
        List<Vector2Int> chunksToDespawn = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, GameObject> pair in activeChunks)
        {
            if (!requiredChunks.Contains(pair.Key))
                chunksToDespawn.Add(pair.Key);
        }

        // * Trả các chunk ngoài vùng về pool trước để chúng có thể được dùng lại ngay.
        foreach (Vector2Int coordinate in chunksToDespawn)
        {
            poolManager.Despawn(activeChunks[coordinate]);
            activeChunks.Remove(coordinate);
        }

        // * Chỉ lấy bản sao cho tọa độ còn thiếu, không tạo lại chunk đã có.
        foreach (Vector2Int coordinate in requiredChunks)
        {
            if (activeChunks.ContainsKey(coordinate))
                continue;

            // * Đổi tọa độ ô thành vị trí thế giới trên lưới neo tại gốc XY = (0, 0).
            // * Z giữ theo mẫu nền; vị trí XY của mẫu không dùng làm gốc lưới.
            Vector3 position = new Vector3(
                coordinate.x * chunkSize,
                coordinate.y * chunkSize,
                groundChunkPrefab.transform.position.z);

            GameObject chunk = poolManager.Spawn(
                groundChunkPrefab, position, Quaternion.identity, PoolGroup.World, chunkParent);
            if (chunk == null)
                continue;

            // ! Chunk tái sử dụng có thể còn sprite của map cũ, nên luôn gán lại sau spawn.
            ApplyMapToChunk(chunk);
            activeChunks.Add(coordinate, chunk);
        }
    }

    #endregion

    #region Map Selection

    // * Level Data hoặc hệ thống chọn màn gọi hàm này để đổi nền theo key.
    // ! Nếu tra cứu thất bại thì giữ nguyên map hiện tại; trả về false để bên gọi biết.
    public bool SetMap(string key)
    {
        if (mapDatabase == null || !mapDatabase.TryGetMap(key, out Sprite sprite))
        {
            Debug.LogWarning($"GroundChunkManager: cannot load map '{key}'. Check the database, key and sprite.", this);
            return false;
        }

        if (groundChunkPrefab != null && groundChunkPrefab.GetComponent<SpriteRenderer>() == null)
        {
            Debug.LogWarning("GroundChunkManager: the Ground prefab needs a SpriteRenderer on its root.", this);
            return false;
        }

        currentMapSprite = sprite;
        CurrentMapKey = key;
        foreach (GameObject chunk in activeChunks.Values)
            ApplyMapToChunk(chunk);

        return true;
    }

    // * Gán sprite lên bản sao đang chạy; giữ kích thước Tiled/Sliced khớp khoảng cách chunk.
    private void ApplyMapToChunk(GameObject chunk)
    {
        if (chunk == null || currentMapSprite == null)
            return;

        SpriteRenderer renderer = chunk.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.sprite = currentMapSprite;
        if (renderer.drawMode != SpriteDrawMode.Simple)
            renderer.size = Vector2.one * chunkSize;
    }

    #endregion

    #region Grid Coordinates

    // * Chuyển vị trí thế giới sang chỉ số chunk XY, bỏ qua Z.
    // * Cộng nửa chunk trước FloorToInt để mỗi chunk có tâm tại chỉ số * chunkSize;
    // * ví dụ chunkSize 4: chunk 0 phủ khoảng [-2, 2), chunk 1 phủ [2, 6).
    private Vector2Int GetChunkCoordinate(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt((worldPosition.x + chunkSize * 0.5f) / chunkSize),
            Mathf.FloorToInt((worldPosition.y + chunkSize * 0.5f) / chunkSize));
    }

    #endregion

    #region Chunk Cleanup

    // * Khi manager bị hủy, trả các chunk đang giữ về Lean Pool rồi xóa bảng theo dõi.
    private void OnDestroy()
    {
        foreach (GameObject chunk in activeChunks.Values)
            poolManager?.Despawn(chunk);

        activeChunks.Clear();
    }

    #endregion

}
