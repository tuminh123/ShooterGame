using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Class thuần singleton trong SceneContext; điều phối một scene, không trực tiếp thực hiện pooling/save/UI.
public sealed class GameManager : IInitializable, IDisposable
{
    #region Dependencies And Flow State

    private readonly SaveManager saves; // Save đã load trước Initialize theo execution order của Zenject.
    private readonly PoolPrewarmService resources; // Chuẩn bị tài nguyên đồng bộ hiện có, chỉ chạy một lần.
    private readonly LevelManager levels; // Reset level, bonus, skill và các hệ thống nghe RunReset đúng một lần mỗi trận.
    private readonly PlayerManager players; // GameManager là nơi quyết định lúc spawn/remove Player.
    private readonly CharacterManager characters; // Nhận Player chết và thu thập character còn lại khi dọn trận.
    private readonly CharacterFactory factory; // Trả character về đúng pool, không Destroy trực tiếp.
    private readonly ProjectileManager projectiles; // Thu hồi đạn cũ trước khi tạo trận mới.
    private readonly GameplayPauseService pauses; // Token luồng game độc lập với pause thủ công và bảng chọn skill.
    private readonly SkillManager skills; // Dọn skill gameplay; skill nhân vật theo vòng đời CharacterView.
    private readonly bool autoStartRun; // Scene hiện chưa có Home UI nên mặc định tự vào trận sau khi sẵn sàng.
    private IDisposable flowPause; // Giữ game dừng khi chuẩn bị, ở Home, thắng/thua hoặc gặp lỗi.
    private bool initialized; // Chặn Initialize lặp và API gameplay chạy trước bước chuẩn bị.
    private bool transitioning; // Chặn yêu cầu chuyển luồng lồng nhau từ callback UI hoặc gameplay.
    private bool disposed; // Không xử lý yêu cầu sau khi SceneContext đã kết thúc.
    public GameState State { get; private set; } = GameState.Boot; // Nguồn trạng thái duy nhất; bus/UI không tự ghi.
    public Exception LastError { get; private set; } // Cho loading/error UI đọc nguyên nhân chuẩn bị thất bại.

    #endregion

    #region Initialization

    // Zenject cung cấp service; constructor chưa spawn Player hoặc phát sự kiện khi UI chưa inject xong.
    public GameManager(SaveManager saves, PoolPrewarmService resources, LevelManager levels,
        PlayerManager players, CharacterManager characters, CharacterFactory factory,
        ProjectileManager projectiles, GameplayPauseService pauses, SkillManager skills, bool autoStartRun = true)
    {
        this.saves = saves; this.resources = resources; this.levels = levels; this.players = players;
        this.characters = characters; this.factory = factory; this.projectiles = projectiles;
        this.pauses = pauses; this.autoStartRun = autoStartRun;
        this.skills = skills;
    }

    // Chạy sau Save/RunSession.Initialize; chỉ cho vào Playing sau khi pool sẵn sàng và spawn thành công.
    public void Initialize()
    {
        if (initialized || disposed) return;
        initialized = true;
        transitioning = true;
        HoldFlowPause();
        characters.CharacterDied += OnCharacterDied;
        try
        {
            if (!saves.IsLoaded) throw new InvalidOperationException("Cannot initialize game before save data is loaded.");
            resources.Prewarm();
            if (!resources.IsReady) throw new InvalidOperationException("Gameplay resources are not ready.");
            ChangeState(GameState.Home);
        }
        catch (Exception exception) { Fail(exception); }
        finally { transitioning = false; }
        if (autoStartRun && State == GameState.Home) StartRun();
    }

    #endregion

    #region Run Commands

    // Bắt đầu hoặc chơi lại từ Home/thắng/thua; không cho double click khởi tạo lại một trận đang chạy.
    public bool StartRun()
    {
        if (!CanTransition() || !resources.IsReady ||
            (State != GameState.Home && State != GameState.Won && State != GameState.Lost)) return false;
        transitioning = true;
        HoldFlowPause();
        try
        {
            LastError = null;
            ChangeState(GameState.PreparingRun);
            CleanupCharactersAndProjectiles();
            levels.StartNewRun();
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

    // Điều kiện thắng do hệ thống gameplay gọi; enum Won không được phát trực tiếp từ UI bus.
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

    // Dừng gameplay trước khi thông báo kết quả; giữ snapshot trận để UI kết quả đọc cho tới restart/về Home.
    private bool FinishRun(GameState result)
    {
        if (!CanTransition() || State != GameState.Playing) return false;
        transitioning = true;
        try { HoldFlowPause(); skills.ClearRunSkills(); ChangeState(result); return true; }
        catch (Exception exception) { Fail(exception); return false; }
        finally { transitioning = false; }
    }

    // CharacterManager có thể phát cả chết Enemy; chỉ cái chết của Player hiện tại mới kết thúc trận.
    private void OnCharacterDied(CharacterView character)
    {
        if (ReferenceEquals(character, players.CurrentPlayer)) LoseRun();
    }

    // Một nơi kiểm tra vòng đời/khóa chuyển state cho tất cả command.
    private bool CanTransition() => initialized && !disposed && !transitioning;

    // Công bố state sau khi đã ghi state thật; subscriber có thể đọc State trong callback nhưng không chuyển lồng nhau.
    private void ChangeState(GameState next)
    {
        if (State == next) return;
        GameState previous = State; // Chụp trạng thái cũ để UI biết bước vừa kết thúc.
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
        HoldFlowPause();
        ChangeState(GameState.Faulted);
        Debug.LogException(exception);
    }

    // Gỡ listener và token trước khi các service scene được dispose; không giữ singleton/static ngoài scene hiện tại.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        characters.CharacterDied -= OnCharacterDied;
        ReleaseFlowPause();
    }

    #endregion
}
