using UnityEngine;

// Particle nằm trên visual con; EffectView quyết định thời điểm bắt đầu và kết thúc.
[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleEffectVisual : MonoBehaviour, IEffectVisual
{
    #region Particle State

    private ParticleSystem particles; // Particle gốc; withChildren xử lý cả nhánh con.
    private ParticleSystem[] particleSystems; // Cache cả nhánh để vô hiệu auto-destroy trên từng particle con.

    #endregion

    #region Playback

    // Cache nhánh particle ở lần đầu, vô hiệu cơ chế tự hủy rồi phát lại toàn bộ từ trạng thái sạch.
    public void Play()
    {
        if (particles == null) particles = GetComponent<ParticleSystem>();
        if (particleSystems == null) particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particleSystems)
        {
            ParticleSystem.MainModule settings = particle.main;
            settings.playOnAwake = false; // Chỉ phát khi EffectView gọi Play.
            settings.stopAction = ParticleSystemStopAction.None; // Không tự Destroy/Disable object thuộc pool.
        }
        StopAndReset();
        particles.Play(true);
    }

    // Clear cả hạt world-space để lần spawn sau không còn hạt ở vị trí cũ.
    public void StopAndReset()
    {
        if (particles == null) particles = GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion

    #region Disable Cleanup

    private void OnDisable() => StopAndReset(); // Vẫn dọn khi visual bị tắt trực tiếp.

    #endregion
}
