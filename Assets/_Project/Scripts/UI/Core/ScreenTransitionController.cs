using System;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using Zenject;

/// <summary>Che UI trước khi đổi lượt; giữ pause đến khi màn mới đã hiện hoàn toàn.</summary>
public sealed class ScreenTransitionController : MonoBehaviour, IScreenTransition
{
    #region Presentation Settings

    [Tooltip("Lớp phủ toàn màn hình trên Canvas riêng, có thứ tự hiển thị cao nhất.")]
    [SerializeField] private CanvasGroup overlay;
    [Tooltip("Số giây làm tối trước khi đổi màn; dùng thời gian thực khi game đang pause.")]
    [SerializeField, Min(0.01f)] private float coverDuration = 0.25f;
    [Tooltip("Số giây mở dần màn mới; gameplay chỉ chạy sau khi mở xong.")]
    [SerializeField, Min(0.01f)] private float revealDuration = 0.3f;

    #endregion

    #region Dependencies And Playback State
    private IGameState gameState;
    private IGameFlow runCommands;
    private IGamePause pauses;
    private IDisposable pauseLease;
    private Func<bool> pendingCommand;
    private Tween fade;
    private int version;
    /// <summary>Đang trong một lượt chuyển màn (đang giữ token pause).</summary>
    public bool IsTransitioning => pauseLease != null;

    /// <summary>SceneContext inject trạng thái, luồng game và pause, rồi đưa overlay về trạng thái ẩn.</summary>
    [Inject]
    public void Construct(IGameState gameState, IGameFlow gameFlow, IGamePause gamePause)
    {
        this.gameState = gameState;
        this.runCommands = gameFlow;
        this.pauses = gamePause;
        ResetPresentation();
    }

    #endregion

    #region Navigation Commands

    /// <summary>Che màn rồi bắt đầu lượt chơi; chỉ hợp lệ khi đang ở Home.</summary>
    public bool StartRun() => gameState != null && gameState.State == GameState.Home && Begin(runCommands.StartRun);
    /// <summary>Che màn rồi chơi lại lượt hiện tại; chỉ hợp lệ khi đang Playing.</summary>
    public bool RestartRun() => gameState != null && gameState.State == GameState.Playing && Begin(runCommands.RestartRun);
    /// <summary>Che màn rồi về Home; hợp lệ khi đang Playing hoặc đã thắng.</summary>
    public bool ReturnHome() => gameState != null &&
        (gameState.State == GameState.Playing || gameState.State == GameState.Won) && Begin(runCommands.ReturnHome);

    // Overlay chặn raycast ngay khi nhận lệnh; token riêng không làm mất pause của popup/Home.
    private bool Begin(Func<bool> command)
    {
        if (!isActiveAndEnabled || overlay == null || pauses == null || IsTransitioning) return false;
        pendingCommand = command;
        pauseLease = pauses.Acquire();
        version++;
        overlay.alpha = 0f;
        overlay.blocksRaycasts = true;
        overlay.interactable = true;
        transform.SetAsLastSibling();
        fade = Tween.Alpha(overlay, 0f, 1f, Mathf.Max(0.01f, coverDuration),
            Ease.InOutSine, useUnscaledTime: true)
            .OnComplete(this, controller => controller.OnCovered());
        return true;
    }

    #endregion

    #region Cover, Switch And Reveal

    // Chỉ thực thi lệnh khi đã kín màn hình; lệnh vẫn do GameManager kiểm tra và xử lý lỗi.
    private void OnCovered()
    {
        fade = default;
        int playbackVersion = version;
        Func<bool> command = pendingCommand;
        pendingCommand = null;
        try { command?.Invoke(); }
        catch (Exception exception) { Debug.LogException(exception); }
        if (IsTransitioning && isActiveAndEnabled && playbackVersion == version)
            RevealAfterFrameAsync(playbackVersion).Forget();
    }

    // Chờ một frame để UI, camera và Player đồng bộ dưới lớp phủ rồi mới mở màn.
    private async UniTask RevealAfterFrameAsync(int playbackVersion)
    {
        bool canceled = await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (canceled || !isActiveAndEnabled || !IsTransitioning || playbackVersion != version) return;
        fade = Tween.Alpha(overlay, 1f, 0f, Mathf.Max(0.01f, revealDuration),
            Ease.InOutSine, useUnscaledTime: true)
            .OnComplete(this, controller => controller.ResetPresentation());
    }

    // Hủy callback/tween trước khi trả token; không để lượt chuyển cũ chạy tiếp khi UI bật lại.
    private void ResetPresentation()
    {
        version++;
        fade.Stop();
        fade = default;
        pendingCommand = null;
        if (overlay != null)
        {
            overlay.alpha = 0f;
            overlay.blocksRaycasts = false;
            overlay.interactable = false;
        }
        IDisposable previous = pauseLease;
        pauseLease = null;
        previous?.Dispose();
    }

    private void OnDisable() => ResetPresentation();
    private void OnDestroy() => ResetPresentation();

    #endregion
}
