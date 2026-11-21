using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nền lặp vô hạn: giữ một lưới chunk vuông quanh mục tiêu (Player) và lấy chunk từ pool.
/// Chỉ dựng lại khi mục tiêu sang chunk khác, nên phần lớn các frame không làm gì.
/// </summary>
/// <remarks>
/// Do <see cref="WorldManager"/> tạo và tick; không bind vào Zenject. Không biết map nào đang chọn,
/// chỉ nhận sprite cần gán (<see cref="SetSprite"/>).
/// </remarks>
internal sealed class GroundChunks : IDisposable
{
    #region Configuration

    private readonly IGameObjectPool pool;
    private readonly GameObject template; // Mẫu chunk (prefab có SpriteRenderer ở root).
    private readonly Transform parent; // Cha của các chunk; nên giữ scale 1 để kích thước khớp khoảng cách lưới.
    private readonly float chunkSize; // Cạnh một chunk theo world unit; phải khớp kích thước hiển thị của sprite.
    private readonly int radius; // Số vòng chunk quanh chunk trung tâm: cạnh lưới = 2 * radius + 1.

    #endregion

    #region State

    private readonly Dictionary<Vector2Int, GameObject> active = new Dictionary<Vector2Int, GameObject>(); // Tọa độ chunk → bản sao đang hiện.
    private readonly HashSet<Vector2Int> required = new HashSet<Vector2Int>(); // Tập cần giữ ở lần dựng hiện tại.
    private readonly List<Vector2Int> outOfRange = new List<Vector2Int>(); // Dùng lại giữa các lần dựng; không sửa dictionary khi đang duyệt.
    private Transform target; // Tâm lưới; null khi chưa có Player (nền đứng yên, chunk cũ giữ nguyên).
    private Vector2Int lastCenter; // Chunk chứa mục tiêu ở lần dựng trước.
    private Sprite sprite; // Sprite của map đang chọn; null thì giữ sprite có sẵn trên prefab.

    #endregion

    #region Lifecycle

    internal GroundChunks(IGameObjectPool pool, GameObject template, Transform parent, float chunkSize, int radius)
    {
        this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
        this.template = template;
        this.parent = parent;
        this.chunkSize = chunkSize;
        this.radius = radius;
        // Mẫu đặt trong scene thì tắt đi để chỉ các bản sao từ pool hiển thị.
        // Mẫu là prefab asset thì không đụng vào: SetActive trên asset sẽ sửa thẳng file prefab.
        if (template.scene.IsValid())
            template.SetActive(false);
    }

    /// <summary>Trả mọi chunk về pool. Gọi trước khi pool bị dispose.</summary>
    public void Dispose()
    {
        foreach (GameObject chunk in active.Values)
            if (chunk != null)
                pool.Despawn(chunk);
        active.Clear();
        target = null;
    }

    #endregion

    #region Streaming

    /// <summary>Đổi tâm lưới (Player mới hoặc Player bị gỡ). Có mục tiêu thì dựng lưới quanh nó ngay.</summary>
    internal void Follow(Transform next)
    {
        target = next;
        if (target == null)
            return;
        lastCenter = ChunkOf(target.position);
        Refresh(lastCenter);
    }

    /// <summary>Mỗi frame: chỉ dựng lại khi mục tiêu sang chunk khác.</summary>
    internal void Tick()
    {
        if (target == null)
            return;
        Vector2Int center = ChunkOf(target.position);
        if (center == lastCenter)
            return;
        lastCenter = center;
        Refresh(center);
    }

    // Thu hồi chunk ngoài vùng trước (để pool dùng lại ngay), rồi bổ sung chunk còn thiếu.
    // Vị trí tính trực tiếp từ tọa độ nên mục tiêu vượt nhiều ô một lúc vẫn đúng.
    private void Refresh(Vector2Int center)
    {
        required.Clear();
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                required.Add(center + new Vector2Int(x, y));

        outOfRange.Clear();
        foreach (Vector2Int coordinate in active.Keys)
            if (!required.Contains(coordinate))
                outOfRange.Add(coordinate);
        foreach (Vector2Int coordinate in outOfRange)
        {
            pool.Despawn(active[coordinate]);
            active.Remove(coordinate);
        }

        foreach (Vector2Int coordinate in required)
        {
            if (active.ContainsKey(coordinate))
                continue;
            // Lưới neo tại gốc XY = (0, 0); Z giữ theo mẫu.
            var position = new Vector3(coordinate.x * chunkSize, coordinate.y * chunkSize, template.transform.position.z);
            GameObject chunk = pool.Spawn(template, position, Quaternion.identity, PoolGroup.World, parent);
            if (chunk == null)
                continue;
            ApplySprite(chunk); // Chunk tái sử dụng có thể còn sprite của map cũ.
            active.Add(coordinate, chunk);
        }
    }

    // Cộng nửa chunk trước khi làm tròn xuống để chunk i có tâm tại i * chunkSize
    // (chunkSize 4: chunk 0 phủ [-2, 2), chunk 1 phủ [2, 6)).
    private Vector2Int ChunkOf(Vector3 position) => new Vector2Int(
        Mathf.FloorToInt((position.x + chunkSize * 0.5f) / chunkSize),
        Mathf.FloorToInt((position.y + chunkSize * 0.5f) / chunkSize));

    #endregion

    #region Map Sprite

    /// <summary>Gán sprite của map mới cho mọi chunk đang hiện và các chunk tạo sau.</summary>
    internal void SetSprite(Sprite next)
    {
        sprite = next;
        foreach (GameObject chunk in active.Values)
            ApplySprite(chunk);
    }

    // Chỉ sửa bản sao đang chạy, không sửa prefab hay database. Draw mode Tiled/Sliced thì khớp kích thước với lưới.
    private void ApplySprite(GameObject chunk)
    {
        if (chunk == null || sprite == null || !chunk.TryGetComponent(out SpriteRenderer renderer))
            return;
        renderer.sprite = sprite;
        if (renderer.drawMode != SpriteDrawMode.Simple)
            renderer.size = Vector2.one * chunkSize;
    }

    #endregion
}
