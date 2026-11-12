using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

// Wave quản lý enemy thường và các thực thể đội hình. Boss chỉ nhờ spawner tạo nhân vật tại điểm spawn;
// không lấy enemy của Wave để tạo hộ tống hoặc đấu trường nữa.
[DefaultExecutionOrder(10000)]
public sealed class EnemySpawnManager : MonoBehaviour
{
    #region Scene Configuration

    [Header("References")]
    [SerializeField] private Transform spawnCenter;

    [Tooltip("Camera làm tâm cho Path đội hình. Bỏ trống để dùng Main Camera. Spawn Center chỉ dành cho quái thường.")]
    [SerializeField] private Camera formationCamera;

    [Header("Enemy Separation")]
    [SerializeField] private EnemyController.SeparationSettings separation = new EnemyController.SeparationSettings();

    [Header("Ordinary Attack Recoil")]
    [FormerlySerializedAs("contactAttack")]
    [SerializeField] private EnemyController.RecoilSettings recoil = new EnemyController.RecoilSettings();

    [Header("Spawn Perimeter")]
    [Tooltip("Chiều rộng/cao vùng chọn điểm spawn quanh Spawn Center, theo world unit.")]
    [SerializeField] private Vector2 areaSize = new Vector2(10f, 6f);

    [Min(2)] [SerializeField] private int pointsPerSide = 4;
    [SerializeField] private float spawnZ;

    #endregion

    #region Dependencies And Run State

    private readonly List<Vector2> spawnPoints = new List<Vector2>();
    private readonly List<CharacterView> activeEnemies = new List<CharacterView>();
    private readonly List<EnemyFormationRuntime> groups = new List<EnemyFormationRuntime>();
    private readonly WaveProgress progress = new WaveProgress();
    private Dictionary<CharacterType, CharacterLevelConfigSO> levelConfigs;
    private CharacterFactory factory;
    private ISpatialManager spatial;
    private PlayerManager players;
    private WaveConfigSO waveConfig;
    private float nextSpawnTime;

    // Dữ liệu hình học dùng chung với Workbench; đọc không thay đổi cấu hình scene.
    public Vector2 SpawnCenterPosition => spawnCenter != null ? (Vector2)spawnCenter.position :
        Camera.main != null ? (Vector2)Camera.main.transform.position : (Vector2)transform.position;
    public Camera FormationCamera => formationCamera != null ? formationCamera : Camera.main;
    public float BaseKnockbackDistance => recoil != null ? recoil.knockbackDistance : 0.35f;
    public Vector2 SpawnAreaSize => areaSize;
    public float SpawnPlaneZ => spawnZ;

    public int CurrentWaveNumber => progress.WaveIndex + 1;
    public int CurrentEnemyLevel => progress.EnemyLevel;
    public int SpawnedInWave => progress.SpawnedInWave;
    public int AliveEnemyCount => CountAliveEnemies();
    public event Action<int> WaveSpawnCompleted;

    [Inject]
    public void Construct(CharacterFactory factory, PlayerManager players, LevelDatabaseSO levelDatabase, ISpatialManager spatial)
    {
        this.factory = factory;
        this.spatial = spatial;
        this.players = players;
        levelConfigs = levelDatabase.CreateTypeLookup();
    }

    private void Awake()
    {
        if (spawnCenter == null && Camera.main != null)
        {
            spawnCenter = Camera.main.transform;
        }
    }

    #endregion

    #region Configuration And Wave Lifecycle

    public void ValidateConfig(WaveConfigSO config)
    {
        config.Validate();

        for (int i = 0; i < config.Count; i++)
        {
            foreach (CharacterDataSO enemy in config.GetWave(i).enemies)
            {
                ValidateCharacter(enemy);
            }
        }

        if (spawnCenter == null)
        {
            throw new InvalidOperationException("Cần gán Spawn Center hoặc camera MainCamera.");
        }

        for (int i = 0; i < config.Count; i++)
        {
            if (config.GetWave(i).UsesFormation && FormationCamera == null)
            {
                throw new InvalidOperationException("Wave có đội hình cần Formation Camera hoặc Main Camera.");
            }
        }
    }

    public void ValidateCharacter(CharacterDataSO enemy)
    {
        if (!levelConfigs.TryGetValue(enemy.CharacterType, out CharacterLevelConfigSO config))
        {
            throw new InvalidOperationException("Thiếu cấu hình level cho enemy/boss: " + enemy.name);
        }

        config.GetTotalBonus(1);
    }

    public void BeginWave(WaveConfigSO config, int index)
    {
        waveConfig = config;
        progress.BeginWave(index);
        nextSpawnTime = Time.time;
    }

    public void StopFormations()
    {
        foreach (EnemyFormationRuntime group in groups)
        {
            group.Dispose();
        }

        groups.Clear();
    }

    public void ResetRun()
    {
        StopFormations();

        while (activeEnemies.Count > 0)
        {
            int last = activeEnemies.Count - 1;
            CharacterView enemy = activeEnemies[last];
            activeEnemies.RemoveAt(last);

            if (enemy != null)
            {
                enemy.Released -= OnEnemyReleased;
                factory.Despawn(enemy);
            }
        }

        waveConfig = null;
        progress.Reset();
        nextSpawnTime = Time.time;
    }

    #endregion

    #region Spawn And Group Updates

    public void TickSpawning()
    {
        if (!isActiveAndEnabled || waveConfig == null || spawnCenter == null ||
            players.CurrentPlayer == null || players.RuntimeData == null || !players.RuntimeData.IsAlive || Time.deltaTime <= 0f)
        {
            return;
        }

        // Các nhóm thuộc wave trước vẫn phải di chuyển ngay cả khi wave hiện tại đã spawn đủ.
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            groups[i].Tick(Time.deltaTime);

            if (groups[i].IsBroken)
            {
                groups.RemoveAt(i);
            }
        }

        if (!progress.CanSpawn(waveConfig) || Time.time < nextSpawnTime)
        {
            return;
        }

        WaveConfigSO.Wave wave = waveConfig.GetWave(progress.WaveIndex);
        nextSpawnTime = Time.time + wave.spawnInterval;

        // Lịch đã dành đủ ngân sách: trước/sau mốc là quái thường, trong mốc là nhóm đầy đủ.
        if (wave.UsesFormation && wave.GetFormationSpawnPlan().TryGetFormationIndex(progress.SpawnedInWave, out int formationIndex))
        {
            SpawnFormation(wave, wave.formationSpawns[formationIndex]);
        }
        else
        {
            SpawnWaveEnemy(wave, ChooseSpawnPoint(), null, Vector2.zero);
        }
    }

    private void SpawnFormation(WaveConfigSO.Wave wave, EnemyFormationSpawn entry)
    {
        EnemyFormationSettings settings = entry.settings.Copy();
        int remaining = wave.spawnCount - progress.SpawnedInWave;
        int count = entry.formation.SlotCount;

        // Không âm thầm đổi một nhóm đã đặt lịch thành quái thường nếu cấu hình bị sửa sai.
        if (remaining < count)
        {
            throw new InvalidOperationException("Wave không còn đủ enemy cho một đội hình đầy đủ. Kiểm tra lại lịch spawn.");
        }

        Camera camera = FormationCamera;

        if (camera == null)
        {
            throw new InvalidOperationException("Không tìm thấy camera cho Path đội hình.");
        }

        // Nhóm sinh trực tiếp tại A. Route chốt hình học và offset tâm, chỉ bám vị trí camera.
        var route = new EnemyPathRoute(EnemyPathBuilder.Build(entry.path, camera.transform.position), camera.transform);
        Vector2[] offsets = FormationBuilder.Build(entry.formation);
        var group = new EnemyFormationRuntime(route, settings, BaseKnockbackDistance, entry.formation.Speed);
        groups.Add(group);

        for (int slot = 0; slot < count; slot++)
        {
            Vector2 point = route.MemberPosition(settings, 0f, slot, offsets[slot]);
            SpawnWaveEnemy(wave, point, group, offsets[slot]);
        }

        group.Seal();
    }

    private void SpawnWaveEnemy(WaveConfigSO.Wave wave, Vector2 point, EnemyFormationRuntime group, Vector2 offset)
    {
        CharacterDataSO template = wave.enemies[UnityEngine.Random.Range(0, wave.enemies.Length)];
        var controller = new EnemyController(players, factory, separation, recoil);
        CharacterView enemy = SpawnCharacter(template, progress.EnemyLevel, point, controller);

        if (enemy == null)
        {
            throw new InvalidOperationException("Không thể spawn enemy của wave.");
        }

        activeEnemies.Add(enemy);
        enemy.Released += OnEnemyReleased;
        group?.Add(enemy, controller, offset);
        progress.RecordSpawn();

        if (!progress.CanSpawn(waveConfig))
        {
            WaveSpawnCompleted?.Invoke(CurrentWaveNumber);
        }
    }

    // Boss không vào activeEnemies hoặc ngân sách quái thường. Stage sở hữu danh sách boss.
    public CharacterView SpawnBoss(CharacterDataSO boss, int level)
    {
        return SpawnCharacter(boss, level, ChooseSpawnPoint(), new EnemyController(players, factory, separation, recoil));
    }

    private CharacterView SpawnCharacter(CharacterDataSO template, int level, Vector2 point, EnemyController controller)
    {
        StatBonus bonus = levelConfigs[template.CharacterType].GetTotalBonus(level);
        return factory.Spawn(template, bonus, controller, new Vector3(point.x, point.y, spawnZ));
    }

    #endregion

    #region Spawn Geometry And Tracking

    private Vector2 ChooseSpawnPoint()
    {
        BuildSpawnPoints();
        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
    }

    private void BuildSpawnPoints()
    {
        GetSpawnPoints(spawnPoints);
    }

    // Tool và gameplay gọi cùng thuật toán để các điểm minh họa luôn trùng điểm spawn thật.
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

    private int CountAliveEnemies()
    {
        int count = 0;

        foreach (CharacterView enemy in activeEnemies)
        {
            if (enemy != null && enemy.RuntimeData != null && enemy.RuntimeData.IsAlive)
            {
                count++;
            }
        }

        return count;
    }

    private void OnEnemyReleased(CharacterView enemy)
    {
        activeEnemies.Remove(enemy);
    }

    private void LateUpdate()
    {
        bool moved = false;

        foreach (EnemyFormationRuntime group in groups)
        {
            moved |= group.ApplyCameraMotion();
        }

        if (moved)
        {
            spatial?.Invalidate();
        }
    }

    private void OnDestroy()
    {
        ResetRun();
        WaveSpawnCompleted = null;
    }

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
