using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Game: điều phối luồng của scene — loading → Home → vào trận → thắng/thua → Home.
/// Đây là class <b>duy nhất</b> của Game được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// GameManager
/// ├── GameStateMachine   state hiện tại + StateChanged cho UI
/// ├── GamePause          token pause/slow motion nhiều nguồn
/// └── GamePrewarm        bước Loading: tạo sẵn pool và buffer của mọi miền
/// </code>
/// Game nằm trên mọi miền gameplay và gọi xuống trực tiếp qua contract của chúng; chỉ UI nghe Game (qua event).
/// Miền khác chỉ thấy <c>GameContracts.cs</c>.
/// </remarks>
public sealed class GameManager : DomainManager, IGameState, IGameFlow, IGamePause, ILoadingProgress, IInitializable, IDisposable
{
    #region Settings

    /// <summary>Cấu hình luồng game, installer gán từ Inspector.</summary>
    public sealed class Settings
    {
        /// <summary>Vỏ nhân vật dùng chung; prewarm tạo sẵn theo số lượng trong PoolPrewarmSettings.</summary>
        public readonly CharacterView CharacterPrefab;
        /// <summary>true: vào trận ngay khi prewarm xong (test nhanh). false: chờ người chơi bấm Play tại Home.</summary>
        public readonly bool AutoStartRun;

        public Settings(CharacterView characterPrefab, bool autoStartRun)
        {
            CharacterPrefab = characterPrefab != null ? characterPrefab : throw new ArgumentNullException(nameof(characterPrefab));
            AutoStartRun = autoStartRun;
        }
    }

    #endregion

    #region Fields

    private readonly GameStateMachine states;
    private readonly GamePause pause;
    private readonly GamePrewarm prewarm;
    private readonly ISaveStore saves; // Save đã load trước Initialize theo execution order.
    private readonly IRunLifecycle run; // Reset level, loot, thống kê và skill gameplay của lượt.
    private readonly IPlayerReader playerReader;
    private readonly IPlayerLifecycle playerLifecycle; // GameManager là nơi quyết định lúc spawn/thu hồi Player.
    private readonly IPoolCleanup poolCleanup; // Lưới an toàn khi rời gameplay: thu hồi object mà miền sở hữu quên dọn.
    private readonly ICharacterDeathEvents characterDeaths;
    private readonly ICharacterDespawn characterDespawn; // Thu hồi mọi nhân vật khi rời gameplay, không Destroy trực tiếp.
    private readonly ICombatLifecycle combat; // Dọn đạn/effect/VFX và skill gameplay.
    private readonly IStageRunLifecycle stageLifecycle;
    private readonly IStageResults stageResults;
    private readonly Settings settings;
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private IDisposable flowPause; // Giữ game dừng khi chuẩn bị, ở Home, thắng/thua hoặc gặp lỗi.
    private bool initialized; // Chặn Initialize lặp và lệnh chạy trước bước chuẩn bị.
    private bool transitioning; // Chặn yêu cầu chuyển luồng lồng nhau từ callback UI hoặc gameplay.
    private bool disposed;
    // Kết thúc trận chờ HAI animation xong rồi mới hiện popup: Player (Win/Lose) và quái (ăn mừng khi thua).
    private bool playerResultComplete;
    private bool enemiesResultComplete;
    // Nếu cả hai báo xong NGAY trong lúc FinishRun còn đang chạy (không có animation, hết quái), không được đổi
    // state lồng vào giữa: lưu Won/Lost tạm ở đây, FinishRun áp dụng khi chạy xong. null = chưa có kết quả chờ.
    private GameState? pendingResultState;

    #endregion

    #region Properties / Events

    /// <inheritdoc/>
    public GameState State => states.State;

    /// <inheritdoc/>
    public Exception LastError { get; private set; }

    // Event chuyển tiếp: đăng ký vào event của Manager thực chất là đăng ký thẳng vào system con
    // (add/remove), Manager không giữ danh sách listener riêng.
    /// <inheritdoc/>
    public event Action<GameState, GameState> StateChanged
    {
        add => states.StateChanged += value;
        remove => states.StateChanged -= value;
    }

    /// <inheritdoc/>
    public bool IsReady => prewarm.IsReady;

    /// <inheritdoc/>
    public float Progress => prewarm.Progress;

    /// <inheritdoc/>
    public event Action<float> ProgressChanged
    {
        add => prewarm.ProgressChanged += value;
        remove => prewarm.ProgressChanged -= value;
    }

    #endregion

    #region Constructor / Lifecycle

    /// <summary>Dựng các system của Game; chưa spawn Player hay phát event (UI chưa inject xong).</summary>
    public GameManager(
        Settings settings,
        ISaveStore saves,
        IRunLifecycle runLifecycle,
        IPlayerReader playerReader, // Bốn vai trò Character đều do CharacterManager implement (CharacterContracts.cs).
        IPlayerLifecycle playerLifecycle,
        ICharacterDeathEvents characterDeaths,
        ICharacterDespawn characterDespawn,
        ICombatLifecycle combatLifecycle,
        IStageRunLifecycle stageLifecycle, // Hai vai trò Stage đều do StageManager implement (StageContracts.cs).
        IStageResults stageResults,
        IPoolCleanup poolCleanup,
        // Dữ liệu và dung lượng cho bước prewarm.
        PoolPrewarmSettingsSO prewarmSettings,
        CharacterDatabaseSO characterDatabase,
        CombatAssetsSO combatAssets,
        LootDatabaseSO lootDatabase,
        IPoolPreparation poolPreparation,
        ICharacterCapacity characterCapacity,
        ICombatCapacity combatCapacity,
        ISimulationCapacity simulationCapacity)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.saves = saves;
        run = runLifecycle;
        this.playerReader = playerReader;
        this.playerLifecycle = playerLifecycle;
        this.characterDeaths = characterDeaths;
        this.characterDespawn = characterDespawn;
        combat = combatLifecycle;
        this.stageLifecycle = stageLifecycle;
        this.stageResults = stageResults;
        this.poolCleanup = poolCleanup;

        states = new GameStateMachine();
        pause = new GamePause();
        prewarm = new GamePrewarm(prewarmSettings, settings.CharacterPrefab, characterDatabase, combatAssets, lootDatabase,
            poolPreparation, characterCapacity, combatCapacity, simulationCapacity);
    }

    /// <summary>Chạy sau Save và Progression; chỉ cho vào Playing sau khi pool sẵn sàng và spawn thành công.</summary>
    public override void Initialize()
    {
        if (initialized || disposed) return;
        initialized = true;
        InitializeAsync().Forget();
    }

    /// <summary>Gỡ listener và token trước khi các miền khác Dispose; GamePause trả timeScale về lúc đầu.</summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel(); // Hủy prewarm trước khi các pool bắt đầu Dispose.
        lifetime.Dispose();
        characterDeaths.CharacterDied -= OnCharacterDied;
        characterDeaths.EnemyCelebrationsCompleted -= OnEnemyCelebrationsCompleted;
        stageResults.Cleared -= OnStageCleared;
        stageResults.Failed -= Fail;
        stageLifecycle.StopRun();
        ReleaseFlowPause();
        states.ClearListeners(); // UI bị hủy cùng scene; không giữ reference tới listener đã chết.
        pause.Dispose();
    }

    // Nhường một frame trước prewarm để Loading hiện; hủy an toàn khi rời scene/thoát ứng dụng.
    private async UniTask InitializeAsync()
    {
        transitioning = true;
        HoldFlowPause();
        characterDeaths.CharacterDied += OnCharacterDied;
        characterDeaths.EnemyCelebrationsCompleted += OnEnemyCelebrationsCompleted;
        stageResults.Cleared += OnStageCleared;
        stageResults.Failed += Fail;
        try
        {
            if (!saves.IsLoaded) throw new InvalidOperationException("Cannot initialize game before save data is loaded.");
            stageLifecycle.Validate();
            await UniTask.NextFrame(cancellationToken: lifetime.Token);
            await prewarm.PrewarmAsync(lifetime.Token);
            if (!prewarm.IsReady) throw new InvalidOperationException("Gameplay resources are not ready.");
            await UniTask.NextFrame(cancellationToken: lifetime.Token); // Cho Loading vẽ mốc 100%.
            states.Change(GameState.Home);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception) { Fail(exception); }
        finally { transitioning = false; }
        if (!disposed && settings.AutoStartRun && State == GameState.Home) StartRun();
    }

    #endregion

    #region IGameFlow

    /// <inheritdoc/>
    public bool StartRun() => BeginRun(false);

    /// <inheritdoc/>
    public bool RestartRun() => BeginRun(true);

    /// <inheritdoc/>
    public bool ReturnHome()
    {
        if (!CanTransition() || State == GameState.Home) return false;
        transitioning = true;
        HoldFlowPause();
        try
        {
            ClearGameplayWorld();
            states.Change(GameState.Home);
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    // Không cho double click khởi tạo lại một trận đang chạy; chơi lại chỉ từ Playing (menu pause).
    private bool BeginRun(bool restart)
    {
        if (!CanTransition() || !prewarm.IsReady ||
            (restart ? State != GameState.Playing :
                State != GameState.Home && State != GameState.Won && State != GameState.Lost)) return false;
        transitioning = true;
        HoldFlowPause();
        try
        {
            LastError = null;
            pendingResultState = null;
            states.Change(GameState.PreparingRun);
            ClearGameplayWorld();
            stageLifecycle.BeginRun();
            CharacterView player = playerLifecycle.SpawnPlayer(); // PlayerChanged được phát trước khi UI/gameplay nhận Playing.
            if (player == null || player.RuntimeData == null || !player.RuntimeData.IsAlive)
                throw new InvalidOperationException("Player must be alive before entering Playing.");
            states.Change(GameState.Playing);
            ReleaseFlowPause();
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    #endregion

    #region IGamePause

    /// <inheritdoc/>
    public IDisposable Acquire() => pause.Acquire();

    /// <inheritdoc/>
    public IDisposable AcquireSlowMotion(float multiplier) => pause.AcquireSlowMotion(multiplier);

    #endregion

    #region Run Result

    // Stage báo sạch quái → thắng; Player hiện tại chết → thua. Cả hai đi qua cùng một đường để kết thúc đúng một lần.
    private void OnStageCleared() => FinishRun(GameState.Won);

    // CharacterManager phát cả chết Enemy; chỉ cái chết của Player hiện tại mới kết thúc trận.
    private void OnCharacterDied(CharacterView character)
    {
        if (!ReferenceEquals(character, playerReader.CurrentPlayer) || State != GameState.Playing) return;
        FinishRun(GameState.Lost);
    }

    // Khóa gameplay, chuyển sang state kết thúc, dọn combat và chạy animation kết quả.
    private bool FinishRun(GameState result)
    {
        if (!CanTransition() || State != GameState.Playing) return false;
        transitioning = true;
        try
        {
            CharacterView player = playerReader.CurrentPlayer;
            // Thắng chỉ hợp lệ khi Player còn sống (Stage có thể báo clear đúng frame Player chết).
            if (result == GameState.Won && (player == null || player.RuntimeData == null || !player.RuntimeData.IsAlive))
                return false;

            GameState ending = EnterEnding(result);
            StartResultAnimations(result, ending, player);
            ApplyPendingResult(ending);
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    // Dừng trận, cấp thưởng đúng một lần và chuyển sang EndingWin/EndingLose. Trả state kết thúc.
    private GameState EnterEnding(GameState result)
    {
        HoldFlowPause();
        stageResults.FinishRun(result == GameState.Won);
        combat.ClearRunSkills();

        GameState ending = result == GameState.Won ? GameState.EndingWin : GameState.EndingLose;
        pendingResultState = null;
        playerResultComplete = false;
        enemiesResultComplete = result == GameState.Won; // Thắng thì không có quái ăn mừng để chờ.
        states.Change(ending);
        return ending;
    }

    // Dọn đạn/effect rồi phát animation: Player (Win/Lose), và quái ăn mừng khi thua.
    private void StartResultAnimations(GameState result, GameState ending, CharacterView player)
    {
        // Thắng hay thua đều không được để đạn, effect và VFX chạy tiếp phía sau popup kết quả.
        // Phải chạy SAU khi đổi state: nếu không còn quái nào để ăn mừng, CharacterManager báo hoàn tất
        // ngay lập tức, và OnEnemyCelebrationsCompleted chỉ chấp nhận khi State đã là EndingLose.
        combat.ClearCombat();
        if (ending == GameState.EndingLose) characterDeaths.PlayEnemyCelebrations();

        CharacterVisualState animation = result == GameState.Won ? CharacterVisualState.Win : CharacterVisualState.Lose;
        if (player == null || !player.PlayResultAnimation(animation, () => OnPlayerResultAnimationCompleted(ending)))
            playerResultComplete = true; // Không có animation để chờ.
        TryCompleteResultPresentation();
    }

    // Hai animation đã xong ngay trong FinishRun (xem pendingResultState): giờ mới đổi sang Won/Lost.
    private void ApplyPendingResult(GameState ending)
    {
        if (!pendingResultState.HasValue) return;
        GameState completed = pendingResultState.Value;
        pendingResultState = null;
        if (State == ending) states.Change(completed);
    }

    // Chờ cả Player và nhóm enemy/boss trước khi công bố state cuối.
    private void OnPlayerResultAnimationCompleted(GameState ending)
    {
        if (disposed || State != ending) return;
        playerResultComplete = true;
        TryCompleteResultPresentation();
    }

    private void OnEnemyCelebrationsCompleted()
    {
        if (disposed || State != GameState.EndingLose) return;
        enemiesResultComplete = true;
        TryCompleteResultPresentation();
    }

    private void TryCompleteResultPresentation()
    {
        if (!playerResultComplete || !enemiesResultComplete) return;
        GameState result = State == GameState.EndingWin ? GameState.Won : GameState.Lost;
        if (transitioning) pendingResultState = result;
        else CompleteResult(result);
    }

    private void CompleteResult(GameState result)
    {
        transitioning = true;
        try { states.Change(result); }
        catch (Exception exception) { Fail(exception); }
        finally { transitioning = false; }
    }

    #endregion

    #region Private Helpers

    // Một nơi kiểm tra vòng đời/khóa chuyển state cho tất cả lệnh.
    private bool CanTransition() => initialized && !disposed && !transitioning;

    /// <summary>
    /// Rời gameplay (về Home, hoặc trước khi vào trận mới): dọn sạch mọi object của trận. Một chỗ duy nhất cho cả hai đường.
    /// Không dùng Dispose, vì các miền còn được dùng lại cho trận sau.
    /// </summary>
    /// <remarks>
    /// Thứ tự có lý do:
    /// 1. Stage dừng trước, để không spawn thêm quái trong lúc dọn.
    /// 2. Combat dọn đạn/effect/VFX trước nhân vật, để đạn đang bay không trúng nhân vật đang bị thu hồi.
    /// 3. Gỡ Player qua PlayerSystem (đồng bộ CurrentPlayer/PlayerChanged), rồi thu hồi MỌI nhân vật còn lại.
    /// 4. ResetRun của miền Run: dọn skill gameplay, loot, level và thống kê của trận, rồi phát RunReset.
    /// 5. Lưới an toàn ở tầng pool: thu hồi thứ gì còn sót và log warning kèm tên, để tìm miền nào quên dọn.
    /// </remarks>
    private void ClearGameplayWorld()
    {
        stageLifecycle.StopRun();
        combat.ClearCombat();
        playerLifecycle.RemovePlayer();
        characterDespawn.DespawnAll();
        run.ResetRun();
        poolCleanup.DespawnGameplayClones();
    }

    // Token chỉ được lấy một lần; các nguồn pause khác vẫn giữ token riêng của chúng.
    private void HoldFlowPause()
    {
        if (flowPause == null) flowPause = pause.Acquire();
    }

    // Chỉ trả token luồng; không ép timeScale = 1 và không bỏ pause của bảng skill/người chơi.
    private void ReleaseFlowPause()
    {
        IDisposable previous = flowPause;
        flowPause = null;
        previous?.Dispose();
    }

    // Lỗi chuẩn bị không được vào Playing: giữ pause, công bố lỗi và log nguyên nhân để sửa cấu hình.
    private void Fail(Exception exception)
    {
        LastError = exception;
        stageLifecycle.StopRun();
        HoldFlowPause();
        states.Change(GameState.Faulted);
        Debug.LogException(exception);
    }

    #endregion
}
