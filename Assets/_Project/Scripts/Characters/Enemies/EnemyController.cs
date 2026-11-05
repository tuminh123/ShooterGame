using UnityEngine;

// * Hành vi riêng của quái: tự tìm hướng đến Player hiện tại, hoàn toàn không đọc joystick.
public sealed class EnemyController : CharacterControllerBase, ITimerListener
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

    // Recoil is movement configuration. Attack behavior and cooldown come from the skill asset/manager.
    [System.Serializable]
    public sealed class RecoilSettings
    {
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
    private CharacterTarget[] neighborBuffer = System.Array.Empty<CharacterTarget>(); // Mảng của controller, chỉ tăng khi thiếu chỗ; không mượn/trả mỗi lần tìm.
    private ITimerManager timers;
    private TimerHandle scanTimer;
    private TimerHandle recoilTimer;
    private bool scanReady; // Nhiều nhịp trễ được gộp thành một lần quét, không tính hàng xóm lặp lại vô ích.
    private bool disposed;
    private readonly float knockbackDistance;
    private readonly float knockbackDuration;
    private float previousRecoilRemaining; // Chênh lệch với kết quả TimerJob cho biết bước lùi của frame, không tự trừ thời gian.
    private Vector2 knockbackDirection; // * Hướng lùi chốt lúc trúng, không đổi theo Player trong lúc lùi.

    public EnemyController(PlayerManager players, CharacterFactory characters,
        SeparationSettings separation = null, RecoilSettings recoil = null)
    {
        this.players = players;
        this.characters = characters;
        separation = separation ?? new SeparationSettings();
        separationRadius = Mathf.Max(0.01f, separation.radius);
        separationOffsetY = separation.offsetY;
        separationStrength = Mathf.Max(0f, separation.strength);
        scanInterval = Mathf.Max(0.01f, separation.scanInterval);
        recoil = recoil ?? new RecoilSettings();
        knockbackDistance = Mathf.Max(0f, recoil.knockbackDistance);
        knockbackDuration = Mathf.Max(0.01f, recoil.knockbackDuration);
    }

    #endregion

    #region Chase & Separation Movement

    protected override void TickMovement(CharacterView view, CharacterRuntimeData runtime)
    {
        EnsureTimers(view);
        // * Chết/thu hồi là quyết định của Enemy; vỏ không chứa luật sống chết theo phe.
        if (!runtime.IsAlive)
        {
            characters.Despawn(view);
            return;
        }
        if (timers.DeltaTime <= 0f)
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
        if (separationStrength <= 0f || !scanReady) return;
        scanReady = false;
        // Search đọc snapshot hitbox và grid chung; không truy vấn collider.
        separationDirection = CalculateSeparation(view);
    }

    // Dùng lại buffer của controller; chỉ giữ dung lượng, xóa tham chiếu hàng xóm sau khi tính xong.
    // Vùng query chạm hitbox; kiểm tra lại khoảng cách giữa hai tâm để lực tách về 0 đúng bán kính.
    private Vector2 CalculateSeparation(CharacterView view)
    {
        Vector2 position = GetSeparationCenter(view.transform.position);
        int neighborCount = view.Search.FindAll(position, separationRadius, CharacterTeam.Enemy, ref neighborBuffer, view);
        float radiusSquared = separationRadius * separationRadius;
        Vector2 direction = Vector2.zero;
        try
        {
            for (int i = 0; i < neighborCount; i++)
            {
                if (!neighborBuffer[i].TryGetCharacter(out CharacterView neighbor))
                    continue;

                // Dùng cùng tâm/offset cho cả hai bên; vector chỉ ra xa hàng xóm.
                Vector2 away = position - (Vector2)GetSeparationCenter(neighbor.transform.position);
                float distanceSquared = away.sqrMagnitude;
                if (distanceSquared >= radiusSquared) continue;
                if (distanceSquared <= 0.000001f)
                {
                    direction += GetOverlapDirection(view.GetInstanceID(), neighbor.GetInstanceID());
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                float weight = 1f - distance / separationRadius;
                direction += away / distance * weight;
            }
            // Giới hạn tổng lực để mật độ cao không làm tốc độ tăng vô hạn.
            return Vector2.ClampMagnitude(direction, 1f);
        }
        finally { System.Array.Clear(neighborBuffer, 0, neighborBuffer.Length); }
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

    protected override void OnAttack(CharacterView view, CharacterRuntimeData runtime, Vector2 away)
    {
        if (knockbackDistance <= 0f || !view.isActiveAndEnabled) return;
        EnsureTimers(view);
        knockbackDirection = away.normalized;
        previousRecoilRemaining = knockbackDuration;
        timers.Restart(recoilTimer, knockbackDuration);
        separationDirection = Vector2.zero;
    }

    private bool ApplyKnockback(CharacterView view)
    {
        float remaining = timers.GetRemaining(recoilTimer);
        float stepTime = Mathf.Max(0f, previousRecoilRemaining - remaining);
        previousRecoilRemaining = remaining;
        if (remaining <= 0f && stepTime <= 0f) return false;
        // Lượt vừa Restart chờ bước TimerJob đầu tiên; không di chuyển trước khi thời gian đó được tiêu thụ.
        // * MovementManager tích phân theo deltaTime cả frame; tỷ lệ giữ bước cuối đúng quãng lùi cấu hình.
        float speed = (knockbackDistance / knockbackDuration) * (stepTime / timers.DeltaTime);
        CharacterView player = players.CurrentPlayer;
        float facing = player != null ? player.transform.position.x - view.transform.position.x : -knockbackDirection.x;
        view.Move(knockbackDirection, speed, facingDirection: facing);
        return true;
    }

    private void EnsureTimers(CharacterView view)
    {
        if (timers != null || disposed) return;
        timers = view.Timers;
        if (separationStrength > 0f)
        {
            float firstDelay = scanInterval * (unchecked((uint)view.GetInstanceID()) % 16u) / 16f;
            scanTimer = timers.RegisterPeriodic(scanInterval, this, firstDelay);
        }
        recoilTimer = timers.Register(0f, this);
    }

    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        if (!disposed && scanTimer.Equals(handle) && result.DueTickCount > 0) scanReady = true;
    }

    public override void SetPaused(bool paused)
    {
        timers?.SetPaused(scanTimer, paused);
        timers?.SetPaused(recoilTimer, paused);
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timers?.Cancel(scanTimer);
        timers?.Cancel(recoilTimer);
        System.Array.Clear(neighborBuffer, 0, neighborBuffer.Length);
        neighborBuffer = System.Array.Empty<CharacterTarget>();
    }

    #endregion

    #if UNITY_EDITOR
    #region Separation Debug

    // * CharacterView gọi khi chọn Enemy trong Scene view; dùng đúng bán kính đã chốt cho lượt spawn này.
    // Vẽ vùng tìm hàng xóm theo world XY, không nhân theo scale của vỏ hoặc visual.
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
    public override void OnDeath(CharacterView view, CharacterRuntimeData runtime) => characters.Despawn(view);

    #endregion
}
