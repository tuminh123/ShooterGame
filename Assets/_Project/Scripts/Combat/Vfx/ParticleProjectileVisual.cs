using UnityEngine;

/// <summary>
/// Visual particle phát lặp liên tục khi object đang bật: đạn bay, VFX trạng thái bám trên quái, vùng skill.
/// Đặt trên gốc prefab visual; chỉ lo phần phát hình, không biết gì về gameplay.
/// </summary>
/// <remarks>
/// Prefab dùng với pool nên mỗi lần bật phải bắt đầu sạch: hạt và vệt world-space của lượt trước còn nằm ở vị trí cũ,
/// không xóa thì lần lấy ra pool sau sẽ kéo một vệt từ chỗ cũ tới chỗ mới.
/// Vỏ (ví dụ <see cref="ProjectileView"/>) thuê visual với vị trí/góc thế giới đúng ngay từ đầu, nên lúc <c>OnEnable</c>
/// chạy thì hạt phát đúng chỗ. Dùng scaled time (mặc định của ParticleSystem) nên dừng theo pause của gameplay.
/// Không đổi tên class: prefab tham chiếu script qua GUID.
/// </remarks>
public sealed class ParticleProjectileVisual : MonoBehaviour
{
    #region State

    private ParticleSystem[] particleSystems; // Mọi hệ hạt trong prefab, lấy một lần để không cấp phát mỗi lượt.
    private TrailRenderer[] trails; // Vệt không thuộc ParticleSystem, phải xóa riêng.

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    // Lượt mới: xóa sạch rồi phát lại từ đầu tại vị trí hiện tại.
    private void OnEnable()
    {
        ClearTrails();
        foreach (ParticleSystem particles in particleSystems)
        {
            particles.Clear(false);
            particles.Play(false);
        }
    }

    // Trả về pool: dừng và xóa để object nằm trong pool không còn hạt nào.
    private void OnDisable()
    {
        foreach (ParticleSystem particles in particleSystems)
            if (particles != null) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        ClearTrails();
    }

    #endregion

    #region Private Helpers

    private void ClearTrails()
    {
        foreach (TrailRenderer trail in trails)
            if (trail != null) trail.Clear();
    }

    #endregion
}
