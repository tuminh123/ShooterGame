using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// System VFX của miền Combat: phát các hiệu ứng hình ảnh chạy một lần (trúng đòn, nổ…) và thu hồi chúng.
/// </summary>
/// <remarks>
/// Chỉ là hình ảnh, không ảnh hưởng gameplay; effect gameplay (slow, burn…) thuộc <see cref="EffectSystem"/>.
/// Do <see cref="CombatManager"/> tạo; không bind vào Zenject.
/// </remarks>
public sealed class CombatVfx : IDisposable
{
    #region Dependencies And State

    private readonly IGameObjectPool pools;
    private readonly CombatAssetsSO combatAssets; // Catalog: EffectType → prefab visual.
    private readonly HashSet<EffectView> activeEffects = new HashSet<EffectView>(); // Để dọn hết khi kết thúc trận.
    private bool disposed;

    #endregion

    #region Constructor

    public CombatVfx(IGameObjectPool pools, CombatAssetsSO combatAssets)
    {
        this.pools = pools ?? throw new ArgumentNullException(nameof(pools));
        this.combatAssets = combatAssets != null ? combatAssets : throw new ArgumentNullException(nameof(combatAssets));
    }

    #endregion

    #region Spawn And Release

    /// <summary>Phát VFX <paramref name="effectType"/> tại vị trí cho trước. <see cref="EffectType.None"/> thì không làm gì.</summary>
    public void Spawn(EffectType effectType, Vector3 position)
    {
        if (disposed || effectType == EffectType.None) return;
        CombatAssetsSO.EffectEntry entry = combatAssets.GetEffect(effectType); // Ném lỗi nếu catalog thiếu loại này.
        pools.Spawn(combatAssets.EffectPrefab.gameObject, position, Quaternion.identity, PoolGroup.Other,
            beforeActivate: pooledObject =>
            {
                EffectView view = pooledObject.GetComponent<EffectView>();
                view.Initialize(this, pools, entry.visualPrefab);
                activeEffects.Add(view);
            });
    }

    /// <summary>Trả một VFX về pool: thu hồi visual con trước, rồi mới trả vỏ.</summary>
    internal void Release(EffectView effect)
    {
        if (effect == null) return;
        activeEffects.Remove(effect);
        try { effect.Clear(); }
        finally { pools.Despawn(effect.gameObject); }
    }

    #endregion

    #region Cleanup

    /// <summary>Thu hồi mọi VFX đang phát, ví dụ khi trận kết thúc hoặc chơi lại.</summary>
    public void ClearEffects()
    {
        // Duyệt bản sao vì Release xóa phần tử khỏi tập đang duyệt.
        foreach (EffectView effect in new List<EffectView>(activeEffects))
            if (effect != null) Release(effect);
        activeEffects.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ClearEffects();
    }

    #endregion
}
