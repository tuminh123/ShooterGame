using System;
using UnityEngine;
using Zenject;

// Vỏ VFX: quản lý tuổi thọ và visual con; không biết visual dùng animation, particle hay sprite.
public sealed class EffectView : MonoBehaviour, ITimerListener
{
    #region Settings & Dependencies

    [SerializeField, Min(0.01f)] private float duration = 0.14f; // Số giây gameplay trước khi thu hồi cả vỏ và visual.
    private PoolManager pools; // Visual con và vỏ được tái sử dụng qua pool chung.
    private ITimerManager timers; // Dùng bộ đếm chung, không cần Update trên mỗi EffectView.
    private CombatSpawner spawner; // Đầu mối thu hồi visual trước khi thu hồi vỏ.
    private GameObject visualObject; // Clone visual được gắn dưới vỏ trong lượt spawn này.
    private IEffectVisual visual; // Chỉ gọi hợp đồng phát/dừng, không rẽ nhánh theo loại VFX.
    private TimerHandle lifetimeTimer; // Phân biệt callback hết hạn của từng lượt spawn.

    #endregion

    #region Dependency Injection

    // Pool inject dịch vụ trước khi CombatSpawner chuẩn bị visual và bật vỏ.
    [Inject]
    public void Construct(PoolManager pools, ITimerManager timers)
    { this.pools = pools; this.timers = timers; }

    #endregion

    #region Spawn & Playback

    // CombatSpawner gọi khi vỏ còn tắt; visual chỉ phát khi cả nhánh được bật.
    internal void Initialize(CombatSpawner spawner, GameObject visualPrefab)
    {
        Clear();
        if (!(duration > 0f) || float.IsInfinity(duration))
            throw new InvalidOperationException("EffectView duration must be positive and finite.");
        this.spawner = spawner;
        try
        {
            visualObject = pools.Spawn(visualPrefab, transform.position, transform.rotation, PoolGroup.Other, transform);
            if (visualObject == null) throw new InvalidOperationException("Could not spawn effect visual.");
            visual = visualObject.GetComponent<IEffectVisual>();
            if (visual == null) throw new InvalidOperationException("Effect visual prefab needs IEffectVisual on its root.");
            // Khôi phục pose gốc vì visual trong pool có thể đã đổi scale/rotation ở lần phát trước.
            visualObject.transform.localPosition = visualPrefab.transform.localPosition;
            visualObject.transform.localRotation = visualPrefab.transform.localRotation;
            visualObject.transform.localScale = visualPrefab.transform.localScale;
        }
        catch { Clear(); throw; }
    }

    // Chỉ phát sau Initialize thành công; duration của vỏ độc lập với thời lượng animation/particle con.
    private void OnEnable()
    {
        if (spawner == null || visualObject == null) return;
        lifetimeTimer = timers.Register(duration, this);
        visual.Play();
    }

    // So handle để bỏ callback lifetime cũ khi cùng vỏ đã được pool dùng lại.
    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        if (spawner != null && lifetimeTimer.Equals(handle) && result.Completed)
            spawner.Release(gameObject);
    }

    #endregion

    #region Clear & Despawn

    // Gọi TRƯỚC khi tắt vỏ: dừng/reset visual, trả con về pool rồi mới thu hồi cha.
    internal void Clear()
    {
        timers?.Cancel(lifetimeTimer);
        lifetimeTimer = default;
        spawner = null;
        // Xóa ownership trước callback StopAndReset/Despawn để cleanup lồng nhau không trả con hai lần.
        GameObject previousObject = visualObject;
        IEffectVisual previousVisual = visual;
        visualObject = null;
        visual = null;
        if (previousObject == null) return;
        try { previousVisual?.StopAndReset(); }
        finally { pools.Despawn(previousObject); }
    }

    // Chỉ dừng khi hierarchy đang tắt; không reparent/trả visual về pool bên trong OnDisable.
    private void OnDisable()
    {
        timers?.Cancel(lifetimeTimer);
        lifetimeTimer = default;
        if (visualObject != null) visual?.StopAndReset();
    }

    // Destroy hủy luôn con; không trả object đang bị hủy về pool.
    private void OnDestroy()
    {
        timers?.Cancel(lifetimeTimer);
        spawner = null;
        visualObject = null;
        visual = null;
    }

    #endregion
}
