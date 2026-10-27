using UnityEngine;

// * Hành vi riêng của quái: tự tìm hướng đến Player hiện tại, hoàn toàn không đọc joystick.
public sealed class EnemyController : ICharacterController
{
    #region Separation Configuration

    // * Cấu hình chung hiển thị trên EnemySpawnManager; mỗi controller chỉ giữ bản sao giá trị cần dùng.
    [System.Serializable]
    public sealed class SeparationSettings
    {
        [Min(0.01f)] public float radius = 0.2f; // * Khoảng cách mong muốn giữa hai tâm tránh nhau, tính bằng world unit.
        public float offsetY = -0.05f; // * Cộng vào world Y của vỏ; âm dịch xuống, dương dịch lên, không dời visual/collider.
        [Min(0f)] public float strength = 2f; // * Trọng số hướng ra xa hàng xóm so với hướng đuổi Player có độ dài 1.
        [Min(0.01f)] public float scanInterval = 0.1f; // * Số giây game giữa hai lần quét; không quét lại trong mọi Update.
    }

    // * Luật chạm thuộc hành vi Enemy; không đưa cooldown/knockback vào data chung của Character.
    [System.Serializable]
    public sealed class ContactSettings
    {
        [Min(0.01f)] public float cooldown = 0.8f; // * Khoảng chờ riêng mỗi Enemy, không phải miễn thương toàn Player.
        [Min(0f)] public float damageMultiplier = 1f; // * Nhân với damage và crit do Character nguồn tính.
        [Min(0f)] public float knockbackDistance = 0.35f; // * Chính Enemy lùi ra xa Player sau khi gây damage thành công.
        [Min(0.01f)] public float knockbackDuration = 0.18f; // * Dịch chuyển bằng MovementManager, không dùng lực Rigidbody.
    }

    #endregion

    #region Dependencies & Per-Spawn State

    private readonly PlayerManager players; // * Tra mục tiêu hiện tại để không giữ Transform Player cũ sau khi đổi nhân vật.
    private readonly CharacterFactory characters; // * Thu hồi visual trước vỏ qua một đầu mối, không gọi pool trực tiếp cho nhân vật.
    private readonly float separationRadius; // * Giá trị cấu hình chốt khi tạo controller, không sửa template hoặc dùng chung trạng thái cá thể.
    private readonly float separationOffsetY; // * Offset tâm quét, không bị scale hoặc flip của visual ảnh hưởng.
    private readonly float separationStrength; // * Trọng số tách đã chốt cho cá thể; bằng 0 sẽ bỏ qua truy vấn hàng xóm.
    private readonly float scanInterval; // * Chu kỳ quét của cá thể, tối thiểu 0.01 giây dù cấu hình bị sửa ngoài Inspector.
    private Vector2 separationDirection; // * Hướng ra xa đã tính ở lượt quét trước; dùng lại giữa các lần quét.
    private float nextScanTime; // * Mốc Time.time cho lượt quét tiếp; pause không làm đồng hồ này chạy tiếp.
    private bool scanScheduled; // * Lần Tick đầu mới biết vỏ hiện tại để chia lịch; controller mới không giữ cache của lượt spawn cũ.
    private readonly float contactCooldown;
    private readonly float contactDamageMultiplier;
    private readonly float knockbackDistance;
    private readonly float knockbackDuration;
    private float nextContactTime; // * Cooldown riêng của lượt spawn hiện tại.
    private float knockbackRemaining; // * Thời gian game còn lại; pause không làm phản lực chạy tiếp.
    private Vector2 knockbackDirection; // * Hướng lùi chốt lúc trúng, không đổi theo Player trong lúc lùi.

    public EnemyController(PlayerManager players, CharacterFactory characters,
        SeparationSettings separation = null, ContactSettings contact = null)
    {
        this.players = players;
        this.characters = characters;
        separation = separation ?? new SeparationSettings();
        separationRadius = Mathf.Max(0.01f, separation.radius);
        separationOffsetY = separation.offsetY;
        separationStrength = Mathf.Max(0f, separation.strength);
        scanInterval = Mathf.Max(0.01f, separation.scanInterval);
        contact = contact ?? new ContactSettings();
        contactCooldown = Mathf.Max(0.01f, contact.cooldown);
        contactDamageMultiplier = Mathf.Max(0f, contact.damageMultiplier);
        knockbackDistance = Mathf.Max(0f, contact.knockbackDistance);
        knockbackDuration = Mathf.Max(0.01f, contact.knockbackDuration);
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
        if (Time.deltaTime <= 0f)
            return;
        // * Phản lực đang chạy có ưu tiên hơn đuổi/tách; hai lệnh không triệt tiêu nhau.
        // * Vẫn hoàn tất lùi ngay cả khi đòn vừa rồi làm Player chết.
        if (ApplyKnockback(view))
            return;
        CharacterView target = players.CurrentPlayer;
        if (target == null || target.RuntimeData == null || !target.RuntimeData.IsAlive)
        {
            separationDirection = Vector2.zero;
            view.Move(Vector2.zero, 0f);
            return;
        }

        if (TryContactAttack(view, runtime, target) && ApplyKnockback(view))
            return;

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

        // * CharacterSearch quản lý đồng bộ physics và buffer mượn dùng chung cho mọi kiểu tìm kiếm.
        separationDirection = CalculateSeparation(view);
    }

    // * Mượn buffer trong phạm vi using, tính xong hướng tách thì trả ngay; không giữ danh sách riêng.
    // * Vùng query chạm hình collider; kiểm tra lại khoảng cách giữa hai tâm để lực tách về 0 đúng bán kính.
    private Vector2 CalculateSeparation(CharacterView view)
    {
        Vector2 position = GetSeparationCenter(view.transform.position);
        using var neighbors = CharacterSearch.FindAll(position, separationRadius, CharacterTeam.Enemy, view);
        float radiusSquared = separationRadius * separationRadius;
        Vector2 direction = Vector2.zero;
        for (int i = 0; i < neighbors.Count; i++)
        {
            if (!neighbors[i].TryGetCharacter(out CharacterView neighbor))
                continue;

            // * Dùng cùng quy ước tâm cho cả hai bên để offset không tạo hướng tách lệch một phía.
            // * Tâm mình trừ tâm hàng xóm tạo vector đi RA XA con đó, không chọn trái/phải ngẫu nhiên.
            Vector2 away = position - (Vector2)GetSeparationCenter(neighbor.transform.position);
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

    // * Một cách tính tâm dùng chung cho truy vấn, khoảng cách và vòng debug để hình vẽ khớp logic.
    // * Offset giống nhau triệt tiêu khi trừ hai tâm; nó dời vùng query, không tự đổi khoảng cách giữa quái.
    private Vector3 GetSeparationCenter(Vector3 position)
    {
        position.y += separationOffsetY;
        return position;
    }

    // * Hai tâm trùng nhau không có hướng để chuẩn hóa; chọn hướng ổn định từ ID của cặp.
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

    #region Contact Attack & Recoil

    private bool TryContactAttack(CharacterView view, CharacterRuntimeData runtime, CharacterView target)
    {
        if (Time.time < nextContactTime || !view.Overlaps(target))
            return false;

        // * Chốt hướng trước callback nhận damage; không phụ thuộc mục tiêu sau khi xử lý chết.
        Vector2 away = view.HitPosition - target.HitPosition;
        if (away.sqrMagnitude <= 0.000001f)
            away = GetOverlapDirection(view.GetInstanceID(), target.GetInstanceID());
        nextContactTime = Time.time + contactCooldown;
        DamageResult result = target.ReceiveDamage(runtime.CreateDamage(contactDamageMultiplier));
        if (!result.Accepted || knockbackDistance <= 0f)
            return false;

        knockbackDirection = away.normalized;
        knockbackRemaining = knockbackDuration;
        separationDirection = Vector2.zero;
        return true;
    }

    private bool ApplyKnockback(CharacterView view)
    {
        if (knockbackRemaining <= 0f)
            return false;
        float stepTime = Mathf.Min(Time.deltaTime, knockbackRemaining);
        knockbackRemaining -= stepTime;
        // * MovementManager tích phân theo deltaTime cả frame; tỷ lệ giữ bước cuối đúng quãng lùi cấu hình.
        float speed = (knockbackDistance / knockbackDuration) * (stepTime / Time.deltaTime);
        CharacterView player = players.CurrentPlayer;
        float facing = player != null ? player.transform.position.x - view.transform.position.x : -knockbackDirection.x;
        view.Move(knockbackDirection, speed, facingDirection: facing);
        return true;
    }

    #endregion

    #if UNITY_EDITOR
    #region Separation Debug

    // * CharacterView gọi khi chọn Enemy trong Scene view; dùng đúng bán kính đã chốt cho lượt spawn này.
    // * Vẽ trên mặt phẳng XY theo world unit như OverlapCircle, không nhân theo scale của vỏ hoặc visual.
    // ! Chỉ hiển thị phạm vi quét, không thực hiện truy vấn Physics2D hoặc thay đổi hành vi di chuyển.
    public void DrawSeparationGizmo(Vector3 position)
    {
        // * DrawingScope tự khôi phục màu và ma trận để không ảnh hưởng gizmo của đối tượng khác.
        using (new UnityEditor.Handles.DrawingScope(Color.yellow, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(GetSeparationCenter(position), Vector3.forward, separationRadius);
    }

    #endregion
    #endif

    #region Death Behavior

    // * Chết mới thu hồi; bỏ hoàn toàn hành vi test chạm Player thì biến mất.
    public void OnDeath(CharacterView view, CharacterRuntimeData runtime) => characters.Despawn(view);

    #endregion
}
