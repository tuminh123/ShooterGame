using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Sinh và quản lý nhiều lượt Pattern độc lập trong cùng hệ thống. Do StageManager tạo và tick; không bind vào Zenject.</summary>
internal sealed class FormationSpawnSystem
{
    #region Snapshot & Dependencies

    /// <summary>Snapshot một lượt Pattern: nhóm, quái, cấp, quota và lịch sinh.</summary>
    private sealed class ActiveFormation
    {
        public EnemyPatternSO.RuntimeData config;
        public EnemyFormationRuntime group;
        public CharacterDataSO[] enemies;
        public int level, spawned, total;
        public float interval, startsAt, nextSpawn;
    }
    private readonly EnemySpawner spawner; // Tạo/thu hồi quái, vùng sinh; do StageManager tạo và dùng chung cho mọi system.
    private readonly IPlayerReader players;
    private readonly ISpatialInvalidation spatialInvalidation;
    private readonly StageSpawnRoster roster;
    private readonly List<ActiveFormation> formations = new List<ActiveFormation>();
    #endregion

    #region Counts & Construction

    /// <summary>Đếm thành viên sống của mọi nhóm đang được quản lý.</summary>
    public int AliveCount => roster.AliveCount;
    /// <summary>True khi mọi nhóm đã sinh đủ số thành viên dự kiến.</summary>
    public bool DeploymentComplete
    {
        get { foreach (var formation in formations) if (formation.spawned < formation.total) return false; return true; }
    }

    /// <summary>Nhận spawner, player và vai trò báo lưới không gian dựng lại qua constructor.</summary>
    public FormationSpawnSystem(
        EnemySpawner spawner,
        IPlayerReader playerReader,
        ISpatialInvalidation spatialInvalidation)
    {
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.players = playerReader;
        this.spatialInvalidation = spatialInvalidation; // SimulationManager (SimulationContracts.cs).
        roster = new StageSpawnRoster(spawner);
    }

    #endregion

    #region Validation & Group Spawning

    /// <summary>Kiểm tra asset, cấp quái và camera cần cho Pattern chạy Path.</summary>
    public void Validate(EnemyPatternSO config)
    {
        config.Validate();
        foreach (var enemy in config.Enemies) spawner.ValidateCharacter(enemy);
        if (config.Type != EnemyPatternType.Area && spawner.FormationCamera == null)
            throw new InvalidOperationException("Formation/SequentialPath cần Formation Camera hoặc Main Camera.");
    }

    /// <summary>Đăng ký Pattern chờ mốc delay tính từ đầu Stage.</summary>
    public void Execute(EnemyPatternSO source)
    {
        var config = source.CreateRuntimeData();
        formations.Add(new ActiveFormation
        {
            config = config,
            enemies = config.Enemies,
            level = config.EnemyLevel,
            total = config.Count,
            interval = config.SpawnInterval,
            startsAt = config.Delay,
            nextSpawn = config.Delay + config.SpawnInterval
        });
    }

    /// <summary>Tạo runtime tại thời điểm bắt đầu để lấy đúng camera và chốt tâm Area tại vị trí Player hiện tại.</summary>
    private void StartFormation(ActiveFormation formation)
    {
        var config = formation.config;
        var camera = spawner.FormationCamera;
        var route = config.CreateRoute(camera != null ? camera.transform : null);
        // Area lấy Player làm tâm chỉ ở thời điểm spawn; sau đó vòng đứng yên trong world.
        Vector2 areaCenter = players.CurrentPlayer != null ? (Vector2)players.CurrentPlayer.transform.position : Vector2.zero;
        formation.group = new EnemyFormationRuntime(config, route, areaCenter, spawner.Despawn);
        roster.Add(formation.group);
        if (config.Type == EnemyPatternType.SequentialPath) SpawnMember(formation);
        else while (formation.spawned < formation.total) SpawnMember(formation);
    }

    /// <summary>Tạo thành viên theo chỉ số, gắn vào nhóm và Seal khi đủ quota.</summary>
    private void SpawnMember(ActiveFormation formation)
    {
        var controller = spawner.CreateController();
        var enemy = spawner.Spawn(formation.enemies[UnityEngine.Random.Range(0, formation.enemies.Length)],
            formation.level, formation.group.GetSpawnPosition(formation.spawned), controller);
        try { formation.group.Add(enemy, controller, formation.spawned); }
        catch { spawner.Despawn(enemy); throw; }
        formation.spawned++;
        if (formation.spawned == formation.total) formation.group.Seal();
    }

    #endregion

    #region Movement Update

    /// <summary>Kích hoạt theo giờ Stage; chia bước di chuyển tại từng mốc sinh thành viên.</summary>
    public void Tick(float stageTime, float deltaTime)
    {
        if (deltaTime <= 0f) return;
        for (int i = formations.Count - 1; i >= 0; i--)
        {
            var formation = formations[i];
            if (stageTime < formation.startsAt) continue;
            if (formation.group == null) StartFormation(formation);
            float cursor = Mathf.Max(formation.startsAt, stageTime - deltaTime);
            // Advance old members up to each birth, so a new member never receives time before its birth.
            while (!formation.group.IsBroken && formation.spawned < formation.total && formation.nextSpawn <= stageTime)
            {
                formation.group.Tick(Mathf.Max(0f, formation.nextSpawn - cursor));
                cursor = Mathf.Max(cursor, formation.nextSpawn);
                if (formation.group.IsBroken) break;
                SpawnMember(formation);
                formation.nextSpawn += formation.interval;
            }
            formation.group.Tick(Mathf.Max(0f, stageTime - cursor));
            if (formation.group.IsBroken) formations.RemoveAt(i);
        }
    }

    /// <summary>Bù dịch chuyển camera/player và báo spatialInvalidation khi vị trí nhóm thay đổi.</summary>
    public void LateTick()
    {
        bool moved = false;
        foreach (var formation in formations) if (formation.group != null) moved |= formation.group.ApplyCameraMotion();
        if (moved) spatialInvalidation.Invalidate();
    }

    #endregion

    #region Stop & Dispose

    /// <summary>Hủy lịch sinh rồi giao roster thu hồi các nhóm đang sở hữu.</summary>
    public void Stop()
    {
        formations.Clear();
        roster.Clear();
    }
    #endregion
}
