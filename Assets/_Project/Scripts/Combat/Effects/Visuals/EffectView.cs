using System;
using UnityEngine;
using Zenject;

// Vỏ VFX chỉ sở hữu visual và pooling; visual quyết định khi nào phát xong.
public sealed class EffectView : MonoBehaviour
{

    #region Dependencies And State
    private IGameObjectPool pools;
    private ICombatObjectRelease spawner;
    private GameObject visualObject;
    private IEffectVisual visual;
    private Action completed;
    private int playbackVersion;

    #endregion

    #region Initialization

    [Inject]
    public void Construct(IPoolSystem poolSystem)=> this.pools = poolSystem.GetGameObjectPool();

    // Chuẩn bị khi vỏ còn tắt, chỉ phát sau khi cả nhánh được bật.
    internal void Initialize(ICombatObjectRelease spawner, GameObject visualPrefab)
    {
        Clear();
        this.spawner = spawner;
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
        if (spawner == null || visualObject == null) return;
        Unsubscribe();
        int version = playbackVersion;
        completed = () =>
        {
            if (version == playbackVersion && spawner != null) spawner.Release(gameObject);
        };
        visual.Completed += completed;
        try { visual.Play(); }
        catch
        {
            if (version == playbackVersion && spawner != null) spawner.Release(gameObject);
            throw;
        }
    }

    #endregion

    #region Runtime Operations

    private void Unsubscribe()
    {
        playbackVersion++;
        if (visual != null && completed != null) visual.Completed -= completed;
        completed = null;
    }

    #endregion

    #region Cleanup

    // Gỡ callback trước StopAndReset/Despawn để không trả cùng object hai lần.
    internal void Clear()
    {
        Unsubscribe();
        spawner = null;
        GameObject previousObject = visualObject;
        IEffectVisual previousVisual = visual;
        visualObject = null;
        visual = null;
        if (previousObject == null) return;
        try { previousVisual?.StopAndReset(); }
        finally { pools.Despawn(previousObject); }
    }

    #endregion

    #region Unity Lifecycle

    // Không reparent/trả con về pool trong callback tắt hierarchy.
    private void OnDisable()
    {
        Unsubscribe();
        if (visualObject != null) visual?.StopAndReset();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        visual?.StopAndReset();
        spawner = null;
        visualObject = null;
        visual = null;
    }

    #endregion
}
