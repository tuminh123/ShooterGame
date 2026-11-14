using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>Điều phối các hệ thống spawn qua interface và quyết định kết quả màn.</summary>
public sealed class StageManager : ITickable, IDisposable
{
    #region Dependency, registry và trạng thái

    private StageDataSO data;
    private readonly Dictionary<string, StageDataSO> stages;
    private readonly DiContainer container;
    private readonly EnemySpawnManager spawner;
    private readonly PlayerManager players;
    private readonly GroundChunkManager ground;
    private readonly CurrencyService currencies;
    private readonly StageProgressService progress;
    private readonly Dictionary<Type, IStageSpawnSystem> systems = new Dictionary<Type, IStageSpawnSystem>();
    private readonly List<IStageSpawnSystem> systemOrder = new List<IStageSpawnSystem>();
    private readonly List<IUpdateStage> updates = new List<IUpdateStage>();
    private readonly List<ILateUpdateStage> lateUpdates = new List<ILateUpdateStage>();
    private readonly List<IStageSpawnStatus> statuses = new List<IStageSpawnStatus>();
    private readonly Dictionary<CurrencyType, long> resultRewards = new Dictionary<CurrencyType, long>();
    private bool running, started, clearRequested, resultGranted, disposed;
    private float stageTime;

    public StageDataSO CurrentStage => data;
    public StageDatabaseSO Database { get; }
    public bool ResultGranted => resultGranted;
    /// <summary>Thời gian gameplay từ đầu lượt; dừng tăng khi pause hoặc kết thúc.</summary>
    public float StageTime => stageTime;
    public event Action Cleared;
    public event Action<Exception> Failed;
    public event Action<StageSpawnWarning, float> SpawnScheduled;
    public event Action WarningsCleared;

    #endregion

    #region Khởi tạo và định tuyến hệ thống

    /// <summary>Tạo registry từ danh sách DataSO và đăng ký callback cuối frame.</summary>
    public StageManager(StageDatabaseSO database, EnemySpawnManager spawner, GroundChunkManager ground,
        PlayerManager players, CurrencyService currencies, StageProgressService progress, DiContainer container)
    {
        Database = database != null ? database : throw new ArgumentNullException(nameof(database));
        stages = database.CreateLookup();
        data = database.DefaultStage;
        this.ground = ground;
        this.currencies = currencies;
        this.progress = progress;
        this.container = container;
        this.spawner = spawner;
        this.players = players;

        BuildSystems();
        spawner.SpawnLateUpdate += LateTickSystems;
    }

    /// <summary>Chỉ tạo hệ thống của màn đang chọn, không chạy đồng thời các màn trong database.</summary>
    private void BuildSystems()
    {
        try
        {
            // Construct one system per Type using each DataSO's explicit factory. No reflection/Activator or type switch.
            foreach (var spawnData in data.SpawnData) Register(spawnData);
            if (systems.TryGetValue(typeof(WaveSpawnSystem), out var warningWaves))
                ((WaveSpawnSystem)warningWaves).SpawnScheduled += ForwardSpawnSchedule;
            if (systems.TryGetValue(typeof(BossSpawnSystem), out var warningBosses))
                ((BossSpawnSystem)warningBosses).SpawnScheduled += ForwardSpawnSchedule;
            if (systems.TryGetValue(typeof(WaveSpawnSystem), out var waveSystem) &&
                systems.TryGetValue(typeof(BossSpawnSystem), out var bossSystem))
            {
                var waves = (WaveSpawnSystem)waveSystem;
                var boss = (BossSpawnSystem)bossSystem;
                waves.AllWavesSpawned += boss.NotifyAllWavesSpawned;
                waves.FinalWaveCleared += boss.NotifyFinalWaveCleared;
            }
        }
        catch
        {
            try { ReleaseSystems(); } catch (Exception cleanup) { Debug.LogException(cleanup); }
            throw;
        }
    }

    /// <summary>Tạo một instance cho mỗi SystemType; từ chối factory trả sai loại.</summary>
    private IStageSpawnSystem Register(IStageSpawnData spawnData)
    {
        Type key = spawnData.SystemType;
        if (systems.TryGetValue(key, out var existing)) return existing;
        IStageSpawnSystem system = spawnData.CreateSystem(container);
        if (system == null || system.GetType() != key)
            throw new InvalidOperationException("Spawn factory must return its declared SystemType: " + key);
        systems.Add(key, system);
        systemOrder.Add(system);
        if (!(system is IStageSpawnStatus status))
            throw new InvalidOperationException("Spawn system must implement IStageSpawnStatus: " + key);
        statuses.Add(status);
        if (system is IUpdateStage update) updates.Add(update);
        if (system is ILateUpdateStage lateUpdate) lateUpdates.Add(lateUpdate);
        return system;
    }

    /// <summary>Lấy hệ thống đã đăng ký; báo lỗi nếu loại cấu hình thay đổi sau khởi tạo.</summary>
    private IStageSpawnSystem GetSystem(IStageSpawnData spawnData)
    {
        if (!systems.TryGetValue(spawnData.SystemType, out var system))
            throw new InvalidOperationException("Spawn configuration changed after Stage initialization. Restart the scene to rebuild the registry.");
        return system;
    }

    #endregion

    #region Chuẩn bị lượt chơi

    // Công bố lịch Boss chờ sinh xong wave nếu mọi chuỗi đều có thời điểm xác định.
    private void PrepareSpawnSchedule()
    {
        if (!systems.TryGetValue(typeof(BossSpawnSystem), out var system)) return;
        float latest = 0f;
        bool found = false;
        foreach (var spawnData in data.SpawnData)
        {
            if (!(spawnData is WaveConfigSO waves)) continue;
            found = true;
            float? time = WaveSpawnSystem.GetScheduledCompletion(waves);
            if (!time.HasValue) { ((BossSpawnSystem)system).ScheduledWaveCompletion = null; return; }
            latest = Mathf.Max(latest, time.Value);
        }
        ((BossSpawnSystem)system).ScheduledWaveCompletion = found ? (float?)latest : null;
    }

    // Chỉ chuyển tiếp loại sự kiện và mốc giờ gameplay; không chứa cấu hình thời gian của UI.
    private void ForwardSpawnSchedule(StageSpawnWarning kind, float spawnAt) => SpawnScheduled?.Invoke(kind, spawnAt);

    /// <summary>Chọn màn bằng ID ổn định trước StartRun; dọn registry và kết quả của màn cũ.</summary>
    public void SelectStage(string stageId)
    {
        EnsureNotDisposed();
        if (running)
            throw new InvalidOperationException("Dừng lượt hiện tại trước khi chọn Stage khác.");
        if (string.IsNullOrWhiteSpace(stageId) || !stages.TryGetValue(stageId, out var next))
            throw new ArgumentException("StageId không có trong Stage Database: " + stageId, nameof(stageId));
        next.Validate();
        ReleaseSystems();
        data = next;
        BuildSystems();
        stageTime = 0f;
        clearRequested = resultGranted = false;
        resultRewards.Clear();
    }

    /// <summary>Kiểm tra từng hệ thống và tính thử thưởng để phát hiện cấu hình lỗi.</summary>
    public void Validate()
    {
        data.Validate();
        foreach (var spawnData in data.SpawnData) GetSystem(spawnData).Validate(spawnData);
        BuildRewards(true);
        BuildRewards(false);
    }

    /// <summary>Dọn lượt cũ, áp dụng map và đặt lại trạng thái cho lượt mới.</summary>
    public void BeginRun()
    {
        EnsureNotDisposed();
        StopRun();
        Validate();
        if (!ground.SetMap(data.MapKey))
            throw new InvalidOperationException("Không thể áp dụng map của Stage: " + data.MapKey);
        resultRewards.Clear();
        resultGranted = false;
        clearRequested = false;
        started = false;
        stageTime = 0f;
        running = true;
    }

    #endregion

    #region Cập nhật và kết thúc màn

    /// <summary>Cập nhật Stage và chuyển ngoại lệ sang luồng xử lý lỗi.</summary>
    public void Tick()
    {
        try { TickStage(); }
        catch (Exception exception) { FailStage(exception); }
    }

    /// <summary>Chạy dữ liệu khi player sẵn sàng; thắng khi mọi hệ thống sinh xong và hết quái sống.</summary>
    private void TickStage()
    {
        if (disposed || !running || Time.deltaTime <= 0f || players.RuntimeData == null || !players.RuntimeData.IsAlive)
            return;
        if (!started)
        {
            // Start only after GameManager has spawned the player, so Area uses the actual player center.
            started = true;
            PrepareSpawnSchedule();
            foreach (var spawnData in data.SpawnData)
            {
                GetSystem(spawnData).Execute(spawnData);
                if (!running || players.RuntimeData == null || !players.RuntimeData.IsAlive) return;
            }
        }
        float deltaTime = Time.deltaTime;
        stageTime += deltaTime;
        foreach (var system in updates)
        {
            system.Tick(stageTime, deltaTime);
            if (!running || players.RuntimeData == null || !players.RuntimeData.IsAlive) return;
        }
        foreach (var system in statuses)
            if (!system.DeploymentComplete || system.AliveCount > 0) return;
        if (clearRequested) return;
        clearRequested = true;
        StopRun();
        Cleared?.Invoke();
    }

    /// <summary>Gọi bước cập nhật cuối frame khi Stage đang chạy và gameplay không tạm dừng.</summary>
    private void LateTickSystems()
    {
        if (disposed || !running || !started || Time.deltaTime <= 0f ||
            players.RuntimeData == null || !players.RuntimeData.IsAlive) return;
        try { foreach (var system in lateUpdates) system.LateTick(); }
        catch (Exception exception) { FailStage(exception); }
    }

    /// <summary>Dừng mọi hệ thống, gộp lỗi cleanup và thông báo thất bại.</summary>
    private void FailStage(Exception exception)
    {
        try { StopRun(); }
        catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
        if (Failed != null) Failed(exception);
        else Debug.LogException(exception);
    }

    #endregion

    #region Thưởng và tiến trình lần đầu

    /// <summary>Dừng màn và cấp thưởng thắng/thua đúng một lần cho lượt hiện tại.</summary>
    public void FinishRun(bool won)
    {
        EnsureNotDisposed();
        // Dừng clock và spawn của Stage, giữ nhân vật đang sống để phát animation kết quả.
        running = false;
        started = false;
        if (resultGranted)
        {
            return;
        }
        Dictionary<CurrencyType, long> rewards = BuildRewards(won);
        // IO failures retain the combined snapshot; never apply the reward twice.
        if (won)
        {
            progress.GrantVictoryRewards(data.StageId, rewards);
        }
        else
        {
            currencies.AddBatch(rewards);
        }
        resultGranted = true;
        foreach (KeyValuePair<CurrencyType, long> reward in rewards)
        {
            resultRewards.Add(reward.Key, reward.Value);
        }
    }

    /// <summary>Lấy tổng thưởng Stage của loại tiền; trả 0 nếu không có.</summary>
    public long GetResultReward(CurrencyType type)
        => resultRewards.TryGetValue(type, out long amount) ? amount : 0L;

    /// <summary>Gộp thưởng kết quả; chỉ thêm thưởng lần đầu nếu Stage chưa được thắng trước đó.</summary>
    private Dictionary<CurrencyType, long> BuildRewards(bool won)
    {
        var rewards = new Dictionary<CurrencyType, long>();
        if (won && !progress.HasFirstClearedStage(data.StageId))
        {
            AddRewards(rewards, data.FirstClearStageRewards);
        }
        AddRewards(rewards, won ? data.VictoryRewards : data.DefeatRewards);
        return rewards;
    }

    /// <summary>Cộng thưởng theo loại tiền và phát hiện tràn số bằng checked.</summary>
    private static void AddRewards(Dictionary<CurrencyType, long> totals, IReadOnlyList<StageDataSO.CurrencyReward> rewards)
    {
        foreach (StageDataSO.CurrencyReward reward in rewards)
        {
            totals.TryGetValue(reward.type, out long previous);
            totals[reward.type] = checked(previous + reward.amount);
        }
    }

    #endregion

    #region Dừng và giải phóng

    /// <summary>Hủy lịch sinh và dọn mọi hệ thống; tiếp tục cleanup khi một hệ thống lỗi.</summary>
    public void StopRun()
    {
        running = false;
        started = false;
        WarningsCleared?.Invoke();
        List<Exception> failures = null;
        foreach (var system in systemOrder)
        {
            try { system.Stop(); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("Stage spawn cleanup failed.", failures);
    }

    /// <summary>Chặn thao tác khi StageManager đã được giải phóng.</summary>
    private void EnsureNotDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(StageManager));
    }

    /// <summary>Gỡ liên kết Wave/Boss, thu hồi entity và xóa registry trước khi đổi màn hoặc dispose.</summary>
    private void ReleaseSystems()
    {
        if (systems.TryGetValue(typeof(WaveSpawnSystem), out var warningWaves))
            ((WaveSpawnSystem)warningWaves).SpawnScheduled -= ForwardSpawnSchedule;
        if (systems.TryGetValue(typeof(BossSpawnSystem), out var warningBosses))
            ((BossSpawnSystem)warningBosses).SpawnScheduled -= ForwardSpawnSchedule;
        if (systems.TryGetValue(typeof(WaveSpawnSystem), out var waveSystem) &&
            systems.TryGetValue(typeof(BossSpawnSystem), out var bossSystem))
        {
            var waves = (WaveSpawnSystem)waveSystem;
            var boss = (BossSpawnSystem)bossSystem;
            waves.AllWavesSpawned -= boss.NotifyAllWavesSpawned;
            waves.FinalWaveCleared -= boss.NotifyFinalWaveCleared;
        }
        try { StopRun(); }
        finally
        {
            systems.Clear();
            systemOrder.Clear();
            updates.Clear();
            lateUpdates.Clear();
            statuses.Clear();
        }
    }

    /// <summary>Gỡ callback scene và giải phóng hệ thống của màn đang chọn.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        spawner.SpawnLateUpdate -= LateTickSystems;
        try { ReleaseSystems(); }
        finally { Cleared = null; Failed = null; SpawnScheduled = null; WarningsCleared = null; }
    }
    #endregion
}
