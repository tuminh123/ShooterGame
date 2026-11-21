using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lean.Pool;
using UnityEngine;

/// <summary>
/// Manager của miền Pool: bọc LeanPool và gom object vào holder theo <see cref="PoolGroup"/>.
/// Đây là class <b>duy nhất</b> của Pool được bind vào Zenject; miền khác chỉ thấy <c>PoolContracts.cs</c>.
/// </summary>
/// <remarks>
/// API gọi từ main thread; pool tự tạo dùng OnEnable/OnDisable thay cho thông báo IPoolable.
/// Clone KHÔNG được Zenject inject: prefab trong pool (nhân vật, đạn, VFX, loot, ô skill) không có [Inject];
/// dữ liệu cần thiết do Manager sở hữu truyền vào qua <c>beforeActivate</c> hoặc hàm Initialize của view.
/// Bước prewarm (kế hoạch số lượng của mọi miền) không nằm ở đây: nó cần Character/Combat/Simulation, mà các miền đó
/// lại phụ thuộc Pool, nên đặt trong Pool sẽ tạo vòng phụ thuộc. Xem GamePrewarm (miền Game).
/// </remarks>
public sealed class PoolManager : DomainManager, IDisposable, IGameObjectPool, IPoolPreparation, IPoolCleanup
{

    #region Pool State & Holders

    private readonly Dictionary<GameObject, LeanGameObjectPool> pools = new Dictionary<GameObject, LeanGameObjectPool>(); // * Tra pool theo prefab, dùng lại qua các lượt spawn.
    private readonly HashSet<LeanGameObjectPool> ownedPools = new HashSet<LeanGameObjectPool>();  // * Chỉ hủy pool tự tạo.
    // * Clone đang hoạt động → nhóm lúc spawn. Nhóm dùng để biết clone nào là của trận khi rời gameplay.
    private readonly Dictionary<GameObject, PoolGroup> activeClones = new Dictionary<GameObject, PoolGroup>();
    private readonly List<GameObject> sweepBuffer = new List<GameObject>(); // Dùng lại giữa các lần quét; Despawn sửa activeClones.
    private readonly Transform[] holders = new Transform[5];  // * Holder theo thứ tự PoolGroup.
    private Transform root;           // * Gốc Pool Holders với transform mặc định.
    private Transform poolsHolder;    // * Chứa pool và các object đã thu hồi.
    private Transform stagingHolder;  // * Nhánh tắt: clone được chuẩn bị ở đây trước khi OnEnable chạy.
    private bool disposed;            // * Ngăn spawn sau khi dịch vụ được giải phóng.

    #endregion

    #region Initialization & Holder Access

    // * Trả holder theo nhóm, dùng chung cho enemy, đạn, boss hoặc object khác. Chỉ dùng bên trong miền Pool.
    private Transform GetHolder(PoolGroup group)
    {
        if ((int)group < 0 || (int)group >= holders.Length)
            throw new ArgumentOutOfRangeException(nameof(group));
        EnsureHolders();
        return holders[(int)group];
    }

    #endregion

    #region Spawn & Despawn

    public void ReserveActiveCapacity(int count)
    {
        EnsureHolders();
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        activeClones.EnsureCapacity(count);
    }

    // Chuẩn bị đủ clone rảnh trước gameplay, không gọi Initialize/OnEnable của nhân vật/đạn.
    public async UniTask PrewarmAsync(GameObject prefab, int count, int batchSize,
        Action<int> reportPrepared, CancellationToken cancellationToken)
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize));
        EnsureHolders();
        if (count == 0) return;
        LeanGameObjectPool pool = GetPool(prefab);
        if (pool.Strategy != LeanGameObjectPool.StrategyType.ActivateAndDeactivate ||
            pool.Notification != LeanGameObjectPool.NotificationType.None)
            throw new InvalidOperationException($"Prewarm requires inactive clones without pool notifications: {prefab.name}.");
        if (pool.Capacity > 0 && count > pool.Capacity - pool.Spawned)
            throw new InvalidOperationException($"Prewarm exceeds available pool capacity: {prefab.name}.");

        var prepared = new List<GameObject>(count);
        bool previousRecycle = pool.Recycle;
        pool.Recycle = false; // Không lấy cắp object đang hoạt động để đáp ứng số lượng prewarm.
        try
        {
            // Giữ đồng thời count clone: spawn/despawn ngay trong vòng lặp chỉ làm nóng đúng một clone.
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameObject clone = LeanPool.Spawn(prefab, Vector3.zero, Quaternion.identity, stagingHolder);
                if (clone == null) throw new InvalidOperationException($"Could not prewarm {prefab.name}.");
                prepared.Add(clone);
                reportPrepared?.Invoke(1);
                // Giữ các clone đã lấy để batch sau không lấy lại chúng; nhường frame cho Loading vẽ.
                if ((index + 1) % batchSize == 0)
                    await UniTask.NextFrame(cancellationToken: cancellationToken);
            }
        }
        finally
        {
            foreach (GameObject clone in prepared)
                if (clone != null && LeanPool.Links.ContainsKey(clone)) LeanPool.Despawn(clone);
            if (pool != null) pool.Recycle = previousRecycle;
        }
    }

    // * Spawn theo world-space; parent tùy chọn cho object có nhánh riêng như chunk.
    public GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        PoolGroup group = PoolGroup.Other,
        Transform parent = null,
        Action<GameObject> beforeActivate = null)
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));
        EnsureHolders();
        Transform holder = parent != null ? parent : GetHolder(group);
        GetPool(prefab);
        GameObject clone = LeanPool.Spawn(prefab, position, rotation, stagingHolder);
        if (clone == null)
            return null;
        try
        {
            activeClones[clone] = group;
            // * Gán runtime/controller khi clone còn ở Preparing; không để OnEnable nhìn thấy trạng thái cũ.
            beforeActivate?.Invoke(clone);
            clone.transform.SetParent(holder, true);
            return clone;
        }
        catch
        {
            activeClones.Remove(clone);
            LeanPool.Despawn(clone);
            throw;
        }
    }

    // * Giữ kiểu component, ví dụ Spawn<Bullet>(bulletPrefab, ...).
    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation,
        PoolGroup group = PoolGroup.Other, Transform parent = null) where T : Component
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));
        GameObject clone = Spawn(prefab.gameObject, position, rotation, group, parent);
        return clone != null ? clone.GetComponent<T>() : null;
    }

    // * Thu hồi qua LeanPool; gọi hai lần không hủy nhầm clone đã nằm trong pool.
    public void Despawn(GameObject clone)
    {
        if (clone == null)
            return;
        activeClones.Remove(clone);
        if (LeanPool.Links.ContainsKey(clone))
            LeanPool.Despawn(clone);
    }

    #endregion

    #region Gameplay Cleanup

    /// <summary>
    /// Lưới an toàn khi rời gameplay: thu hồi mọi clone còn hoạt động thuộc nhóm của trận (Enemies, Bullets, Other).
    /// Giữ lại nền (World) và UI. Gọi SAU khi các miền đã tự dọn theo quy trình; trả về số clone bị sót.
    /// </summary>
    /// <remarks>
    /// Bình thường kết quả là 0. Khác 0 nghĩa là một miền đã quên dọn object của nó, nên log warning kèm tên object
    /// để tìm nguyên nhân gốc. Object bị thu hồi ở đây không chạy cleanup của miền (registry, runtime…).
    /// </remarks>
    public int DespawnGameplayClones()
    {
        if (disposed)
            return 0;
        sweepBuffer.Clear();
        foreach (KeyValuePair<GameObject, PoolGroup> entry in activeClones)
            if (entry.Value != PoolGroup.World && entry.Value != PoolGroup.UI)
                sweepBuffer.Add(entry.Key);

        int count = 0;
        var names = new System.Text.StringBuilder();
        foreach (GameObject clone in sweepBuffer)
        {
            // Thu hồi một vỏ có thể kéo theo visual con của nó: bỏ qua clone đã được trả trong vòng lặp này.
            if (!activeClones.TryGetValue(clone, out PoolGroup group))
                continue;
            count++;
            if (clone == null)
            {
                activeClones.Remove(clone);
                continue;
            }
            names.Append("\n- ").Append(clone.name).Append(" (").Append(group).Append(clone.activeInHierarchy ? ", đang hiện)" : ", đang ẩn)");
            Despawn(clone);
        }
        sweepBuffer.Clear();
        if (count > 0)
            Debug.LogWarning($"[PoolManager] Rời gameplay còn sót {count} object chưa được miền sở hữu thu hồi; đã thu hồi:{names}");
        return count;
    }

    #endregion

    #region Pool & Holder Creation

    // * Các holder có position zero, rotation identity, scale one để không làm lệch object.
    private void EnsureHolders()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PoolManager));
        if (root != null)
            return;
        root = new GameObject("Pool Holders").transform;
        holders[(int)PoolGroup.Enemies] = CreateHolder("Enemies", root);
        holders[(int)PoolGroup.Bullets] = CreateHolder("Bullets", root);
        holders[(int)PoolGroup.World] = CreateHolder("World", root);
        holders[(int)PoolGroup.Other] = CreateHolder("Other", root);
        holders[(int)PoolGroup.UI] = CreateHolder("UI", root);
        poolsHolder = CreateHolder("Pools", root);
        stagingHolder = CreateHolder("Preparing", poolsHolder);
        stagingHolder.gameObject.SetActive(false);
    }

    // * Một pool mỗi prefab; giữ pool có sẵn và chỉ nhận quyền sở hữu pool do dịch vụ tạo.
    private LeanGameObjectPool GetPool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out LeanGameObjectPool pool) && pool != null)
            return pool;
        if (!LeanGameObjectPool.TryFindPoolByPrefab(prefab, ref pool))
        {
            Transform holder = CreateHolder("LeanPool (" + prefab.name + ")", poolsHolder);
            pool = holder.gameObject.AddComponent<LeanGameObjectPool>();
            pool.Notification = LeanGameObjectPool.NotificationType.None;
            pool.Prefab = prefab;
            ownedPools.Add(pool);
        }
        pools[prefab] = pool;
        return pool;
    }

    // * Tạo nhánh với local transform mặc định.
    private static Transform CreateHolder(string name, Transform parent)
    {
        Transform holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        return holder;
    }

    #endregion

    #region Scene Cleanup

    // * Thu hồi clone đang quản lý và hủy pool tự tạo; không DespawnAll toàn cục.
    public override void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        // * OnDisable của vỏ thu hồi cả visual con, làm thay đổi activeClones; duyệt bản sao để tránh lỗi collection.
        GameObject[] remaining = new GameObject[activeClones.Count];
        activeClones.Keys.CopyTo(remaining, 0);
        foreach (GameObject clone in remaining)
            if (clone != null && LeanPool.Links.ContainsKey(clone))
                LeanPool.Despawn(clone);
        activeClones.Clear();
        foreach (LeanGameObjectPool pool in ownedPools)
            if (pool != null)
                pool.DespawnAll(true);
        pools.Clear();
        ownedPools.Clear();
        if (root == null)
            return;
        root.gameObject.SetActive(false);
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(root.gameObject);
        else
            UnityEngine.Object.DestroyImmediate(root.gameObject);
    }

    #endregion

}
