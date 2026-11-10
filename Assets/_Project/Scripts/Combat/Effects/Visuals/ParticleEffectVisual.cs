using System;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleEffectVisual : MonoBehaviour, IEffectVisual
{
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

    private void LateUpdate()
    {
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

    private void OnDisable() => StopAndReset();
}
