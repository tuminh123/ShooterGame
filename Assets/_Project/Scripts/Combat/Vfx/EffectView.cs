using System;
using UnityEngine;

/// <summary>
/// Vỏ của một VFX chạy một lần (trúng đòn, nổ…), là MonoBehaviour trên prefab và được pool tái sử dụng.
/// </summary>
/// <remarks>
/// Vỏ dùng chung cho mọi <see cref="EffectType"/>; mỗi lượt phát thuê một visual con theo loại, nên đổi hình chỉ cần
/// đổi prefab visual trong <see cref="CombatAssetsSO"/>. Vòng đời: <see cref="CombatVfx"/> thuê vỏ (đã bật) → gọi
/// <see cref="Play"/> → mỗi frame gọi <see cref="Advance"/> → xong thì <see cref="Clear"/> rồi trả vỏ.
/// Vỏ không làm gì trong <c>OnEnable</c>, nên không cần gán dữ liệu khi vỏ còn tắt (không cần lambda <c>beforeActivate</c>).
/// Không đổi tên class này: prefab tham chiếu script qua GUID trong file .meta.
/// </remarks>
public sealed class EffectView : MonoBehaviour
{
    #region State

    private IGameObjectPool pools; // Giữ lại qua các lượt phát để Clear luôn trả được visual con.
    private GameObject visualObject;
    private IEffectVisual visual;

    #endregion

    #region Playback

    /// <summary>Gắn visual con cho lượt phát này rồi phát ngay. Gọi ngay sau khi thuê vỏ (vỏ đã bật).</summary>
    internal void Play(IGameObjectPool objectPools, GameObject visualPrefab)
    {
        pools = objectPools; // Gán trước Clear, vì Clear cần pool để trả visual của lượt trước.
        Clear();
        try
        {
            // Vỏ đang bật nên visual bật ngay khi được gắn: truyền sẵn vị trí/góc thế giới đúng theo transform local của prefab.
            visualObject = pools.Spawn(visualPrefab, transform.TransformPoint(visualPrefab.transform.localPosition),
                transform.rotation * visualPrefab.transform.localRotation, PoolGroup.Other, transform);
            if (visualObject == null) throw new InvalidOperationException("Could not spawn effect visual.");
            visual = visualObject.GetComponent<IEffectVisual>();
            if (visual == null) throw new InvalidOperationException("Effect visual prefab needs IEffectVisual on its root.");
            visualObject.transform.localPosition = visualPrefab.transform.localPosition;
            visualObject.transform.localRotation = visualPrefab.transform.localRotation;
            visualObject.transform.localScale = visualPrefab.transform.localScale;
            visual.Play();
        }
        catch { Clear(); throw; }
    }

    /// <summary>Chuyển frame xuống visual con; trả về true khi đã phát xong (hoặc không còn gì để phát).</summary>
    internal bool Advance(float deltaTime) => visual == null || visual.Advance(deltaTime);

    #endregion

    #region Unity Lifecycle

    // Không reparent hay trả visual con về pool trong callback tắt hierarchy; chỉ dừng hiển thị.
    private void OnDisable()
    {
        if (visualObject != null) visual?.StopAndReset();
    }

    // Unity hủy cả visual con cùng lúc, nên chỉ dừng và bỏ reference, không trả về pool.
    private void OnDestroy()
    {
        visual?.StopAndReset();
        visualObject = null;
        visual = null;
    }

    #endregion

    #region Cleanup

    /// <summary>Dừng và trả visual con về pool. <see cref="CombatVfx"/> gọi trước khi trả vỏ.</summary>
    internal void Clear()
    {
        // Xóa reference trước khi trả, để gọi lặp không trả cùng một visual hai lần.
        GameObject previousObject = visualObject;
        IEffectVisual previousVisual = visual;
        visualObject = null;
        visual = null;
        if (previousObject == null) return;
        try { previousVisual?.StopAndReset(); }
        finally { pools.Despawn(previousObject); }
    }

    #endregion
}
