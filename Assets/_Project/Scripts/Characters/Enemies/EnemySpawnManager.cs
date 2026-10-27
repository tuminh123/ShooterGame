using System.Collections.Generic;
using UnityEngine;
using Zenject;

// * Sinh quái theo nhịp ở các điểm trên viền hình chữ nhật và giới hạn số quái đang hoạt động.
public class EnemySpawnManager : MonoBehaviour
{

    #region Spawn Settings

    [Header("References")]
    [SerializeField] private Transform spawnCenter;   // * Tâm vùng sinh quái; mặc định lấy camera chính nếu chưa gán trong Inspector.
    [SerializeField] private WaveConfigSO waveConfig; // * Chọn loại quái/nhịp/trần theo wave; mọi loại dùng cùng vỏ từ CharacterFactory.

    [Header("Enemy Separation")]
    [SerializeField] private EnemyController.SeparationSettings separation = new EnemyController.SeparationSettings(); // * Một bộ cấu hình cho quái do spawner tạo; không thêm trạng thái né lên CharacterDataSO.

    [Header("Enemy Contact Attack")]
    [SerializeField] private EnemyController.ContactSettings contactAttack = new EnemyController.ContactSettings(); // * Damage khi chạm và phản lực sau hit; mỗi controller chốt cấu hình tại spawn.

    [Header("Spawn Area")]
    // * Chiều rộng X và chiều cao Y của toàn vùng sinh, tính bằng world unit; chỉ sinh trên viền.
    [Tooltip("World-space width and height of the rectangular spawn perimeter.")]
    [SerializeField] private Vector2 areaSize = new Vector2(10f, 6f); // * Kích thước toàn vùng spawn theo world unit, chỉ lấy điểm trên viền.
    // * Số điểm mỗi cạnh, tính cả góc; các góc chung chỉ thêm một lần.
    // * Tổng điểm = 4 * pointsPerSide - 4; giá trị 4 tạo 12 điểm.
    [Tooltip("Includes the two corners on each side. 4 creates 12 unique spawn points.")]
    [SerializeField, Min(2)] private int pointsPerSide = 4; // * Số điểm mỗi cạnh gồm hai góc; 4 tạo tổng cộng 12 điểm riêng biệt.
    [SerializeField] private float spawnZ = 0f;  // * Tọa độ Z của quái khi sinh; không phải khoảng cách ngẫu nhiên theo chiều sâu.

    #endregion

    #region Spawn State & Dependencies

    // * Danh sách tọa độ XY trên viền; được xóa nội dung và dùng lại khi tính vùng mới.
    private readonly List<Vector2> spawnPoints = new List<Vector2>(); // * Buffer điểm spawn được xóa và tái sử dụng khi tâm di chuyển.
    private readonly List<CharacterView> activeEnemies = new List<CharacterView>(); // * Được xóa ngay khi Released để tránh đếm nhầm vỏ đã dùng lại làm Player.
    private readonly EnemySpawnProgress progress = new EnemySpawnProgress(); // * Đồng hồ riêng theo loại, giữ nguyên khi đổi wave hoặc không còn quái sống.
    private float nextSpawnTime;  // * Mốc Time.time sớm nhất được kiểm tra sinh tiếp, tính bằng giây.
    private float runStartedAt; // * Mốc bắt đầu để chọn wave theo thời gian game, không dùng thời gian thực.
    private WaveConfigSO.Wave currentWave; // * Nhận biết chuyển wave để áp dụng nhịp mới ngay.
    private LevelManager levels; // * Nghe StartNewRun để reset wave, đồng hồ và thu hồi quái của lượt trước.
    private CharacterFactory factory; // * Ghép runtime/hành vi với prefab vỏ chung.
    private PlayerManager players; // * Truyền mục tiêu cho EnemyController và dừng spawn khi chưa có Player sống.

    [Inject]
    public void Construct(CharacterFactory factory, PlayerManager players, LevelManager levels)
    {
        if (this.levels != null)
            this.levels.RunReset -= ResetRun;
        this.factory = factory;
        this.players = players;
        this.levels = levels;
        this.levels.RunReset += ResetRun;
    }

    private void Start()
    {
        if (waveConfig == null)
            throw new System.InvalidOperationException("Assign Wave Config on EnemySpawnManager.");
        waveConfig.Validate();
        ResetRun();
    }

    // * Reset chỉ khi bắt đầu lượt mới; tắt/bật spawner hoặc đổi wave không làm reset đồng hồ loại quái.
    private void ResetRun()
    {
        while (activeEnemies.Count > 0)
        {
            int last = activeEnemies.Count - 1;
            CharacterView enemy = activeEnemies[last];
            activeEnemies.RemoveAt(last);
            if (enemy == null)
                continue;
            enemy.Released -= OnEnemyReleased;
            factory.Despawn(enemy);
        }
        progress.Reset();
        runStartedAt = Time.time;
        nextSpawnTime = Time.time;
        currentWave = null;
    }

    #endregion

    #region Spawn Loop

    // * Khởi tạo tâm sinh mặc định theo camera có tag MainCamera.
    private void Awake()
    {
        if (spawnCenter == null && Camera.main != null)
            spawnCenter = Camera.main.transform;
    }

    // * Mỗi frame chọn wave theo thời gian game; đến nhịp thì sinh nếu còn chỗ trong trần đang sống.
    private void Update()
    {
        if (waveConfig == null || spawnCenter == null || players.CurrentPlayer == null ||
            !players.RuntimeData.IsAlive || Time.deltaTime <= 0f)
            return;

        WaveConfigSO.Wave wave = waveConfig.GetWave(Time.time - runStartedAt);
        if (wave != currentWave)
        {
            currentWave = wave;
            nextSpawnTime = Time.time;
        }
        if (wave == null || Time.time < nextSpawnTime)
            return;
        nextSpawnTime = Time.time + wave.spawnInterval;

        if (activeEnemies.Count < wave.maxAliveEnemies)
            SpawnEnemy(wave.enemies[Random.Range(0, wave.enemies.Length)]);
    }

    // * Tính lại viền theo tâm hiện tại, chọn ngẫu nhiên một điểm và lấy quái từ Lean Pool.
    private void SpawnEnemy(CharacterDataSO template)
    {
        BuildSpawnPoints();
        if (spawnPoints.Count == 0)
            return;

        // * Chọn đều giữa các điểm có sẵn, không chọn liên tục mọi vị trí trên viền.
        Vector2 point = spawnPoints[Random.Range(0, spawnPoints.Count)];
        // * Bonus chốt vào runtime mới; không cộng vào SO, không cập nhật ngược các cá thể đang sống.
        StatBonus bonus = progress.GetBonus(template, Time.time);
        var runtime = new CharacterRuntimeData(template, 1, 1, default, default, default, bonus);
        // * Controller mới mỗi lần spawn: hướng tách cache và lịch quét không đi theo vỏ được tái sử dụng.
        CharacterView enemy = factory.Spawn(template, runtime, new EnemyController(players, factory, separation, contactAttack),
            new Vector3(point.x, point.y, spawnZ));

        if (enemy != null)
        {
            progress.RecordSpawn(template.CharacterType, Time.time);
            enemy.Released += OnEnemyReleased;
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

    // * Loại ngay theo vòng đời, không đợi nhịp spawn rồi kiểm tra activeInHierarchy trên vỏ có thể đã đổi chủ.
    private void OnEnemyReleased(CharacterView enemy)
    {
        activeEnemies.Remove(enemy);
    }

    private void OnDestroy()
    {
        if (levels != null)
            levels.RunReset -= ResetRun;
        ResetRun();
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
