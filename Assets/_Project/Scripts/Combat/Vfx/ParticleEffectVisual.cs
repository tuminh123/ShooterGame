using UnityEngine;

/// <summary>VFX phát một ParticleSystem (kể cả các hệ con) và báo đã xong khi mọi hạt đã tắt.</summary>
/// <remarks>Không đổi tên class: prefab tham chiếu script qua GUID.</remarks>
[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleEffectVisual : MonoBehaviour, IEffectVisual
{
    #region Frame

    private int activationFrame = -1; // Bỏ qua frame vừa bật: vỏ pool tái sử dụng không đọc trạng thái cũ.

    private void OnEnable() => activationFrame = Time.frameCount;

    #endregion

    #region Playback & Completion

    private ParticleSystem particles;
    private ParticleSystem[] particleSystems;
    private bool playing;

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

    /// <summary>CombatVfx gọi ở LateTick (qua EffectView): trả về true khi mọi hạt đã tắt.</summary>
    public bool Advance(float deltaTime)
    {
        if (!playing) return true;
        // Frame vừa bật chưa mô phỏng hạt nào: IsAlive có thể còn false, chưa được coi là xong.
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return false;
        if (particles == null || particles.IsAlive(true)) return false;
        playing = false;
        return true;
    }

    public void StopAndReset()
    {
        playing = false;
        if (particles == null) particles = GetComponent<ParticleSystem>();
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDestroy() => StopAndReset();

    private void OnDisable() => StopAndReset();

    #endregion
}
