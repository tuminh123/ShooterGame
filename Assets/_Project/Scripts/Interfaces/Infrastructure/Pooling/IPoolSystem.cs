using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

#region GameObjectPool

/// <summary>Thuê và trả GameObject; callback chuẩn bị chạy trước khi bật object.</summary>
public interface IGameObjectPool
{
    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, PoolGroup group = PoolGroup.Other, Transform parent = null, Action<GameObject> beforeActivate = null);
    T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, PoolGroup group = PoolGroup.Other, Transform parent = null)
        where T : Component;
    void Despawn(GameObject clone);
}

#endregion

#region PoolPreparation

/// <summary>Chuẩn bị clone và dung lượng tracking trước gameplay.</summary>
public interface IPoolPreparation
{
    void ReserveActiveCapacity(int count);
    UniTask PrewarmAsync(GameObject prefab, int count, int batchSize, Action<int> reportPrepared, CancellationToken cancellationToken);
}

#endregion

#region PoolHierarchy

/// <summary>Cung cấp nhánh cha tổ chức các object theo nhóm pool.</summary>
public interface IPoolHierarchy
{
    Transform GetHolder(PoolGroup group);
}

#endregion

#region PoolCleanup

/// <summary>Lưới an toàn khi rời gameplay: thu hồi object của trận mà miền sở hữu đã quên dọn. Dùng bởi GameManager.</summary>
public interface IPoolCleanup
{
    /// <summary>Thu hồi mọi clone còn hoạt động trừ nền (World) và UI; trả về số clone bị sót (bình thường là 0).</summary>
    int DespawnGameplayClones();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của PoolManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IPoolSystem
{
    IGameObjectPool GetGameObjectPool();
    IPoolPreparation GetPoolPreparation();
    IPoolHierarchy GetPoolHierarchy();
    IPoolCleanup GetPoolCleanup();
}

#endregion
