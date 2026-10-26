using UnityEngine;

// * Hành vi riêng của quái: tự tìm hướng đến Player hiện tại, hoàn toàn không đọc joystick.
public sealed class EnemyController : ICharacterController
{
    #region Separation Configuration & Shared Query Buffer

    // * Cấu hình chung hiển thị trên EnemySpawnManager; mỗi controller chỉ giữ bản sao giá trị cần dùng.
    [System.Serializable]
    public sealed class SeparationSettings
    {
        [Min(0.01f)] public float radius = 0.3f; // * Khoảng cách mong muốn giữa hai gốc nhân vật, tính bằng world unit.
        [Min(0f)] public float strength = 2f; // * Trọng số hướng ra xa hàng xóm so với hướng đuổi Player có độ dài 1.
        [Min(0.01f)] public float scanInterval = 0.1f; // * Số giây game giữa hai lần quét; không quét lại trong mọi Update.
    }

    // * Mọi Enemy dùng chung mảng kết quả; xử lý xong lượt hiện tại trước khi Enemy tiếp theo ghi đè.
    // ! Chỉ gọi truy vấn trên main thread, không chuyển buffer này vào Job hoặc giữ kết quả qua frame.
    // ! Mảng cố định nhận tối đa 32 collider gồm cả chính nó; vùng quá đông có thể bỏ sót hàng xóm.
    private static readonly Collider2D[] neighbors = new Collider2D[32];
    private static int lastSyncFrame = -1; // * Gộp SyncTransforms cho các lượt quét trong cùng frame, không gọi lại cho từng quái.

    #endregion

    #region Dependencies & Per-Spawn State

    private readonly PlayerManager players; // * Tra mục tiêu hiện tại để không giữ Transform Player cũ sau khi đổi nhân vật.
    private readonly CharacterFactory characters; // * Thu hồi visual trước vỏ qua một đầu mối, không gọi pool trực tiếp cho nhân vật.
    private readonly ContactFilter2D neighborFilter; // * Chỉ nhận layer Enemy và chủ động cho phép collider trigger của vỏ.
    private readonly float separationRadius; // * Giá trị cấu hình chốt khi tạo controller, không sửa template hoặc dùng chung trạng thái cá thể.
    private readonly float separationStrength; // * Trọng số tách đã chốt cho cá thể; bằng 0 sẽ bỏ qua truy vấn hàng xóm.
    private readonly float scanInterval; // * Chu kỳ quét của cá thể, tối thiểu 0.01 giây dù cấu hình bị sửa ngoài Inspector.
    private Vector2 separationDirection; // * Hướng ra xa đã tính ở lượt quét trước; dùng lại giữa các lần quét.
    private float nextScanTime; // * Mốc Time.time cho lượt quét tiếp; pause không làm đồng hồ này chạy tiếp.
    private bool scanScheduled; // * Lần Tick đầu mới biết vỏ hiện tại để chia lịch; controller mới không giữ cache của lượt spawn cũ.

    public EnemyController(PlayerManager players, CharacterFactory characters, SeparationSettings separation = null)
    {
        this.players = players;
        this.characters = characters;
        separation = separation ?? new SeparationSettings();
        separationRadius = Mathf.Max(0.01f, separation.radius);
        separationStrength = Mathf.Max(0f, separation.strength);
        scanInterval = Mathf.Max(0.01f, separation.scanInterval);
        neighborFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask(GameConstants.PhysicsLayers.Enemy),
            useTriggers = true
        };
    }

    #endregion

    #region Chase & Separation Movement

    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        // * Chết/thu hồi là quyết định của Enemy; vỏ không chứa luật sống chết theo phe.
        if (!runtime.IsAlive)
        {
            characters.Despawn(view);
            return;
        }
        CharacterView target = players.CurrentPlayer;
        if (target == null || target.RuntimeData == null || !target.RuntimeData.IsAlive)
        {
            separationDirection = Vector2.zero;
            view.Move(Vector2.zero, 0f);
            return;
        }

        UpdateSeparation(view);
        Vector2 toPlayer = target.transform.position - view.transform.position;
        // * Hướng đuổi luôn được đọc mới mỗi frame; hướng tách chỉ cập nhật khi đến lịch quét.
        // * Giữ độ lớn nhỏ hơn 1 khi hai hướng triệt tiêu một phần để quái có thể chậm lại trong đám đông.
        Vector2 direction = Vector2.ClampMagnitude(
            toPlayer.normalized + separationDirection * separationStrength, 1f);
        // * MovementManager chuẩn hóa hướng: nhân tốc độ với độ lớn để giữ mức giảm tốc ở trên.
        // * Tổng tốc độ không vượt moveSpeed; giới hạn bước đi vẫn ngăn chạy vượt khoảng cách đến Player.
        // * Hướng nhìn vẫn theo Player, không bị lực tách làm flip ngược lúc quái phải lùi ra xa hàng xóm.
        view.Move(direction, runtime.FinalStats.moveSpeed * direction.magnitude, toPlayer.magnitude, toPlayer.x);
    }

    // * Chia pha theo ID vỏ thành 16 nhóm; quái spawn cùng lúc không mặc định quét cùng một frame.
    // * Giữa hai mốc chỉ giữ hướng tách, không giữ tham chiếu hàng xóm có thể đã despawn/đổi chủ.
    private void UpdateSeparation(CharacterView view)
    {
        if (separationStrength <= 0f || Time.deltaTime <= 0f)
            return;
        if (!scanScheduled)
        {
            uint phase = unchecked((uint)view.GetInstanceID()) % 16u;
            nextScanTime = Time.time + scanInterval * (phase / 16f);
            scanScheduled = true;
        }
        if (Time.time < nextScanTime)
            return;

        // * Bỏ qua các mốc đã trễ thay vì quét bù nhiều lần khi FPS giảm.
        // * Cộng theo lịch cũ để các pha không tự dồn về cùng một mốc sau các frame chậm.
        float elapsedIntervals = Mathf.Floor((Time.time - nextScanTime) / scanInterval) + 1f;
        nextScanTime += elapsedIntervals * scanInterval;

        // * MovementManager ghi Transform trong LateTick, trong khi Auto Sync Transforms đang tắt.
        // * Đồng bộ trước lượt quét đầu tiên của frame để Physics2D nhận vị trí đã di chuyển.
        if (lastSyncFrame != Time.frameCount)
        {
            Physics2D.SyncTransforms();
            lastSyncFrame = Time.frameCount;
        }
        separationDirection = CalculateSeparation(view);
    }

    // * OverlapCircle ghi vào mảng có sẵn, không cấp phát danh sách/mảng mới cho từng lượt quét.
    // * Vùng query chạm hình collider; kiểm tra lại khoảng cách giữa hai gốc để lực tách về 0 đúng bán kính.
    private Vector2 CalculateSeparation(CharacterView view)
    {
        Vector2 position = view.transform.position;
        int count = Physics2D.OverlapCircle(position, separationRadius, neighborFilter, neighbors);
        float radiusSquared = separationRadius * separationRadius;
        Vector2 direction = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D neighborCollider = neighbors[i];
            neighbors[i] = null; // * Không để mảng static giữ tham chiếu scene/collider sau khi đã xử lý.
            if (neighborCollider == null || neighborCollider.gameObject == view.gameObject ||
                !neighborCollider.TryGetComponent(out CharacterView neighbor))
                continue;
            CharacterRuntimeData neighborRuntime = neighbor.RuntimeData;
            if (neighborRuntime == null || !neighborRuntime.IsAlive || neighborRuntime.Team != CharacterTeam.Enemy)
                continue;

            // * Vị trí mình trừ vị trí hàng xóm tạo vector đi RA XA con đó, không chọn trái/phải ngẫu nhiên.
            Vector2 away = position - (Vector2)neighbor.transform.position;
            float distanceSquared = away.sqrMagnitude;
            if (distanceSquared >= radiusSquared)
                continue;
            if (distanceSquared <= 0.000001f)
            {
                direction += GetOverlapDirection(view.GetInstanceID(), neighbor.GetInstanceID());
                continue;
            }

            float distance = Mathf.Sqrt(distanceSquared);
            float weight = 1f - distance / separationRadius;
            direction += away / distance * weight;
        }
        // * Cộng ảnh hưởng hàng xóm rồi giới hạn, tránh mật độ cao tự làm lực tách tăng vô hạn.
        return Vector2.ClampMagnitude(direction, 1f);
    }

    // * Hai gốc trùng nhau không có hướng để chuẩn hóa; chọn hướng ổn định từ ID của cặp.
    // * Cùng một cặp nhận hai vector đối nhau nên không cùng chạy sang một phía rồi tiếp tục dính nhau.
    private static Vector2 GetOverlapDirection(int ownId, int neighborId)
    {
        uint pairHash = unchecked((uint)Mathf.Min(ownId, neighborId) * 73856093u ^
            (uint)Mathf.Max(ownId, neighborId) * 19349663u);
        float angle = (pairHash % 360u) * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return ownId < neighborId ? direction : -direction;
    }

    #endregion

    #region Contact Behavior

    public void OnContact(CharacterView view, CharacterRuntimeData runtime, CharacterView other)
    {
        if (other.RuntimeData != null && other.RuntimeData.Team == CharacterTeam.Player)
            characters.Despawn(view);
    }

    #endregion
}
