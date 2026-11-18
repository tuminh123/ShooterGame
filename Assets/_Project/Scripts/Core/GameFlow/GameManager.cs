using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

// Class thuần singleton trong SceneContext; điều phối một scene, không trực tiếp thực hiện pooling/save/UI.
public sealed class GameManager : IInitializable, IDisposable, IGameFlow, IGameStateReader, IRunCommands, IRunResultCommands
{
    #region Dependencies And Flow State
    private readonly ISaveReader saves; // Save đã load trước Initialize theo execution order của Zenject.
    private readonly IPoolPrewarm poolPrewarm; // Chuẩn bị tài nguyên theo batch, chỉ chạy một lần.
    private readonly IPoolPrewarmProgress prewarmProgress;
    private readonly IRunProgressCommands levels; // Reset level, bonus, skill và các hệ thống nghe RunReset đúng một lần mỗi trận.
    private readonly IPlayerReader playerReader; // GameManager là nơi quyết định lúc spawn/remove Player.
    private readonly IPlayerLifecycle playerLifecycle;
    private readonly IPoolCleanup poolCleanup; // Lưới an toàn khi rời gameplay: thu hồi object mà miền sở hữu quên dọn.
    private readonly ICharacterDeathEvents characterDeaths;
    private readonly ICharacterDespawn characterDespawn; // Thu hồi mọi nhân vật khi rời gameplay, không Destroy trực tiếp.
    private readonly ICombatLifecycle combat; // Dọn đạn/effect/VFX và skill gameplay; skill vốn có theo vòng đời CharacterView.
    private readonly IGameplayPause pauses; // Token luồng game độc lập với pause thủ công và bảng chọn skill.
    private readonly IStageRunLifecycle stageLifecycle;
    private readonly IStageResults stageResults;
    private readonly bool autoStartRun; // Tắt để chờ người chơi bấm Play tại Home.
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private IDisposable flowPause; // Giữ game dừng khi chuẩn bị, ở Home, thắng/thua hoặc gặp lỗi.
    private bool initialized; // Chặn Initialize lặp và API gameplay chạy trước bước chuẩn bị.
    private bool transitioning; // Chặn yêu cầu chuyển luồng lồng nhau từ callback UI hoặc gameplay.
    private bool disposed; // Không xử lý yêu cầu sau khi SceneContext đã kết thúc.
    private bool playerResultComplete;
    private bool enemiesResultComplete;
    private GameState? completedDuringTransition; // Giữ kết quả nếu hoàn tất ngay trong lúc bus đang phát state.
    public GameState State { get; private set; } = GameState.Boot; // Nguồn trạng thái duy nhất; UI chỉ đọc, không tự ghi.
    public Exception LastError { get; private set; } // Cho loading/error UI đọc nguyên nhân chuẩn bị thất bại.

    /// <summary>
    /// Phát (state trước, state sau) ngay sau khi <see cref="State"/> đã được ghi.
    /// Chỉ dành cho tầng trên Game (UI). Các hệ thống gameplay bên dưới không nghe event này;
    /// GameManager gọi thẳng xuống chúng (ví dụ <see cref="ICombatLifecycle.ClearCombat"/> khi kết thúc trận).
    /// </summary>
    public event Action<GameState, GameState> StateChanged;

    #endregion

    #region Initialization

    // Zenject cung cấp service; constructor chưa spawn Player hoặc phát sự kiện khi UI chưa inject xong.
    public GameManager(
        ISaveService saveService,
        IPoolPrewarmService poolPrewarmService,
        ILevelService levelService,
        IPlayerReader playerReader, // Năm vai trò Character đều do CharacterManager implement (CharacterContracts.cs).
        IPlayerLifecycle playerLifecycle,
        IPoolSystem poolSystem,
        ICharacterDeathEvents characterDeaths,
        ICharacterDespawn characterDespawn,
        ICombatLifecycle combatLifecycle,
        IGameplayPauseService gameplayPauseService,
        IStageRunLifecycle stageLifecycle, // Hai vai trò Stage đều do StageManager implement (StageContracts.cs).
        IStageResults stageResults,
        bool autoStartRun = false)
    {
        this.saves = saveService.GetSaveReader();
        this.poolPrewarm = poolPrewarmService.GetPoolPrewarm();
        this.prewarmProgress = poolPrewarmService.GetPoolPrewarmProgress();
        this.levels = levelService.GetRunProgressCommands();
        this.playerReader = playerReader;
        this.playerLifecycle = playerLifecycle;
        this.poolCleanup = poolSystem.GetPoolCleanup();
        this.characterDeaths = characterDeaths;
        this.characterDespawn = characterDespawn;
        this.combat = combatLifecycle;
        this.pauses = gameplayPauseService.GetGameplayPause();
        this.autoStartRun = autoStartRun;
        this.stageLifecycle = stageLifecycle;
        this.stageResults = stageResults;
    }

    // Chạy sau Save/RunSession.Initialize; chỉ cho vào Playing sau khi pool sẵn sàng và spawn thành công.
    public void Initialize()
    {
        if (initialized || disposed) return;
        initialized = true;
        InitializeAsync().Forget();
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
            await poolPrewarm.PrewarmAsync(lifetime.Token);
            if (!prewarmProgress.IsReady) throw new InvalidOperationException("Gameplay resources are not ready.");
            await UniTask.NextFrame(cancellationToken: lifetime.Token); // Cho Loading vẽ mốc 100%.
            ChangeState(GameState.Home);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception) { Fail(exception); }
        finally { transitioning = false; }
        if (!disposed && autoStartRun && State == GameState.Home) StartRun();
    }

    #endregion

    #region Run Commands

    // Bắt đầu hoặc chơi lại từ Home/thắng/thua; không cho double click khởi tạo lại một trận đang chạy.
    public bool StartRun()
        => BeginRun(false);

    // Pause gọi riêng lệnh chơi lại; StartRun vẫn chặn double click khi đang Playing.
    public bool RestartRun()
        => BeginRun(true);

    private bool BeginRun(bool restart)
    {
        if (!CanTransition() || !prewarmProgress.IsReady ||
            (restart ? State != GameState.Playing :
                State != GameState.Home && State != GameState.Won && State != GameState.Lost)) return false;
        transitioning = true;
        HoldFlowPause();
        try
        {
            LastError = null;
            completedDuringTransition = null;
            ChangeState(GameState.PreparingRun);
            ClearGameplayWorld();
            stageLifecycle.BeginRun();
            CharacterView player = playerLifecycle.SpawnPlayer(); // PlayerChanged được phát trước khi UI/gameplay nhận Playing.
            if (player == null || player.RuntimeData == null || !player.RuntimeData.IsAlive)
                throw new InvalidOperationException("Player must be alive before entering Playing.");
            ChangeState(GameState.Playing);
            ReleaseFlowPause();
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    // Điều kiện thắng do hệ thống gameplay gọi; UI không tự quyết định state Won.
    public bool WinRun() => FinishRun(GameState.Won);

    // Có thể gọi từ gameplay; Player chết cũng đi qua cùng API để kết thúc đúng một lần.
    public bool LoseRun() => FinishRun(GameState.Lost);

    // Rời trận về Home trong cùng scene; dọn world, reset lượt và giữ pause cho tới lần StartRun tiếp theo.
    public bool ReturnHome()
    {
        if (!CanTransition() || State == GameState.Home) return false;
        transitioning = true;
        HoldFlowPause();
        try
        {
            ClearGameplayWorld();
            ChangeState(GameState.Home);
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    #endregion

    #region Transition And Cleanup

    // Khóa gameplay, chuyển sang state kết thúc, dọn combat và chạy animation kết quả.
    private bool FinishRun(GameState result)
    {
        if (!CanTransition() || State != GameState.Playing) return false;
        transitioning = true;
        try
        {
            CharacterView player = playerReader.CurrentPlayer;
            if (result == GameState.Won && (player == null || player.RuntimeData == null || !player.RuntimeData.IsAlive))
                return false;
            HoldFlowPause();
            stageResults.FinishRun(result == GameState.Won);
            combat.ClearRunSkills();
            GameState ending = result == GameState.Won ? GameState.EndingWin : GameState.EndingLose;
            completedDuringTransition = null;
            playerResultComplete = false;
            enemiesResultComplete = result == GameState.Won;
            ChangeState(ending);

            // Thắng hay thua đều không được để đạn, effect và VFX chạy tiếp phía sau popup kết quả.
            // Phải chạy SAU ChangeState: nếu không còn quái nào để ăn mừng, CharacterManager báo hoàn tất
            // ngay lập tức, và OnEnemyCelebrationsCompleted chỉ chấp nhận khi State đã là EndingLose.
            combat.ClearCombat();
            if (ending == GameState.EndingLose) characterDeaths.PlayEnemyCelebrations();

            CharacterVisualState animation = result == GameState.Won ? CharacterVisualState.Win : CharacterVisualState.Lose;
            if (player == null || !player.PlayResultAnimation(animation, () => OnPlayerResultAnimationCompleted(ending)))
                playerResultComplete = true;
            TryCompleteResultPresentation();
            if (completedDuringTransition.HasValue)
            {
                GameState completed = completedDuringTransition.Value;
                completedDuringTransition = null;
                if (State == ending) ChangeState(completed);
            }
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    // CharacterManager có thể phát cả chết Enemy; chỉ cái chết của Player hiện tại mới kết thúc trận.
    private void OnCharacterDied(CharacterView character)
    {
        if (!ReferenceEquals(character, playerReader.CurrentPlayer) || State != GameState.Playing) return;
        LoseRun();
    }

    private void OnStageCleared() => WinRun();

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
        if (transitioning) completedDuringTransition = result;
        else CompleteResult(result);
    }

    private void CompleteResult(GameState result)
    {
        transitioning = true;
        try { ChangeState(result); }
        catch (Exception exception) { Fail(exception); }
        finally { transitioning = false; }
    }

    // Một nơi kiểm tra vòng đời/khóa chuyển state cho tất cả command.
    private bool CanTransition() => initialized && !disposed && !transitioning;

    // Công bố state sau khi đã ghi state thật; subscriber có thể đọc State trong callback nhưng không chuyển lồng nhau.
    private void ChangeState(GameState next)
    {
        if (State == next) return;
        GameState previous = State; // Chụp state cũ trước khi phát thông báo cho các listener.
        State = next;
        PublishStateChanged(previous, next);
    }

    // Lỗi của một listener không được chặn các listener còn lại, nếu không một panel UI lỗi
    // có thể khiến các panel khác kẹt ở state cũ.
    private void PublishStateChanged(GameState previous, GameState next)
    {
        Action<GameState, GameState> handlers = StateChanged; // Chụp danh sách để listener tự gỡ khi đang phát vẫn an toàn.
        if (handlers == null) return;
        foreach (Action<GameState, GameState> handler in handlers.GetInvocationList())
        {
            try { ((Action<GameState, GameState>)handler)(previous, next); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    /// <summary>
    /// Rời gameplay (về Home, hoặc trước khi vào trận mới): dọn sạch mọi object của trận. Một chỗ duy nhất cho cả hai đường.
    /// Không dùng Dispose, vì các service còn được dùng lại cho trận sau.
    /// </summary>
    /// <remarks>
    /// Thứ tự có lý do:
    /// 1. Stage dừng trước, để không spawn thêm quái trong lúc dọn.
    /// 2. Combat dọn đạn/effect/VFX trước nhân vật, để đạn đang bay không trúng nhân vật đang bị thu hồi.
    /// 3. Gỡ Player qua PlayerManager (đồng bộ CurrentPlayer/PlayerChanged), rồi thu hồi MỌI nhân vật còn lại.
    /// 4. StartNewRun phát RunReset: loot, level và skill của trận được reset.
    /// 5. Lưới an toàn ở tầng pool: thu hồi thứ gì còn sót và log warning kèm tên, để tìm miền nào quên dọn.
    /// </remarks>
    private void ClearGameplayWorld()
    {
        stageLifecycle.StopRun();
        combat.ClearCombat();
        playerLifecycle.RemovePlayer();
        characterDespawn.DespawnAll();
        levels.StartNewRun();
        poolCleanup.DespawnGameplayClones();
    }

    // Token chỉ được lấy một lần; các nguồn pause khác vẫn giữ token riêng của chúng.
    private void HoldFlowPause()
    {
        if (flowPause == null) flowPause = pauses.Acquire();
    }

    // Chỉ trả token luồng; không trực tiếp ép timeScale=1 và không bỏ pause của bảng skill/người chơi.
    private void ReleaseFlowPause()
    {
        IDisposable previous = flowPause; flowPause = null; previous?.Dispose();
    }

    // Chuẩn bị lỗi không được vào Playing; giữ pause, công bố lỗi và log nguyên nhân để sửa cấu hình.
    private void Fail(Exception exception)
    {
        LastError = exception;
        stageLifecycle.StopRun();
        HoldFlowPause();
        ChangeState(GameState.Faulted);
        Debug.LogException(exception);
    }

    // Gỡ listener và token trước khi các service scene được dispose; không giữ singleton/static ngoài scene hiện tại.
    public void Dispose()
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
        StateChanged = null; // UI bị hủy cùng scene; không giữ reference tới listener đã chết.
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IGameStateReader GetGameStateReader() => this;

    public IRunCommands GetRunCommands() => this;

    public IRunResultCommands GetRunResultCommands() => this;

    #endregion
}
