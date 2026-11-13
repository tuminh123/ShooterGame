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
        public EnemyPatternSO config;
        public EnemyFormationRuntime group;
        public CharacterDataSO[] enemies;
        public int level, spawned, total;
        public float interval, startsAt, nextSpawn;
    }

    private readonly EnemySpawnManager spawner;
    private readonly PlayerManager players;
    private readonly ISpatialManager spatial;
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

    /// <summary>Nhận spawner, player và spatial trực tiếp qua constructor.</summary>
    public FormationSpawnSystem(EnemySpawnManager spawner, PlayerManager players, ISpatialManager spatial)
    {
        this.spawner = spawner;
        this.players = players;
        this.spatial = spatial;
        roster = new StageSpawnRoster(spawner);
    }

    #endregion

    #region Kiểm tra và sinh nhóm

    /// <summary>Kiểm tra asset, cấp quái và camera cần cho Pattern chạy Path.</summary>
    public void Validate(IStageSpawnData data)
    {
        var config = data as EnemyPatternSO ?? throw new ArgumentException("FormationSpawnSystem requires EnemyPatternSO.");
        config.Validate();
        foreach (var enemy in config.enemies) spawner.ValidateCharacter(enemy);
        if (config.type != EnemyPatternType.Area && spawner.FormationCamera == null)
            throw new InvalidOperationException("Formation/SequentialPath cần Formation Camera hoặc Main Camera.");
    }

    /// <summary>Đăng ký Pattern chờ mốc delay tính từ đầu Stage.</summary>
    public void Execute(IStageSpawnData data)
    {
        var config = data as EnemyPatternSO ?? throw new ArgumentException("FormationSpawnSystem requires EnemyPatternSO.");
        runs.Add(new Run
        {
            config = config,
            enemies = (CharacterDataSO[])config.enemies.Clone(),
            level = config.enemyLevel,
            total = config.Count,
            interval = config.spawnInterval,
            startsAt = config.delay,
            nextSpawn = config.delay + config.spawnInterval
        });
    }

    /// <summary>Tạo runtime tại thời điểm bắt đầu để lấy đúng camera và tâm player hiện tại.</summary>
    private void StartRun(Run run)
    {
        var config = run.config;
        var camera = spawner.FormationCamera;
        var route = config.type == EnemyPatternType.Area ? null : new EnemyPathRoute(
            EnemyPathBuilder.Build(config.path, camera.transform.position), camera.transform);
        run.group = new EnemyFormationRuntime(config, route,
                () => players.CurrentPlayer != null ? (Vector2)players.CurrentPlayer.transform.position : Vector2.zero,
                spawner.Despawn);
        roster.Add(run.group);
        if (config.type == EnemyPatternType.SequentialPath) SpawnMember(run);
        else while (run.spawned < run.total) SpawnMember(run);
    }

    /// <summary>Tạo thành viên theo chỉ số, gắn vào nhóm và Seal khi đủ quota.</summary>
    private void SpawnMember(Run run)
    {
        var controller = spawner.CreateController();
        var enemy = spawner.Spawn(run.enemies[UnityEngine.Random.Range(0, run.enemies.Length)],
            run.level, run.group.GetSpawnPosition(run.spawned), controller);
        try { run.group.Add(enemy, controller, run.spawned); }
        catch { spawner.Despawn(enemy); throw; }
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

    /// <summary>Bù dịch chuyển camera/player và báo spatial khi vị trí nhóm thay đổi.</summary>
    public void LateTick()
    {
        bool moved = false;
        foreach (var run in runs) if (run.group != null) moved |= run.group.ApplyCameraMotion();
        if (moved) spatial.Invalidate();
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
