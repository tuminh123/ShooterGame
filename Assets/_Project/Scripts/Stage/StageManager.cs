using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Điều phối một màn chơi: chạy 3 spawn system theo đồng hồ Stage, quyết định thắng màn và cấp thưởng kết quả.
/// </summary>
/// <remarks>
/// Tự tạo bằng <c>new</c> ba system cố định (Wave, Formation, Boss + BossArena) và nối Wave → Boss trực tiếp.
/// Chọn system theo loại data bằng một switch duy nhất (<see cref="Validate(IStageSpawnData)"/>, <see cref="Execute"/>).
/// Ba system không thay thế được cho nhau (mỗi cái nhận một loại data riêng), nên không dùng interface chung.
/// </remarks>
public sealed class StageManager : DomainManager, ITickable, ILateTickable, IDisposable, IStageSelection, IStageRunLifecycle, IStageResults, IStageSchedule
{
    #region Dependencies, Systems & State

    private StageDataSO data;
    private readonly Dictionary<string, StageDataSO> stages;
    private readonly EnemySpawner spawner; // Tạo/thu hồi quái và vùng sinh, dùng chung cho cả ba system.
    private readonly IPlayerReader players;
    private readonly IWorldMap ground;
    private readonly StageRewards rewards; // Tính và cấp thưởng kết quả đúng một lần mỗi lượt.
    // Ba system cố định của mọi màn; màn không có data cho system nào thì system đó đứng yên.
    // Thứ tự Tick/Stop: Wave → Formation → Boss, để Boss phản ứng với event Wave ngay trong cùng frame.
    private readonly WaveSpawnSystem waves;
    private readonly FormationSpawnSystem formations;
    private readonly BossSpawnSystem boss;
    private bool running, started, clearRequested, disposed;
    private float stageTime;

    public StageDataSO CurrentStage => data;
    public StageDatabaseSO Database { get; }
    public bool ResultGranted => rewards.Granted;
    /// <summary>Thời gian gameplay từ đầu lượt; dừng tăng khi pause hoặc kết thúc.</summary>
    public float StageTime => stageTime;
    public event Action Cleared;
    public event Action<Exception> Failed;
    public event Action<StageSpawnWarning, float> SpawnScheduled;
    public event Action WarningsCleared;

    #endregion

    #region Construction

    /// <summary>Tạo ba spawn system và đấu trường Boss, nối event giữa chúng và đăng ký callback cuối frame.</summary>
    public StageManager(
        StageDatabaseSO database,
        EnemySpawner.Settings spawnSettings,
        ICharacterSpawn characterSpawn,
        ICharacterDespawn characterDespawn,
        LevelDatabaseSO levelDatabase,
        IWorldMap worldMap,
        IPlayerReader playerReader,
        ICharacterRegistry characterRegistry,
        IMovementArena movementArena,
        ISpatialInvalidation spatialInvalidation,
        IWallet wallet, // Hai vai trò Progression đều do ProgressionManager implement (ProgressionContracts.cs).
        IStageProgress stageProgress)
    {
        Database = database != null ? database : throw new ArgumentNullException(nameof(database));
        stages = database.CreateLookup();
        data = database.DefaultStage;
        this.ground = worldMap ?? throw new ArgumentNullException(nameof(worldMap));
        this.players = playerReader ?? throw new ArgumentNullException(nameof(playerReader));
        rewards = new StageRewards(wallet, stageProgress);
        spawner = new EnemySpawner(spawnSettings, characterSpawn, characterDespawn, playerReader, levelDatabase);

        waves = new WaveSpawnSystem(spawner);
        formations = new FormationSpawnSystem(spawner, playerReader, spatialInvalidation);
        boss = new BossSpawnSystem(spawner,
            new BossArena(characterRegistry, playerReader, spawner, movementArena));

        waves.SpawnScheduled += ForwardSpawnSchedule;
        boss.SpawnScheduled += ForwardSpawnSchedule;
        waves.AllWavesSpawned += boss.NotifyAllWavesSpawned;
        waves.FinalWaveCleared += boss.NotifyFinalWaveCleared;
    }

    /// <summary>Kiểm tra một asset spawn bằng system tương ứng (vùng sinh, cấu hình level của quái…).</summary>
    private void Validate(IStageSpawnData spawnData)
    {
        switch (spawnData)
        {
            case WaveConfigSO wave: waves.Validate(wave); break;
            case EnemyPatternSO pattern: formations.Validate(pattern); break;
            case BossSpawnDataSO special: boss.Validate(special); break;
            default: throw UnsupportedData(spawnData);
        }
    }

    /// <summary>Giao một asset spawn cho system tương ứng; mốc delay tính từ đầu Stage.</summary>
    private void Execute(IStageSpawnData spawnData)
    {
        switch (spawnData)
        {
            case WaveConfigSO wave: waves.Execute(wave); break;
            case EnemyPatternSO pattern: formations.Execute(pattern); break;
            case BossSpawnDataSO special: boss.Execute(special); break;
            default: throw UnsupportedData(spawnData);
        }
    }

    // Loại spawn mới phải được thêm vào cả hai switch ở trên cùng với system của nó.
    private static Exception UnsupportedData(IStageSpawnData spawnData)
        => new InvalidOperationException("Stage chưa hỗ trợ loại spawn data: " + (spawnData?.GetType().Name ?? "null"));

    #endregion

    #region Run Preparation

    // Công bố lịch Boss chờ sinh xong wave nếu mọi chuỗi đều có thời điểm xác định.
    private void PrepareSpawnSchedule()
    {
        float latest = 0f;
        bool found = false;
        foreach (var spawnData in data.SpawnData)
        {
            if (!(spawnData is WaveConfigSO config)) continue;
            found = true;
            float? time = WaveSpawnSystem.GetScheduledCompletion(config);
            if (!time.HasValue) { boss.ScheduledWaveCompletion = null; return; }
            latest = Mathf.Max(latest, time.Value);
        }
        boss.ScheduledWaveCompletion = found ? (float?)latest : null;
    }

    // Chỉ chuyển tiếp loại sự kiện và mốc giờ gameplay; không chứa cấu hình thời gian của UI.
    private void ForwardSpawnSchedule(StageSpawnWarning kind, float spawnAt) => SpawnScheduled?.Invoke(kind, spawnAt);

    /// <summary>Chọn màn bằng ID ổn định trước StartRun; dọn lượt và kết quả của màn cũ.</summary>
    public void SelectStage(string stageId)
    {
        EnsureNotDisposed();
        if (running)
            throw new InvalidOperationException("Dừng lượt hiện tại trước khi chọn Stage khác.");
        if (string.IsNullOrWhiteSpace(stageId) || !stages.TryGetValue(stageId, out var next))
            throw new ArgumentException("StageId không có trong Stage Database: " + stageId, nameof(stageId));
        next.Validate();
        StopRun(); // System dùng chung cho mọi màn: chỉ cần dọn lượt cũ, không phải tạo lại.
        data = next;
        stageTime = 0f;
        clearRequested = false;
        rewards.Reset();
    }

    /// <summary>Kiểm tra từng asset spawn và tính thử thưởng để phát hiện cấu hình lỗi.</summary>
    public void Validate()
    {
        data.Validate();
        foreach (var spawnData in data.SpawnData) Validate(spawnData);
        rewards.Validate(data);
    }

    /// <summary>Dọn lượt cũ, áp dụng map và đặt lại trạng thái cho lượt mới.</summary>
    public void BeginRun()
    {
        EnsureNotDisposed();
        StopRun();
        Validate();
        if (!ground.SetMap(data.MapKey))
            throw new InvalidOperationException("Không thể áp dụng map của Stage: " + data.MapKey);
        rewards.Reset();
        clearRequested = false;
        started = false;
        stageTime = 0f;
        running = true;
    }

    #endregion

    #region Update & Stage End

    /// <summary>Cập nhật Stage và chuyển ngoại lệ sang luồng xử lý lỗi.</summary>
    public override void Tick()
    {
        try { TickStage(); }
        catch (Exception exception) { FailStage(exception); }
    }

    // Player phải còn sống: một system có thể làm Player chết (hoặc kết thúc lượt) ngay trong lời gọi trước đó.
    private bool CanContinue => running && players.RuntimeData != null && players.RuntimeData.IsAlive;

    /// <summary>Chạy dữ liệu khi player sẵn sàng; thắng khi mọi system sinh xong và hết quái sống.</summary>
    private void TickStage()
    {
        if (disposed || Time.deltaTime <= 0f || !CanContinue)
            return;
        if (!started)
        {
            // Chỉ bắt đầu sau khi GameManager đã spawn Player, để Area lấy đúng tâm Player.
            started = true;
            PrepareSpawnSchedule();
            foreach (var spawnData in data.SpawnData)
            {
                Execute(spawnData);
                if (!CanContinue) return;
            }
        }
        float deltaTime = Time.deltaTime;
        stageTime += deltaTime;
        waves.Tick(stageTime, deltaTime);
        if (!CanContinue) return;
        formations.Tick(stageTime, deltaTime);
        if (!CanContinue) return;
        boss.Tick(stageTime, deltaTime);
        if (!CanContinue) return;

        bool allDeployed = waves.DeploymentComplete && formations.DeploymentComplete && boss.DeploymentComplete;
        bool noneAlive = waves.AliveCount == 0 && formations.AliveCount == 0 && boss.AliveCount == 0;
        if (!allDeployed || !noneAlive || clearRequested) return;
        clearRequested = true;
        StopRun();
        Cleared?.Invoke();
    }

    /// <summary>
    /// Bước cuối frame: bù chuyển động camera cho đội hình khi Stage đang chạy và gameplay không tạm dừng.
    /// Chạy ở LateTick 200, sau khi Simulation đã áp di chuyển (LateTick 0). Trước đây EnemySpawnManager
    /// tự đăng ký LateTick chỉ để phát event cho hàm này; giờ Zenject gọi thẳng.
    /// </summary>
    public override void LateTick()
    {
        if (disposed || !started || Time.deltaTime <= 0f || !CanContinue) return;
        try { formations.LateTick(); }
        catch (Exception exception) { FailStage(exception); }
    }

    /// <summary>Dừng mọi system, gộp lỗi cleanup và thông báo thất bại.</summary>
    private void FailStage(Exception exception)
    {
        try { StopRun(); }
        catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
        if (Failed != null) Failed(exception);
        else Debug.LogException(exception);
    }

    #endregion

    #region Run Result

    /// <summary>Dừng clock và spawn (giữ nhân vật đang sống để phát animation kết quả), rồi cấp thưởng đúng một lần.</summary>
    public void FinishRun(bool won)
    {
        EnsureNotDisposed();
        running = false;
        started = false;
        rewards.Grant(data, won);
    }

    /// <summary>Thưởng kết quả đã cấp của một loại tiền; 0 nếu không có.</summary>
    public long GetResultReward(CurrencyType type) => rewards.Get(type);

    #endregion

    #region Stop & Dispose

    /// <summary>Hủy lịch sinh và thu hồi quái của cả ba system; một system lỗi không chặn các system còn lại.</summary>
    public void StopRun()
    {
        running = false;
        started = false;
        WarningsCleared?.Invoke();
        List<Exception> failures = null;
        Stop(waves.Stop, ref failures);
        Stop(formations.Stop, ref failures);
        Stop(boss.Stop, ref failures);
        if (failures != null) throw new AggregateException("Stage spawn cleanup failed.", failures);
    }

    // Gom lỗi để vẫn dọn được các system sau.
    private static void Stop(Action stop, ref List<Exception> failures)
    {
        try { stop(); }
        catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
    }

    /// <summary>Chặn thao tác khi StageManager đã được giải phóng.</summary>
    private void EnsureNotDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(StageManager));
    }

    /// <summary>Gỡ event giữa các system và callback scene, rồi thu hồi quái của lượt đang chạy.</summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        waves.SpawnScheduled -= ForwardSpawnSchedule;
        boss.SpawnScheduled -= ForwardSpawnSchedule;
        waves.AllWavesSpawned -= boss.NotifyAllWavesSpawned;
        waves.FinalWaveCleared -= boss.NotifyFinalWaveCleared;
        try { StopRun(); }
        finally
        {
            Cleared = null;
            Failed = null;
            SpawnScheduled = null;
            WarningsCleared = null;
        }
    }

    #endregion
}
