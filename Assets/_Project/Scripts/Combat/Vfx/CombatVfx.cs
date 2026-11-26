using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// System VFX của miền Combat: phát các hiệu ứng hình ảnh chạy một lần (trúng đòn, nổ…) và thu hồi chúng.
/// </summary>
/// <remarks>
/// Chỉ là hình ảnh, không ảnh hưởng gameplay; effect gameplay (slow, burn…) thuộc <see cref="EffectSystem"/>.
/// Do <see cref="CombatManager"/> tạo; không bind vào Zenject.
/// Luồng: thuê vỏ <see cref="EffectView"/> → <see cref="EffectView.Play"/> → mỗi LateTick hỏi
/// <see cref="EffectView.Advance"/> → xong thì trả visual con rồi trả vỏ. Không lambda, không event, không cấp phát
/// mỗi lần phát.
/// </remarks>
public sealed class CombatVfx : IDisposable
{
    #region Dependencies And State

    private readonly IGameObjectPool pools;
    private readonly CombatAssetsSO combatAssets; // Catalog: EffectType → prefab visual.
    private readonly List<EffectView> activeEffects = new List<EffectView>(64); // Để tick và dọn hết khi kết thúc trận.
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
        GameObject shell = pools.Spawn(combatAssets.EffectPrefab.gameObject, position, Quaternion.identity, PoolGroup.Other);
        if (shell == null) return;
        EffectView view = shell.GetComponent<EffectView>();
        try { view.Play(pools, entry.VisualPrefab); }
        catch
        {
            // Phát lỗi thì trả vỏ ngay, không để một vỏ "trống" đang bật trong scene.
            ReleaseView(view);
            throw;
        }
        activeEffects.Add(view);
    }

    // Trả visual con trước (vỏ còn bật), rồi mới trả vỏ.
    private void ReleaseView(EffectView view)
    {
        if (view == null) return;
        try { view.Clear(); }
        finally { pools.Despawn(view.gameObject); }
    }

    // Swap-remove: đưa phần tử cuối vào chỗ trống để không phải dời cả danh sách.
    private void ReleaseAt(int index)
    {
        EffectView view = activeEffects[index];
        int last = activeEffects.Count - 1;
        activeEffects[index] = activeEffects[last];
        activeEffects.RemoveAt(last);
        ReleaseView(view);
    }

    #endregion

    #region Frame

    /// <summary>
    /// Hỏi mọi VFX đang phát đã xong chưa; xong thì thu hồi. CombatManager gọi ở LateTick.
    /// Duyệt ngược nên swap-remove chỉ đưa vào chỗ trống phần tử đã được xét trong frame này.
    /// </summary>
    internal void LateTick()
    {
        if (disposed || activeEffects.Count == 0) return;
        float deltaTime = Time.deltaTime;
        for (int index = activeEffects.Count - 1; index >= 0; index--)
        {
            EffectView view = activeEffects[index];
            bool finished;
            try { finished = view == null || view.Advance(deltaTime); }
            catch (Exception exception)
            {
                Debug.LogException(exception, view);
                finished = true; // Visual lỗi thì thu hồi luôn, không để kẹt trong danh sách.
            }
            if (finished) ReleaseAt(index);
        }
    }

    #endregion

    #region Cleanup

    /// <summary>Thu hồi mọi VFX đang phát, ví dụ khi trận kết thúc hoặc chơi lại.</summary>
    public void ClearEffects()
    {
        for (int index = activeEffects.Count - 1; index >= 0; index--)
            ReleaseAt(index);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ClearEffects();
    }

    #endregion
}
