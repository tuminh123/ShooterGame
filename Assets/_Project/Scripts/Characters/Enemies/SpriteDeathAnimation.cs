using System;
using UnityEngine;

// Phát sprite chết một lần trên visual; giữ nguyên vị trí, góc xoay và scale của nhân vật.
// Dùng timer gameplay chung nên animation chậm/dừng cùng slow motion/pause của trận.
public sealed class SpriteDeathAnimation : MonoBehaviour, ITimerListener
{
    #region Frame Configuration

    [SerializeField] private SpriteRenderer target;
    [SerializeField] private Sprite[] frames = Array.Empty<Sprite>(); // Đúng thứ tự từ phản ứng trúng đòn đến xác nằm yên.
    [SerializeField, Min(1f)] private float frameRate = 12f;

    #endregion

    #region Playback State

    private ITimerManager timers;
    private TimerHandle timer;
    private Sprite originalSprite;
    private int frameIndex;
    private bool playing;
    private bool hasOriginal;
    private Action completed;

    #endregion

    #region One Shot Playback

    // EnemyCombatFeedback đã tắt Animator đi bộ trước khi gọi, tránh hai nơi cùng đổi target.sprite.
    public bool Play(ITimerManager timerManager, Action onComplete)
    {
        StopAndReset();
        if (target == null || frames == null || frames.Length == 0 || timerManager == null) return false;
        if (!(frameRate > 0f) || float.IsInfinity(frameRate))
            throw new InvalidOperationException("Death animation frame rate must be positive and finite.");
        foreach (Sprite frame in frames)
            if (frame == null) return false;

        timers = timerManager;
        originalSprite = target.sprite;
        hasOriginal = true;
        completed = onComplete;
        frameIndex = 0;
        playing = true;
        target.sprite = frames[0];
        timer = timers.Register(frames.Length / frameRate, this, 1f / frameRate);
        return true;
    }

    // Frame dài có thể đi qua nhiều nhịp; nhảy đúng số frame mà không kéo dài animation.
    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        if (!playing || !timer.Equals(handle)) return;
        frameIndex = Mathf.Min(frames.Length - 1, frameIndex + result.DueTickCount);
        target.sprite = frames[frameIndex];
        if (!result.Completed) return;
        playing = false;
        timers.Cancel(timer);
        timer = default;
        Action callback = completed;
        completed = null;
        callback?.Invoke(); // UI/combat thu hồi visual sau khi frame cuối đã được giữ đủ một nhịp.
    }

    #endregion

    #region Pause And Pool Cleanup

    public void SetPaused(bool paused)
    {
        if (playing) timers.SetPaused(timer, paused);
    }

    // Không gọi callback khi bị thu hồi/reset; trả sprite đi bộ cũ trước khi visual quay lại pool.
    public void StopAndReset()
    {
        completed = null;
        playing = false;
        timers?.Cancel(timer);
        timer = default;
        timers = null;
        if (hasOriginal && target != null) target.sprite = originalSprite;
        originalSprite = null;
        hasOriginal = false;
        frameIndex = 0;
    }

    // Tắt tạm hierarchy chỉ pause. Pool luôn gọi feedback.Dispose/StopAndReset trước khi tắt visual.
    private void OnDisable() => SetPaused(true);
    private void OnEnable() => SetPaused(false);
    private void OnDestroy() => StopAndReset();

    #endregion
}
