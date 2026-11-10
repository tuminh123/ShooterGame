using System;
using UnityEngine;
using Zenject;

// Vỏ VFX chỉ sở hữu visual và pooling; visual quyết định khi nào phát xong.
public sealed class EffectView : MonoBehaviour
{
    private PoolManager pools;
    private CombatSpawner spawner;
    private GameObject visualObject;
    private IEffectVisual visual;
    private Action completed;
    private int playbackVersion;

    [Inject]
    public void Construct(PoolManager pools) => this.pools = pools;

    // Chuẩn bị khi vỏ còn tắt, chỉ phát sau khi cả nhánh được bật.
    internal void Initialize(CombatSpawner spawner, GameObject visualPrefab)
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

    private void Unsubscribe()
    {
        playbackVersion++;
        if (visual != null && completed != null) visual.Completed -= completed;
        completed = null;
    }

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
}
