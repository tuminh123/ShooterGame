using System;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleEffectVisual : MonoBehaviour, IEffectVisual, Zenject.ILateTickable
{
    #region Zenject Tick Registration

    private Zenject.LazyInject<Zenject.TickableManager> tickables;
    private bool registeredForTicks;
    private int activationFrame = -1;

    [Zenject.Inject]
    public void ConstructTickables(Zenject.LazyInject<Zenject.TickableManager> tickables) => this.tickables = tickables;

    // Đăng ký một lần ở Start để tránh vòng phụ thuộc trong lúc Zenject dựng container.
    // Object trong pool vẫn giữ đăng ký; Tick bỏ qua khi tắt hoặc vừa được tái sử dụng.
    private void StartFrameUpdates()
    {
        if (registeredForTicks || tickables == null) return;
        tickables.Value.AddLate(this, 500);
        registeredForTicks = true;
    }

    private void StopFrameUpdates()
    {
        if (!registeredForTicks) return;
        tickables.Value.RemoveLate(this);
        registeredForTicks = false;
    }

    private void Start() => StartFrameUpdates();

    private void OnEnable() => activationFrame = Time.frameCount;

    #endregion

    #region Playback & Completion
    private ParticleSystem particles;
    private ParticleSystem[] particleSystems;
    private bool playing;
    public event Action Completed;

    public void Play()
    {
        if (particles == null) particles = GetComponent<ParticleSystem>();
        if (particleSystems == null) particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        StopAndReset();
        foreach (ParticleSystem particle in particleSystems)
        {
            ParticleSystem.MainModule settings = particle.main;
            settings.playOnAwake = false;
            settings.stopAction = ParticleSystemStopAction.None;
            settings.loop = false; // Impact phát một lần, không giữ vỏ trong pool mãi.
            settings.useUnscaledTime = false; // Cùng pause/slow motion với gameplay.
        }
        particles.Play(true);
        playing = true;
    }

    public void LateTick()
    {
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return;
        if (!playing || particles == null || particles.IsAlive(true)) return;
        playing = false;
        Completed?.Invoke();
    }

    public void StopAndReset()
    {
        playing = false;
        if (particles == null) particles = GetComponent<ParticleSystem>();
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDestroy() { StopFrameUpdates(); StopAndReset(); }

    private void OnDisable()
    {
        StopAndReset();
    }
    #endregion
}
