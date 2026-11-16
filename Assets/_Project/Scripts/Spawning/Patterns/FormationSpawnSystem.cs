using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Sinh và quản lý nhiều lượt Pattern độc lập trong cùng hệ thống.</summary>
public sealed class FormationSpawnSystem : IStageSpawnSystem, IStageSpawnStatus, IUpdateStage, ILateUpdateStage
{
    #region Snapshot và dependency

    /// <summary>Snapshot một lượt Pattern: nhóm, quái, cấp, quota và lịch sinh.</summary>
    private sealed class Run
    {
        public EnemyPatternSO.RuntimeData config;
        public EnemyFormationRuntime group;
        public CharacterDataSO[] enemies;
        public int level, spawned, total;
        public float interval, startsAt, nextSpawn;
    }
    private readonly IEnemySpawnValidation spawnValidation;
    private readonly IEnemySpawn spawnerEnemySpawn;
    private readonly IEnemyDespawn spawnerEnemyDespawn;
    private readonly IEnemySpawnGeometry spawnGeometry;
    private readonly IPlayerReader players;
    private readonly ISpatialInvalidation spatialSpatialInvalidation;
    private readonly StageSpawnRoster roster;
    private readonly List<Run> runs = new List<Run>();
    #endregion

    #region Thống kê và khởi tạo

    /// <summary>Đếm thành viên sống của mọi nhóm đang được quản lý.</summary>
    public int AliveCount => roster.AliveCount;
    /// <summary>True khi mọi nhóm đã sinh đủ số thành viên dự kiến.</summary>
    public bool DeploymentComplete
    {
        get { foreach (var run in runs) if (run.spawned < run.total) return false; return true; }
    }

    /// <summary>Nhận spawner, player và spatialSpatialInvalidation trực tiếp qua constructor.</summary>
    public FormationSpawnSystem(
        IEnemySpawnService enemySpawnService,
        IPlayerSystem playerSystem,
        ISpatialManager spatialManager)
    {
        this.spawnValidation = enemySpawnService.GetEnemySpawnValidation();
        this.spawnerEnemySpawn = enemySpawnService.GetEnemySpawn();
        this.spawnerEnemyDespawn = enemySpawnService.GetEnemyDespawn();
        this.spawnGeometry = enemySpawnService.GetEnemySpawnGeometry();
        this.players = playerSystem.GetPlayerReader();
        this.spatialSpatialInvalidation = spatialManager.GetInvalidation();
        roster = new StageSpawnRoster(enemySpawnService.GetEnemyDespawn());
    }

    #endregion

    #region Kiểm tra và sinh nhóm

    /// <summary>Kiểm tra asset, cấp quái và camera cần cho Pattern chạy Path.</summary>
    public void Validate(IStageSpawnData data)
    {
        var config = data as EnemyPatternSO ?? throw new ArgumentException("FormationSpawnSystem requires EnemyPatternSO.");
        config.Validate();
        foreach (var enemy in config.enemies) spawnValidation.ValidateCharacter(enemy);
        if (config.type != EnemyPatternType.Area && spawnGeometry.FormationCamera == null)
            throw new InvalidOperationException("Formation/SequentialPath cần Formation Camera hoặc Main Camera.");
    }

    /// <summary>Đăng ký Pattern chờ mốc delay tính từ đầu Stage.</summary>
    public void Execute(IStageSpawnData data)
    {
        var source = data as EnemyPatternSO ?? throw new ArgumentException("FormationSpawnSystem requires EnemyPatternSO.");
        var config = source.CreateRuntimeData();
        runs.Add(new Run
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

    /// <summary>Tạo runtime tại thời điểm bắt đầu để lấy đúng camera và tâm player hiện tại.</summary>
    private void StartRun(Run run)
    {
        var config = run.config;
        var camera = spawnGeometry.FormationCamera;
        var route = config.CreateRoute(camera != null ? camera.transform : null);
        run.group = new EnemyFormationRuntime(config, route,
                () => players.CurrentPlayer != null ? (Vector2)players.CurrentPlayer.transform.position : Vector2.zero,
                spawnerEnemyDespawn.Despawn);
        roster.Add(run.group);
        if (config.Type == EnemyPatternType.SequentialPath) SpawnMember(run);
        else while (run.spawned < run.total) SpawnMember(run);
    }

    /// <summary>Tạo thành viên theo chỉ số, gắn vào nhóm và Seal khi đủ quota.</summary>
    private void SpawnMember(Run run)
    {
        var controller = spawnerEnemySpawn.CreateController();
        var enemy = spawnerEnemySpawn.Spawn(run.enemies[UnityEngine.Random.Range(0, run.enemies.Length)],
            run.level, run.group.GetSpawnPosition(run.spawned), controller);
        try { run.group.Add(enemy, controller, run.spawned); }
        catch { spawnerEnemyDespawn.Despawn(enemy); throw; }
        run.spawned++;
        if (run.spawned == run.total) run.group.Seal();
    }

    #endregion

    #region Cập nhật chuyển động

    /// <summary>Kích hoạt theo giờ Stage; chia bước di chuyển tại từng mốc sinh thành viên.</summary>
    public void Tick(float stageTime, float deltaTime)
    {
        if (deltaTime <= 0f) return;
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            var run = runs[i];
            if (stageTime < run.startsAt) continue;
            if (run.group == null) StartRun(run);
            float cursor = Mathf.Max(run.startsAt, stageTime - deltaTime);
            // Advance old members up to each birth, so a new member never receives time before its birth.
            while (!run.group.IsBroken && run.spawned < run.total && run.nextSpawn <= stageTime)
            {
                run.group.Tick(Mathf.Max(0f, run.nextSpawn - cursor));
                cursor = Mathf.Max(cursor, run.nextSpawn);
                if (run.group.IsBroken) break;
                SpawnMember(run);
                run.nextSpawn += run.interval;
            }
            run.group.Tick(Mathf.Max(0f, stageTime - cursor));
            if (run.group.IsBroken) runs.RemoveAt(i);
        }
    }

    /// <summary>Bù dịch chuyển camera/player và báo spatialSpatialInvalidation khi vị trí nhóm thay đổi.</summary>
    public void LateTick()
    {
        bool moved = false;
        foreach (var run in runs) if (run.group != null) moved |= run.group.ApplyCameraMotion();
        if (moved) spatialSpatialInvalidation.Invalidate();
    }

    #endregion

    #region Dừng và giải phóng

    /// <summary>Hủy lịch sinh rồi giao roster thu hồi các nhóm đang sở hữu.</summary>
    public void Stop()
    {
        runs.Clear();
        roster.Clear();
    }
    #endregion
}
