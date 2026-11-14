using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

// Class thuần singleton trong SceneContext; điều phối một scene, không trực tiếp thực hiện pooling/save/UI.
public sealed class GameManager : IInitializable, IDisposable
{
    #region Dependencies And Flow State

    private readonly SaveManager saves; // Save đã load trước Initialize theo execution order của Zenject.
    private readonly PoolPrewarmService resources; // Chuẩn bị tài nguyên theo batch, chỉ chạy một lần.
    private readonly LevelManager levels; // Reset level, bonus, skill và các hệ thống nghe RunReset đúng một lần mỗi trận.
    private readonly PlayerManager players; // GameManager là nơi quyết định lúc spawn/remove Player.
    private readonly CharacterManager characters; // Nhận Player chết và thu thập character còn lại khi dọn trận.
    private readonly CharacterFactory factory; // Trả character về đúng pool, không Destroy trực tiếp.
    private readonly ProjectileManager projectiles; // Thu hồi đạn cũ trước khi tạo trận mới.
    private readonly GameplayPauseService pauses; // Token luồng game độc lập với pause thủ công và bảng chọn skill.
    private readonly SkillManager skills; // Dọn skill gameplay; skill nhân vật theo vòng đời CharacterView.
    private readonly StageManager stage;
    private readonly bool autoStartRun; // Tắt để chờ người chơi bấm Play tại Home.
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private IDisposable flowPause; // Giữ game dừng khi chuẩn bị, ở Home, thắng/thua hoặc gặp lỗi.
    private bool initialized; // Chặn Initialize lặp và API gameplay chạy trước bước chuẩn bị.
    private bool transitioning; // Chặn yêu cầu chuyển luồng lồng nhau từ callback UI hoặc gameplay.
    private bool disposed; // Không xử lý yêu cầu sau khi SceneContext đã kết thúc.
    private bool playerResultComplete;
    private bool enemiesResultComplete;
    private GameState? completedDuringTransition; // Giữ kết quả nếu hoàn tất ngay trong lúc bus đang phát state.
    public GameState State { get; private set; } = GameState.Boot; // Nguồn trạng thái duy nhất; bus/UI không tự ghi.
    public Exception LastError { get; private set; } // Cho loading/error UI đọc nguyên nhân chuẩn bị thất bại.

    #endregion

    #region Initialization

    // Zenject cung cấp service; constructor chưa spawn Player hoặc phát sự kiện khi UI chưa inject xong.
    public GameManager(SaveManager saves, PoolPrewarmService resources, LevelManager levels,
        PlayerManager players, CharacterManager characters, CharacterFactory factory,
        ProjectileManager projectiles, GameplayPauseService pauses, SkillManager skills, StageManager stage, bool autoStartRun = false)
    {
        this.saves = saves; this.resources = resources; this.levels = levels; this.players = players;
        this.characters = characters; this.factory = factory; this.projectiles = projectiles;
        this.pauses = pauses; this.autoStartRun = autoStartRun;
        this.skills = skills;
        this.stage = stage;
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
        characters.CharacterDied += OnCharacterDied;
        characters.EnemyCelebrationsCompleted += OnEnemyCelebrationsCompleted;
        stage.Cleared += OnStageCleared;
        stage.Failed += Fail;
        try
        {
            if (!saves.IsLoaded) throw new InvalidOperationException("Cannot initialize game before save data is loaded.");
            stage.Validate();
            await UniTask.NextFrame(cancellationToken: lifetime.Token);
            await resources.PrewarmAsync(lifetime.Token);
            if (!resources.IsReady) throw new InvalidOperationException("Gameplay resources are not ready.");
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
        if (!CanTransition() || !resources.IsReady ||
            (restart ? State != GameState.Playing :
                State != GameState.Home && State != GameState.Won && State != GameState.Lost)) return false;
        transitioning = true;
        HoldFlowPause();
        try
        {
            LastError = null;
            completedDuringTransition = null;
            ChangeState(GameState.PreparingRun);
            stage.StopRun();
            CleanupCharactersAndProjectiles();
            levels.StartNewRun();
            stage.BeginRun();
            CharacterView player = players.SpawnPlayer(); // PlayerChanged được phát trước khi UI/gameplay nhận Playing.
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
            stage.StopRun();
            CleanupCharactersAndProjectiles();
            levels.StartNewRun();
            ChangeState(GameState.Home);
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    #endregion

    #region Transition And Cleanup

    // Khóa gameplay, phát state kết thúc để listener dọn đạn và chạy animation kết quả.
    private bool FinishRun(GameState result)
    {
        if (!CanTransition() || State != GameState.Playing) return false;
        transitioning = true;
        try
        {
            CharacterView player = players.CurrentPlayer;
            if (result == GameState.Won && (player == null || player.RuntimeData == null || !player.RuntimeData.IsAlive))
                return false;
            HoldFlowPause();
            stage.FinishRun(result == GameState.Won);
            skills.ClearRunSkills();
            GameState ending = result == GameState.Won ? GameState.EndingWin : GameState.EndingLose;
            completedDuringTransition = null;
            playerResultComplete = false;
            enemiesResultComplete = result == GameState.Won;
            ChangeState(ending);
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
        if (!ReferenceEquals(character, players.CurrentPlayer) || State != GameState.Playing) return;
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
        GameUIEventBus.Publish(previous, next);
    }

    // Dọn bản sao danh sách vì Despawn thay đổi registry đồng bộ; không dùng Dispose để các service còn tái sử dụng.
    private void CleanupCharactersAndProjectiles()
    {
        projectiles.ClearProjectiles();
        players.RemovePlayer();
        foreach (CharacterView character in new List<CharacterView>(characters.Characters.Keys))
            if (character != null) factory.Despawn(character);
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
        stage.StopRun();
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
        characters.CharacterDied -= OnCharacterDied;
        characters.EnemyCelebrationsCompleted -= OnEnemyCelebrationsCompleted;
        stage.Cleared -= OnStageCleared;
        stage.Failed -= Fail;
        stage.StopRun();
        ReleaseFlowPause();
    }

    #endregion
}
