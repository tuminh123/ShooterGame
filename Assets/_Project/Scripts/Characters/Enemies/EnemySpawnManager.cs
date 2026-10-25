using System.Collections.Generic;
using UnityEngine;
using Zenject;

// * Sinh quái theo nhịp ở các điểm trên viền hình chữ nhật và giới hạn số quái đang hoạt động.
public class EnemySpawnManager : MonoBehaviour
{

    #region Spawn Settings

    [Header("References")]
    [SerializeField] private Transform spawnCenter;   // * Tâm vùng sinh quái; mặc định lấy camera chính nếu chưa gán trong Inspector.
    [SerializeField] private GameObject enemyPrefab;  // * Prefab quái để Lean Pool tạo mới hoặc lấy bản sao đã thu hồi.

    [Header("Spawn Area")]
    // * Chiều rộng X và chiều cao Y của toàn vùng sinh, tính bằng world unit; chỉ sinh trên viền.
    [Tooltip("World-space width and height of the rectangular spawn perimeter.")]
    [SerializeField] private Vector2 areaSize = new Vector2(10f, 6f);
    // * Số điểm mỗi cạnh, tính cả góc; các góc chung chỉ thêm một lần.
    // * Tổng điểm = 4 * pointsPerSide - 4; giá trị 4 tạo 12 điểm.
    [Tooltip("Includes the two corners on each side. 4 creates 12 unique spawn points.")]
    [SerializeField, Min(2)] private int pointsPerSide = 4;
    [SerializeField] private float spawnZ = 0f;  // * Tọa độ Z của quái khi sinh; không phải khoảng cách ngẫu nhiên theo chiều sâu.

    [Header("Spawn Rate")]
    // * Khoảng cách giữa hai lần kiểm tra sinh, tính bằng giây theo thời gian game.
    // ! Mỗi lượt sinh tối đa một quái, không sinh bù toàn bộ lượt bị trễ khi frame chậm.
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.5f;
    [SerializeField, Min(1)] private int maxAliveEnemies = 20;  // * Giới hạn số quái đang hoạt động do manager này theo dõi.

    #endregion

    #region Spawn State & Dependencies

    // * Danh sách tọa độ XY trên viền; được xóa nội dung và dùng lại khi tính vùng mới.
    private readonly List<Vector2> spawnPoints = new List<Vector2>();
    // * Các quái đã sinh và chưa được loại khỏi danh sách; dọn lại theo mỗi nhịp spawn.
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private float nextSpawnTime;  // * Mốc Time.time sớm nhất được kiểm tra sinh tiếp, tính bằng giây.

    [Inject] private PoolManager poolManager;  // * Spawn kèm holder và injection cho bản sao mới.

    #endregion

    #region Spawn Loop

    // * Khởi tạo tâm sinh mặc định theo camera có tag MainCamera.
    private void Awake()
    {
        if (spawnCenter == null && Camera.main != null)
            spawnCenter = Camera.main.transform;
    }

    // * Mỗi frame kiểm tra tham chiếu và thời gian; đến nhịp thì dọn danh sách, sinh nếu còn chỗ.
    private void Update()
    {
        if (enemyPrefab == null || spawnCenter == null || Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + spawnInterval;
        RemoveDespawnedEnemies();

        if (activeEnemies.Count < maxAliveEnemies)
            SpawnEnemy();
    }

    // * Tính lại viền theo tâm hiện tại, chọn ngẫu nhiên một điểm và lấy quái từ Lean Pool.
    private void SpawnEnemy()
    {
        BuildSpawnPoints();
        if (spawnPoints.Count == 0)
            return;

        // * Chọn đều giữa các điểm có sẵn, không chọn liên tục mọi vị trí trên viền.
        Vector2 point = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject enemy = poolManager.Spawn(
            enemyPrefab,
            new Vector3(point.x, point.y, spawnZ),
            Quaternion.identity, PoolGroup.Enemies);

        if (enemy != null)
        {
            activeEnemies.Add(enemy);
        }
    }

    #endregion

    #region Spawn Perimeter

    // * Dựng các điểm cách đều trên bốn cạnh, không lặp góc; không sinh điểm trong lòng hình chữ nhật.
    private void BuildSpawnPoints()
    {
        spawnPoints.Clear();

        Vector2 center = (Vector2)spawnCenter.position;
        Vector2 halfSize = areaSize * 0.5f;       // * Nửa kích thước để tính các mép trái/phải/trên/dưới từ tâm.
        int count = Mathf.Max(2, pointsPerSide);  // * Bảo đảm ít nhất hai điểm mỗi cạnh, tránh phép chia cho 0 trong nội suy.

        // * Hai cạnh trên/dưới gồm cả bốn góc; t từ 0 đến 1 là tỷ lệ nội suy dọc cạnh.
        // * Top and bottom edges, including their corners.
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float x = Mathf.Lerp(center.x - halfSize.x, center.x + halfSize.x, t);
            spawnPoints.Add(new Vector2(x, center.y + halfSize.y));
            spawnPoints.Add(new Vector2(x, center.y - halfSize.y));
        }

        // * Hai cạnh trái/phải bỏ góc vì các góc đã được thêm ở vòng lặp phía trên.
        // * Left and right edges, excluding corners already added above.
        for (int i = 1; i < count - 1; i++)
        {
            float t = i / (float)(count - 1);
            float y = Mathf.Lerp(center.y - halfSize.y, center.y + halfSize.y, t);
            spawnPoints.Add(new Vector2(center.x - halfSize.x, y));
            spawnPoints.Add(new Vector2(center.x + halfSize.x, y));
        }
    }

    #endregion

    #region Active Enemy Tracking

    // * Loại quái đã bị hủy hoặc bị tắt/thu hồi; duyệt ngược để xóa không làm bỏ sót phần tử.
    private void RemoveDespawnedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = activeEnemies[i];
            if (enemy == null || !enemy.activeInHierarchy)
                activeEnemies.RemoveAt(i);
        }
    }

    #endregion

    #region Scene Gizmos

    // * Chỉ vẽ hỗ trợ trong Scene view khi chọn đối tượng: viền xanh và các điểm sinh màu vàng.
    // * Không sinh quái; dùng chung hàm tính điểm để hình minh họa khớp vùng sinh.
    private void OnDrawGizmosSelected()
    {
        if (spawnCenter == null && Camera.main != null)
            spawnCenter = Camera.main.transform;
        if (spawnCenter == null)
            return;

        BuildSpawnPoints();
        Vector3 center = spawnCenter.position;
        center.z = spawnZ;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, new Vector3(areaSize.x, areaSize.y, 0f));

        Gizmos.color = Color.yellow;
        foreach (Vector2 point in spawnPoints)
            Gizmos.DrawSphere(new Vector3(point.x, point.y, spawnZ), 0.08f);
    }

    #endregion

}
