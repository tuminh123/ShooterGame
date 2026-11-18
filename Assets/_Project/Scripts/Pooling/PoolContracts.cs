using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Ranh giới của miền Pool: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do PoolManager implement (bọc LeanPool, gom Hierarchy, inject clone mới).
// Pool là hạ tầng: không biết gameplay. Pool runtime C# thuần (CharacterRuntimePool, EffectRuntimePool…)
// thuộc miền sở hữu runtime đó, không thuộc đây.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region GameObject Pool (mọi miền gameplay và UI)

/// <summary>
/// Thuê và trả GameObject. Dùng bởi Character (vỏ, visual), Combat (đạn, VFX, visual skill), World (chunk nền),
/// Run (pickup) và UI (ô skill).
/// </summary>
public interface IGameObjectPool
{
    /// <summary>
    /// Lấy clone dưới nhánh "Preparing" đang tắt, chạy <paramref name="beforeActivate"/> rồi mới chuyển vào holder của nhóm,
    /// để OnEnable không bao giờ thấy trạng thái cũ. Trả về null nếu pool không tạo được.
    /// </summary>
    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, PoolGroup group = PoolGroup.Other, Transform parent = null, Action<GameObject> beforeActivate = null);

    /// <summary>Như trên, trả về component của clone (ví dụ <c>Spawn&lt;ProjectileView&gt;(prefab, …)</c>).</summary>
    T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, PoolGroup group = PoolGroup.Other, Transform parent = null)
        where T : Component;

    /// <summary>Trả clone về pool; gọi lại với clone đã trả thì không làm gì.</summary>
    void Despawn(GameObject clone);
}

#endregion

#region Preparation (GamePrewarm của miền Game)

/// <summary>Tạo sẵn clone và dung lượng theo dõi trong bước loading. Dùng bởi GamePrewarm (miền Game).</summary>
public interface IPoolPreparation
{
    /// <summary>Đặt trước chỗ cho số clone có thể cùng hoạt động, để không cấp phát lại giữa trận.</summary>
    void ReserveActiveCapacity(int count);

    /// <summary>Tạo sẵn <paramref name="count"/> clone đã inject, nhường frame sau mỗi <paramref name="batchSize"/> clone.</summary>
    UniTask PrewarmAsync(GameObject prefab, int count, int batchSize, Action<int> reportPrepared, CancellationToken cancellationToken);
}

#endregion

#region Cleanup (GameManager)

/// <summary>Lưới an toàn khi rời gameplay: thu hồi object của trận mà miền sở hữu đã quên dọn. Dùng bởi GameManager.</summary>
public interface IPoolCleanup
{
    /// <summary>Thu hồi mọi clone còn hoạt động trừ nền (World) và UI; trả về số clone bị sót (bình thường là 0).</summary>
    int DespawnGameplayClones();
}

#endregion
