using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

/// <summary>Tạo và thu hồi enemy/boss chung; không quyết định lịch hay quota.</summary>
[DefaultExecutionOrder(10000)]
public sealed class EnemySpawnManager : MonoBehaviour, Zenject.ILateTickable
{
    #region Zenject Tick Registration

    private Zenject.LazyInject<Zenject.TickableManager> tickables;
    private bool registeredForTicks;
    private int activationFrame = -1;

    [Zenject.Inject]
    public void ConstructTickables(Zenject.LazyInject<Zenject.TickableManager> tickables) => this.tickables = tickables;

    // Đăng ký một lần ở Start để tránh vòng phụ thuộc trong lúc Zenject dựng container.
    // Object trong pool vẫn giữ đăng ký; Tick bỏ qua khi tắt hoặc vừa được tái sử dụng.
    private void StartFrameUpdates()
    {
        if (registeredForTicks || tickables == null) return;
        tickables.Value.AddLate(this, 200);
        registeredForTicks = true;
    }

    private void StopFrameUpdates()
    {
        if (!registeredForTicks) return;
        tickables.Value.RemoveLate(this);
        registeredForTicks = false;
    }

    private void Start() => StartFrameUpdates();

    private void OnEnable() => activationFrame = Time.frameCount;

    #endregion
    #region Cấu hình và dependency

    [Header("References")]
    [SerializeField] private Transform spawnCenter;
    [Tooltip("Camera làm tâm Path. Bỏ trống dùng Main Camera; Area lấy tâm player.")]
    [SerializeField] private Camera formationCamera;
    [Header("Enemy Separation")]
    [SerializeField] private EnemyController.SeparationSettings separation = new EnemyController.SeparationSettings();
    [Header("Ordinary Attack Recoil")]
    [FormerlySerializedAs("contactAttack")]
    [SerializeField] private EnemyController.RecoilSettings recoil = new EnemyController.RecoilSettings();
    [Header("Spawn Perimeter")]
    [SerializeField] private Vector2 areaSize = new Vector2(10f, 6f);
    [Min(2)] [SerializeField] private int pointsPerSide = 4;
    [SerializeField] private float spawnZ;

    private readonly List<Vector2> spawnPoints = new List<Vector2>();
    private Dictionary<CharacterType, CharacterLevelConfigSO> levelConfigs;
    private CharacterFactory factory;
    private PlayerManager players;

    #endregion

    #region Dữ liệu công khai

    public Vector2 SpawnCenterPosition => spawnCenter != null ? (Vector2)spawnCenter.position :
        Camera.main != null ? (Vector2)Camera.main.transform.position : (Vector2)transform.position;
    public Camera FormationCamera => formationCamera != null ? formationCamera : Camera.main;
    public Vector2 SpawnAreaSize => areaSize;
    public float SpawnPlaneZ => spawnZ;
    public event Action SpawnLateUpdate;

    #endregion

    #region Khởi tạo và kiểm tra

    /// <summary>Nhận factory, player và lập bảng cấu hình cấp theo loại nhân vật.</summary>
    [Inject]
    public void Construct(CharacterFactory factory, PlayerManager players, LevelDatabaseSO levelDatabase)
    {
        this.factory = factory;
        this.players = players;
        levelConfigs = levelDatabase.CreateTypeLookup();
    }

    /// <summary>Kiểm tra phe, visual và cấu hình cấp của enemy/boss; báo lỗi nếu thiếu.</summary>
    public void ValidateCharacter(CharacterDataSO data)
    {
        if (data == null || data.Team != CharacterTeam.Enemy || data.VisualPrefab == null)
            throw new InvalidOperationException("Spawner cần Enemy/Boss có visual hợp lệ.");
        if (levelConfigs == null || !levelConfigs.TryGetValue(data.CharacterType, out var levels))
            throw new InvalidOperationException("Thiếu cấu hình level cho enemy/boss: " + data.name);
        levels.GetTotalBonus(1);
    }

    /// <summary>Kiểm tra tâm spawn, kích thước vùng, số điểm cạnh và tọa độ Z.</summary>
    public void ValidatePerimeter()
    {
        if (spawnCenter == null && Camera.main == null)
            throw new InvalidOperationException("Cần gán Spawn Center hoặc Main Camera.");
        if (!Positive(areaSize.x) || !Positive(areaSize.y) || pointsPerSide < 2 || float.IsNaN(spawnZ) || float.IsInfinity(spawnZ))
            throw new InvalidOperationException("Vùng spawn, số điểm cạnh và tọa độ Z phải hợp lệ.");
    }

    #endregion

    #region Tạo và thu hồi nhân vật

    /// <summary>Tạo AI mặc định với cấu hình tách đàn và phản lực hiện tại.</summary>
    public EnemyController CreateController() => new EnemyController(players, factory, separation, recoil);

    /// <summary>Tính bonus theo cấp rồi tạo nhân vật; dọn controller nếu tạo thất bại.</summary>
    public CharacterView Spawn(CharacterDataSO data, int level, Vector2 point, ICharacterController controller = null)
    {
        ValidateCharacter(data);
        if (level < 1 || float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y) ||
            float.IsNaN(spawnZ) || float.IsInfinity(spawnZ))
            throw new ArgumentException("Cấp và vị trí spawn phải hợp lệ.");
        if (!isActiveAndEnabled) throw new InvalidOperationException("EnemySpawnManager đang tắt.");
        var behavior = controller ?? CreateController();
        try
        {
            StatBonus bonus = levelConfigs[data.CharacterType].GetTotalBonus(level);
            // CharacterFactory applies data.IsBoss and boss presentation. Both roles retain the existing shared AI.
            return factory.Spawn(data, bonus, behavior, new Vector3(point.x, point.y, spawnZ))
                ?? throw new InvalidOperationException("Không thể tạo enemy/boss: " + data.name);
        }
        catch { behavior.Dispose(); throw; }
    }

    /// <summary>Sinh Elite bằng AI thường rồi áp dụng tăng HP và scale riêng cho lượt này.</summary>
    public CharacterView SpawnElite(CharacterDataSO data, int level, Vector2 point, float scale, float healthMultiplier)
    {
        if (scale < 1f || float.IsNaN(scale) || float.IsInfinity(scale) ||
            healthMultiplier < 1f || float.IsNaN(healthMultiplier) || float.IsInfinity(healthMultiplier))
            throw new ArgumentOutOfRangeException(nameof(scale));
        CharacterView view = Spawn(data, level, point);
        try
        {
            view.IsElite = true;
            view.transform.localScale *= scale;
            if (healthMultiplier > 1f)
                view.RuntimeData.AddModifier(new StatModifier(StatType.MaxHealth, StatOperation.Percent,
                    (healthMultiplier - 1f) * 100f));
            return view;
        }
        catch { Despawn(view); throw; }
    }

    /// <summary>Trả nhân vật về pool qua factory, không phát sự kiện chết.</summary>
    public void Despawn(CharacterView character) => factory.Despawn(character);

    #endregion

    #region Hình học spawn và callback Unity

    /// <summary>Tạo lại điểm viền theo tâm hiện tại rồi chọn ngẫu nhiên một điểm.</summary>
    public Vector2 ChoosePerimeterPoint()
    {
        GetSpawnPoints(spawnPoints);
        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
    }

    /// <summary>Làm mới bộ đệm điểm viền dùng cho chọn điểm và gizmo.</summary>
    private void BuildSpawnPoints() => GetSpawnPoints(spawnPoints);
    /// <summary>Trả về true khi giá trị dương và hữu hạn.</summary>
    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    /// <summary>Phát callback cuối frame để các hệ thống bù chuyển động camera/player.</summary>
    public void LateTick()
    {
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return;
        SpawnLateUpdate?.Invoke();
    }
    /// <summary>Gỡ toàn bộ listener khi component bị hủy.</summary>
    private void OnDestroy() { StopFrameUpdates(); SpawnLateUpdate = null; }

    /// <summary>Ghi các điểm viền chữ nhật vào danh sách đích, không lặp các góc.</summary>
    public void GetSpawnPoints(List<Vector2> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        Vector2 center = SpawnCenterPosition;
        Vector2 half = areaSize * 0.5f;
        int count = Mathf.Max(2, pointsPerSide);

        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(center.x - half.x, center.x + half.x, i / (float)(count - 1));
            destination.Add(new Vector2(x, center.y - half.y));
            destination.Add(new Vector2(x, center.y + half.y));
        }

        for (int i = 1; i < count - 1; i++)
        {
            float y = Mathf.Lerp(center.y - half.y, center.y + half.y, i / (float)(count - 1));
            destination.Add(new Vector2(center.x - half.x, y));
            destination.Add(new Vector2(center.x + half.x, y));
        }
    }

    /// <summary>Vẽ vùng spawn và điểm viền khi chọn component trong Scene.</summary>
    private void OnDrawGizmosSelected()
    {
        if (spawnCenter == null)
        {
            return;
        }

        BuildSpawnPoints();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(spawnCenter.position, new Vector3(areaSize.x, areaSize.y, 0f));
        Gizmos.color = Color.yellow;

        foreach (Vector2 point in spawnPoints)
        {
            Gizmos.DrawSphere(new Vector3(point.x, point.y, spawnZ), 0.08f);
        }
    }

    #endregion
}
