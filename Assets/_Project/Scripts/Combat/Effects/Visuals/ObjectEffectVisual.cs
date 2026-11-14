using System;
using UnityEngine;
using Zenject;

// Hiển thị object/sprite tĩnh trong một khoảng thời gian, không cần Animator hoặc ParticleSystem.
public sealed class ObjectEffectVisual : MonoBehaviour, IEffectVisual, ITickable
{
    #region Zenject Tick Registration

    private LazyInject<TickableManager> tickables;
    private bool registeredForTicks;
    private int activationFrame = -1;

    [Inject]
    public void ConstructTickables(LazyInject<TickableManager> tickables) => this.tickables = tickables;

    private void Start()
    {
        if (registeredForTicks || tickables == null) return;
        tickables.Value.Add(this, -200);
        registeredForTicks = true;
    }

    #endregion

    #region Playback & Duration

    [SerializeField, Min(0.01f)] private float duration = 0.2f;
    private float remaining;
    private bool playing;
    public event Action Completed;

    public void Play()
    {
        StopAndReset();
        if (!(duration > 0f) || float.IsInfinity(duration))
            throw new InvalidOperationException("Object effect duration must be positive and finite.");
        remaining = duration;
        playing = true;
        activationFrame = Time.frameCount;
        gameObject.SetActive(true);
    }

    public void Tick()
    {
        if (!playing || !isActiveAndEnabled || Time.frameCount <= activationFrame || Time.deltaTime <= 0f) return;
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        if (remaining > 0f) return;
        StopAndReset();
        Completed?.Invoke();
    }

    // Tắt/thu hồi không phát Completed; không còn callback thời gian trỏ sang lượt pool mới.
    public void StopAndReset()
    {
        playing = false;
        remaining = 0f;
        gameObject.SetActive(false);
    }

    private void OnDisable() => StopAndReset();
    private void OnDestroy()
    {
        if (registeredForTicks) { tickables.Value.Remove(this); registeredForTicks = false; }
        StopAndReset();
    }

    #endregion
}
