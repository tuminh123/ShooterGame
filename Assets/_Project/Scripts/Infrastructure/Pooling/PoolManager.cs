using System;
using System.Collections.Generic;
using Lean.Pool;
using UnityEngine;
using Zenject;

// * Nhóm holder cho object đang hoạt động, không quyết định hành vi của prefab.
public enum PoolGroup
{
    Enemies,
    Bullets,
    World,
    Other
}

// * Class thuần bọc LeanPool để gom Hierarchy và inject bản sao mới trước OnEnable.
// ! API gọi từ main thread; pool tự tạo dùng OnEnable/OnDisable thay cho thông báo IPoolable.
public sealed class PoolManager : IDisposable
{

    #region Pool State & Holders

    private readonly DiContainer container;  // * Container scene cung cấp dependency cho object mới.
    private readonly Dictionary<GameObject, LeanGameObjectPool> pools = new Dictionary<GameObject, LeanGameObjectPool>(); // * Tra pool theo prefab, dùng lại qua các lượt spawn.
    private readonly HashSet<LeanGameObjectPool> ownedPools = new HashSet<LeanGameObjectPool>();  // * Chỉ hủy pool tự tạo.
    private readonly HashSet<GameObject> injectedClones = new HashSet<GameObject>();  // * Inject một lần mỗi bản sao.
    private readonly HashSet<GameObject> activeClones = new HashSet<GameObject>();    // * Clone cần thu hồi khi kết thúc.
    private readonly Transform[] holders = new Transform[4];  // * Holder theo thứ tự PoolGroup.
    private Transform root;           // * Gốc Pool Holders với transform mặc định.
    private Transform poolsHolder;    // * Chứa pool và các object đã thu hồi.
    private Transform stagingHolder;  // * Nhánh tắt trong lúc chuẩn bị injection.
    private bool disposed;            // * Ngăn spawn sau khi dịch vụ được giải phóng.

    #endregion

    #region Initialization & Holder Access

    // * Nhận container; chỉ tạo holder khi có lần sử dụng đầu tiên.
    public PoolManager(DiContainer container)
    {
        this.container = container;
    }

    // * Trả holder theo nhóm, dùng chung cho enemy, đạn, boss hoặc object khác.
    public Transform GetHolder(PoolGroup group)
    {
        if ((int)group < 0 || (int)group >= holders.Length)
            throw new ArgumentOutOfRangeException(nameof(group));
        EnsureHolders();
        return holders[(int)group];
    }

    #endregion

    #region Spawn & Despawn

    // * Spawn theo world-space; parent tùy chọn cho object có nhánh riêng như chunk.
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
        PoolGroup group = PoolGroup.Other, Transform parent = null, Action<GameObject> beforeActivate = null)
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
            if (!injectedClones.Contains(clone))
            {
                container.InjectGameObject(clone);
                injectedClones.Add(clone);
            }
            activeClones.Add(clone);
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
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        // * OnDisable của vỏ thu hồi cả visual con, làm thay đổi activeClones; duyệt bản sao để tránh lỗi collection.
        GameObject[] remaining = new GameObject[activeClones.Count];
        activeClones.CopyTo(remaining);
        foreach (GameObject clone in remaining)
            if (clone != null && LeanPool.Links.ContainsKey(clone))
                LeanPool.Despawn(clone);
        activeClones.Clear();
        foreach (LeanGameObjectPool pool in ownedPools)
            if (pool != null)
                pool.DespawnAll(true);
        pools.Clear();
        ownedPools.Clear();
        injectedClones.Clear();
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
