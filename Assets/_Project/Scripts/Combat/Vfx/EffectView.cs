using System;
using UnityEngine;

/// <summary>
/// Vỏ của một VFX chạy một lần (trúng đòn, nổ…), là MonoBehaviour trên prefab và được pool tái sử dụng.
/// </summary>
/// <remarks>
/// Vỏ chỉ sở hữu visual con và việc trả về pool; visual con (<see cref="IEffectVisual"/>) tự quyết định
/// khi nào phát xong. Vòng đời: <see cref="CombatVfx"/> gọi <see cref="Initialize"/> khi vỏ còn tắt → pool bật
/// vỏ, <c>OnEnable</c> bắt đầu phát → visual báo <c>Completed</c> → vỏ tự trả về qua <see cref="CombatVfx.Release"/>.
/// Không đổi tên class này: prefab tham chiếu script qua GUID trong file .meta.
/// </remarks>
public sealed class EffectView : MonoBehaviour
{
    #region State

    private IGameObjectPool pools; // Giữ lại qua các lượt phát để Clear luôn trả được visual con.
    private CombatVfx owner; // Bên đã thuê vỏ này; null nghĩa là vỏ đang rảnh trong pool.
    private GameObject visualObject;
    private IEffectVisual visual;
    private Action completed;

    // Tăng mỗi lần hủy đăng ký. Callback Completed của lượt phát cũ so sánh version để không trả nhầm vỏ
    // đã được thuê lại cho lượt mới.
    private int playbackVersion;

    #endregion

    #region Initialization

    /// <summary>Gắn visual con cho một lượt phát. Phải gọi khi vỏ còn tắt; chỉ phát khi cả nhánh được bật.</summary>
    internal void Initialize(CombatVfx vfxOwner, IGameObjectPool objectPools, GameObject visualPrefab)
    {
        pools = objectPools; // Gán trước Clear, vì Clear cần pool để trả visual của lượt trước.
        Clear();
        owner = vfxOwner;
        try
        {
            visualObject = pools.Spawn(visualPrefab, transform.position, transform.rotation, PoolGroup.Other, transform);
            if (visualObject == null) throw new InvalidOperationException("Could not spawn effect visual.");
            visual = visualObject.GetComponent<IEffectVisual>();
            if (visual == null) throw new InvalidOperationException("Effect visual prefab needs IEffectVisual on its root.");
            visualObject.transform.localPosition = visualPrefab.transform.localPosition;
            visualObject.transform.localRotation = visualPrefab.transform.localRotation;
            visualObject.transform.localScale = visualPrefab.transform.localScale;
        }
        catch { Clear(); throw; }
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        // Vỏ prewarm được bật mà chưa qua Initialize thì không có gì để phát.
        if (owner == null || visualObject == null) return;
        Unsubscribe();
        int version = playbackVersion;
        completed = () => ReleaseIfCurrent(version);
        visual.Completed += completed;
        try { visual.Play(); }
        catch
        {
            ReleaseIfCurrent(version);
            throw;
        }
    }

    // Không reparent hay trả visual con về pool trong callback tắt hierarchy; chỉ dừng hiển thị.
    private void OnDisable()
    {
        Unsubscribe();
        if (visualObject != null) visual?.StopAndReset();
    }

    // Unity hủy cả visual con cùng lúc, nên chỉ dừng và bỏ reference, không trả về pool.
    private void OnDestroy()
    {
        Unsubscribe();
        visual?.StopAndReset();
        owner = null;
        visualObject = null;
        visual = null;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Dừng và trả visual con về pool. <see cref="CombatVfx"/> gọi trước khi trả vỏ.
    /// Gỡ callback trước StopAndReset/Despawn để không trả cùng một vỏ hai lần.
    /// </summary>
    internal void Clear()
    {
        Unsubscribe();
        owner = null;
        GameObject previousObject = visualObject;
        IEffectVisual previousVisual = visual;
        visualObject = null;
        visual = null;
        if (previousObject == null) return;
        try { previousVisual?.StopAndReset(); }
        finally { pools.Despawn(previousObject); }
    }

    private void ReleaseIfCurrent(int version)
    {
        if (version == playbackVersion && owner != null) owner.Release(this);
    }

    private void Unsubscribe()
    {
        playbackVersion++;
        if (visual != null && completed != null) visual.Completed -= completed;
        completed = null;
    }

    #endregion
}
