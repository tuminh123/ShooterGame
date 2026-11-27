using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tạo và thu hồi quái/Elite/Boss cho Stage, và cung cấp hình học vùng sinh (viền quanh camera).
/// Không quyết định lịch hay số lượng: đó là việc của các spawn system.
/// </summary>
/// <remarks>
/// Do <see cref="StageManager"/> tạo và truyền cho Wave/Formation/Boss; không bind vào Zenject.
/// Quái đi qua <see cref="ICharacterSpawn"/>/<see cref="ICharacterDespawn"/> của miền Character, nên vẫn dùng chung pool và vòng đời.
/// </remarks>
public sealed class EnemySpawner
{
    #region Settings

    /// <summary>Cấu hình vùng sinh và AI quái, gán trên GameSceneInstaller > Stage.</summary>
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Tâm vùng sinh (thường là Main Camera). Bỏ trống thì dùng Main Camera.")]
        [SerializeField] private Transform spawnCenter;
        [Tooltip("Camera làm tâm Path. Bỏ trống dùng Main Camera; Area lấy tâm player.")]
        [SerializeField] private Camera formationCamera;
        [Header("Enemy Separation")]
        [Tooltip("Cách quái phát hiện và né nhau khi đông. Dùng chung cho mọi loại quái; chỉ quái sinh ra sau khi chỉnh mới nhận số mới.\n" +
                 "Quái đi theo đội hình/đường (pattern) không dùng phần này.")]
        [SerializeField] private EnemyController.SeparationSettings separation = new EnemyController.SeparationSettings();
        [Header("Ordinary Attack Recoil")]
        [Tooltip("Cú lùi của quái sau khi đánh trúng Player bằng đòn thường.")]
        [SerializeField] private EnemyController.RecoilSettings recoil = new EnemyController.RecoilSettings();
        [Header("Spawn Perimeter")]
        [Tooltip("Kích thước hình chữ nhật vùng sinh quanh tâm; quái xuất hiện trên viền.")]
        [SerializeField] private Vector2 areaSize = new Vector2(10f, 6f);
        [Tooltip("Số điểm sinh trên mỗi cạnh ngang (góc không lặp).")]
        [Min(2)] [SerializeField] private int pointsPerSide = 4;
        [Tooltip("Tọa độ Z của quái khi sinh.")]
        [SerializeField] private float spawnZ;

        public Transform SpawnCenter => spawnCenter;
        public Camera FormationCamera => formationCamera;
        public EnemyController.SeparationSettings Separation => separation;
        public EnemyController.RecoilSettings Recoil => recoil;
        public Vector2 AreaSize => areaSize;
        public int PointsPerSide => pointsPerSide;
        public float SpawnZ => spawnZ;

        /// <summary>Vẽ vùng sinh và các điểm viền trong Scene view (GameSceneInstaller gọi khi chọn SceneContext).</summary>
        public void DrawGizmos()
        {
            if (spawnCenter == null) return;
            var points = new List<Vector2>();
            FillPerimeter(spawnCenter.position, areaSize, pointsPerSide, points);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnCenter.position, new Vector3(areaSize.x, areaSize.y, 0f));
            Gizmos.color = Color.yellow;
            foreach (Vector2 point in points)
                Gizmos.DrawSphere(new Vector3(point.x, point.y, spawnZ), 0.08f);
        }
    }

    #endregion

    #region Dependencies

    private readonly Settings settings;
    private readonly ICharacterSpawn characterSpawn;
    private readonly ICharacterDespawn characterDespawn;
    private readonly IPlayerReader players;
    private readonly Dictionary<CharacterType, CharacterLevelConfigSO> levelConfigs; // Bonus chỉ số theo cấp của từng loại quái.
    private readonly List<Vector2> spawnPoints = new List<Vector2>(); // Dùng lại mỗi lần chọn điểm viền.

    #endregion

    #region Lifecycle

    public EnemySpawner(Settings settings, ICharacterSpawn characterSpawn, ICharacterDespawn characterDespawn,
        IPlayerReader players, LevelDatabaseSO levelDatabase)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.characterSpawn = characterSpawn ?? throw new ArgumentNullException(nameof(characterSpawn));
        this.characterDespawn = characterDespawn ?? throw new ArgumentNullException(nameof(characterDespawn));
        this.players = players ?? throw new ArgumentNullException(nameof(players));
        levelConfigs = levelDatabase.CreateTypeLookup();
    }

    #endregion

    #region Geometry

    /// <summary>Tâm vùng sinh: Spawn Center nếu có, không thì Main Camera.</summary>
    public Vector2 SpawnCenterPosition => settings.SpawnCenter != null ? (Vector2)settings.SpawnCenter.position :
        Camera.main != null ? (Vector2)Camera.main.transform.position : Vector2.zero;

    /// <summary>Camera làm tâm Path; bỏ trống thì dùng Main Camera.</summary>
    public Camera FormationCamera => settings.FormationCamera != null ? settings.FormationCamera : Camera.main;

    /// <summary>Tạo lại điểm viền theo tâm hiện tại rồi chọn ngẫu nhiên một điểm.</summary>
    public Vector2 ChoosePerimeterPoint()
    {
        FillPerimeter(SpawnCenterPosition, settings.AreaSize, settings.PointsPerSide, spawnPoints);
        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
    }

    // Các điểm trên viền hình chữ nhật, không lặp các góc. Dùng chung cho spawn và gizmo.
    private static void FillPerimeter(Vector2 center, Vector2 areaSize, int pointsPerSide, List<Vector2> destination)
    {
        destination.Clear();
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

    #endregion

    #region Validation

    /// <summary>Kiểm tra phe, visual và cấu hình cấp của enemy/boss; báo lỗi nếu thiếu.</summary>
    public void ValidateCharacter(CharacterDataSO data)
    {
        if (data == null || data.Team != CharacterTeam.Enemy || data.VisualPrefab == null)
            throw new InvalidOperationException("Spawner cần Enemy/Boss có visual hợp lệ.");
        if (!levelConfigs.TryGetValue(data.CharacterType, out var levels))
            throw new InvalidOperationException("Thiếu cấu hình level cho enemy/boss: " + data.name);
        levels.GetTotalBonus(1);
    }

    /// <summary>Kiểm tra tâm spawn, kích thước vùng, số điểm cạnh và tọa độ Z.</summary>
    public void ValidatePerimeter()
    {
        if (settings.SpawnCenter == null && Camera.main == null)
            throw new InvalidOperationException("Cần gán Spawn Center hoặc Main Camera (GameSceneInstaller > Stage).");
        if (!Positive(settings.AreaSize.x) || !Positive(settings.AreaSize.y) || settings.PointsPerSide < 2 || !Finite(settings.SpawnZ))
            throw new InvalidOperationException("Vùng spawn, số điểm cạnh và tọa độ Z phải hợp lệ.");
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    #endregion

    #region Spawn And Despawn

    /// <summary>Tạo AI mặc định với cấu hình tách đàn và phản lực hiện tại.</summary>
    public EnemyController CreateController()
        => new EnemyController(players, characterDespawn, settings.Separation, settings.Recoil);

    /// <summary>Tính bonus theo cấp rồi tạo nhân vật; dọn controller nếu tạo thất bại.</summary>
    public CharacterView Spawn(CharacterDataSO data, int level, Vector2 point, ICharacterController controller = null)
    {
        ValidateCharacter(data);
        if (level < 1 || !Finite(point.x) || !Finite(point.y) || !Finite(settings.SpawnZ))
            throw new ArgumentException("Cấp và vị trí spawn phải hợp lệ.");
        var behavior = controller ?? CreateController();
        try
        {
            StatBonus bonus = levelConfigs[data.CharacterType].GetTotalBonus(level);
            // CharacterFactory áp IsBoss và cách hiển thị Boss; Boss và quái thường dùng chung AI.
            return characterSpawn.Spawn(data, bonus, behavior, new Vector3(point.x, point.y, settings.SpawnZ))
                ?? throw new InvalidOperationException("Không thể tạo enemy/boss: " + data.name);
        }
        catch { behavior.Dispose(); throw; }
    }

    /// <summary>Sinh Elite bằng AI thường rồi áp dụng tăng HP và scale riêng cho lượt này.</summary>
    public CharacterView SpawnElite(CharacterDataSO data, int level, Vector2 point, float scale, float healthMultiplier)
    {
        if (scale < 1f || !Finite(scale) || healthMultiplier < 1f || !Finite(healthMultiplier))
            throw new ArgumentOutOfRangeException(nameof(scale));
        CharacterView view = Spawn(data, level, point);
        try
        {
            view.IsElite = true;
            view.transform.localScale *= scale;
            if (healthMultiplier > 1f)
                view.RuntimeData.Stats.AddSource(new StatModifier(StatType.MaxHealth, StatOperation.Percent,
                    (healthMultiplier - 1f) * 100f));
            return view;
        }
        catch { Despawn(view); throw; }
    }

    /// <summary>Trả nhân vật về pool qua miền Character, không phát sự kiện chết (không rơi loot).</summary>
    public void Despawn(CharacterView character) => characterDespawn.Despawn(character);

    #endregion
}
