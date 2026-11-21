using System;
using UnityEngine;

/// <summary>
/// Lực tách đàn của MỘT con quái: quét hàng xóm theo nhịp riêng và giữ hướng ra xa giữa hai lần quét,
/// để quái không chồng lên nhau trong đám đông.
/// </summary>
/// <remarks>
/// <see cref="EnemyController"/> tạo một instance cho mỗi con quái; mọi state ở đây thuộc riêng con đó.
/// Tìm hàng xóm qua <see cref="CharacterSearch"/> (snapshot hitbox + lưới chung), không dùng collider.
/// </remarks>
internal sealed class EnemySeparation
{
    #region Configuration And State

    private readonly float radius;       // Khoảng cách mong muốn giữa hai tâm, tính bằng world unit.
    private readonly float offsetY;      // Dời tâm quét theo Y; không bị scale hoặc flip của visual ảnh hưởng.
    private readonly float strength;     // Trọng số hướng tách; bằng 0 thì bỏ qua truy vấn hàng xóm.
    private readonly float scanInterval; // Số giây game giữa hai lần quét, tối thiểu 0.01.

    // Mảng riêng của con quái, chỉ tăng khi thiếu chỗ; không mượn/trả mỗi lần tìm.
    private CharacterHandle[] neighborBuffer = Array.Empty<CharacterHandle>();
    private float scanRemaining;
    private bool timingInitialized;
    private bool scanReady; // Nhiều nhịp trễ được gộp thành một lần quét.

    /// <summary>Chốt cấu hình cho con quái này; không sửa cấu hình dùng chung.</summary>
    internal EnemySeparation(EnemyController.SeparationSettings settings)
    {
        radius = Mathf.Max(0.01f, settings.radius);
        offsetY = settings.offsetY;
        strength = Mathf.Max(0f, settings.strength);
        scanInterval = Mathf.Max(0.01f, settings.scanInterval);
    }

    /// <summary>Bán kính quét đã chốt (dùng cho gizmo).</summary>
    internal float Radius => radius;

    /// <summary>Trọng số cộng hướng tách vào hướng đuổi Player.</summary>
    internal float Strength => strength;

    /// <summary>Hướng ra xa hàng xóm của lần quét gần nhất, độ dài tối đa 1.</summary>
    internal Vector2 Direction { get; private set; }

    #endregion

    #region Frame

    /// <summary>Bỏ hướng tách cũ (đang lùi sau đòn đánh, mất mục tiêu, vào đội hình).</summary>
    internal void Clear() => Direction = Vector2.zero;

    /// <summary>
    /// Đếm nhịp quét riêng của con quái. Lần đầu chia pha theo instance ID để cả đàn không quét cùng một frame;
    /// frame dài chỉ đánh dấu quét một lần.
    /// </summary>
    internal void AdvanceClock(CharacterView view, float deltaTime)
    {
        if (!timingInitialized)
        {
            timingInitialized = true;
            scanRemaining = scanInterval * (unchecked((uint)view.GetInstanceID()) % 16u) / 16f;
            return;
        }

        if (strength <= 0f) return;
        scanRemaining -= deltaTime;
        if (scanRemaining <= 0f)
        {
            scanReady = true;
            scanRemaining += (Mathf.Floor(-scanRemaining / scanInterval) + 1f) * scanInterval;
        }
    }

    /// <summary>Tính lại hướng tách khi đến lịch quét; giữa hai lần quét dùng lại hướng đã tính.</summary>
    internal void Update(CharacterView view)
    {
        if (strength <= 0f || !scanReady) return;
        scanReady = false;
        Direction = Calculate(view);
    }

    /// <summary>
    /// Tâm quét dùng chung cho truy vấn, khoảng cách và gizmo để hình vẽ khớp logic.
    /// Offset giống nhau triệt tiêu khi trừ hai tâm; nó dời vùng query, không đổi khoảng cách giữa quái.
    /// </summary>
    internal Vector3 GetCenter(Vector3 position)
    {
        position.y += offsetY;
        return position;
    }

    /// <summary>Xóa tham chiếu hàng xóm và nhịp quét khi controller bị dispose.</summary>
    internal void Reset()
    {
        scanRemaining = 0f;
        Array.Clear(neighborBuffer, 0, neighborBuffer.Length);
        neighborBuffer = Array.Empty<CharacterHandle>();
    }

    #endregion

    #region Neighbor Query

    // Vùng query chạm hitbox; kiểm tra lại khoảng cách giữa hai tâm để lực tách về 0 đúng bán kính.
    private Vector2 Calculate(CharacterView view)
    {
        Vector2 position = GetCenter(view.transform.position);
        int neighborCount = view.Search.FindAll(position, radius, CharacterTeam.Enemy, ref neighborBuffer, view);
        float radiusSquared = radius * radius;
        Vector2 direction = Vector2.zero;
        try
        {
            for (int i = 0; i < neighborCount; i++)
            {
                if (!neighborBuffer[i].TryGetCharacter(out CharacterView neighbor)) continue;

                // Dùng cùng tâm/offset cho cả hai bên; vector chỉ ra xa hàng xóm.
                Vector2 away = position - (Vector2)GetCenter(neighbor.transform.position);
                float distanceSquared = away.sqrMagnitude;
                if (distanceSquared >= radiusSquared) continue;
                if (distanceSquared <= 0.000001f)
                {
                    direction += GetOverlapDirection(view.GetInstanceID(), neighbor.GetInstanceID());
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                float weight = 1f - distance / radius;
                direction += away / distance * weight;
            }

            // Giới hạn tổng lực để mật độ cao không làm tốc độ tăng vô hạn.
            return Vector2.ClampMagnitude(direction, 1f);
        }
        finally
        {
            // Chỉ giữ dung lượng; không giữ tham chiếu hàng xóm sau khi tính xong.
            Array.Clear(neighborBuffer, 0, neighborBuffer.Length);
        }
    }

    // Hai tâm trùng nhau không có hướng để chuẩn hóa; chọn hướng ổn định từ ID của cặp.
    // Cùng một cặp nhận hai vector đối nhau nên không cùng chạy sang một phía rồi tiếp tục dính nhau.
    private static Vector2 GetOverlapDirection(int ownId, int neighborId)
    {
        uint pairHash = unchecked((uint)Mathf.Min(ownId, neighborId) * 73856093u ^
            (uint)Mathf.Max(ownId, neighborId) * 19349663u);
        float angle = (pairHash % 360u) * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return ownId < neighborId ? direction : -direction;
    }

    #endregion
}
