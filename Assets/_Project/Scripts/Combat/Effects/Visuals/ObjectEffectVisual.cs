using System;
using UnityEngine;
using Zenject;

// Hiển thị object/sprite tĩnh trong một khoảng thời gian, không cần Animator hoặc ParticleSystem.
public sealed class ObjectEffectVisual : MonoBehaviour, IEffectVisual, ITimerListener
{
    [SerializeField, Min(0.01f)] private float duration = 0.2f;
    private ITimerManager timers;
    private TimerHandle timer;
    public event Action Completed;

    [Inject]
    public void Construct(ITimerManager timers) => this.timers = timers;

    public void Play()
    {
        StopAndReset();
        if (!(duration > 0f) || float.IsInfinity(duration))
            throw new InvalidOperationException("Object effect duration must be positive and finite.");
        if (timers == null)
            throw new InvalidOperationException("Inject ITimerManager before playing ObjectEffectVisual.");
        timer = timers.Register(duration, this);
        gameObject.SetActive(true);
    }

    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        if (!result.Completed || !timer.Equals(handle) || !timers.IsRegistered(handle)) return;
        StopAndReset();
        Completed?.Invoke();
    }

    // Tắt/thu hồi không phát Completed; hủy handle để callback cũ không kết thúc lượt phát mới.
    public void StopAndReset()
    {
        timers?.Cancel(timer);
        timer = default;
        gameObject.SetActive(false);
    }

    private void OnDisable() => StopAndReset();
    private void OnDestroy() => StopAndReset();
}
