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
    private TimerHandle stunTimer; // Dùng cùng đồng hồ gameplay để choáng dừng theo pause/slow motion.
    private EnemyFormationRuntime formation; // Null nghĩa là dùng chase/separation thông thường.
    private int formationSlot;
    // CharacterView/skill runtime đọc cờ này; quái vẫn có thể nhận sát thương khi đang choáng.
    public bool CanAttack => !disposed && (timers == null || timers.GetRemaining(stunTimer) <= 0f);
    private bool scanReady; // Nhiều nhịp trễ được gộp thành một lần quét, không tính hàng xóm lặp lại vô ích.
    private bool disposed;
    private readonly float knockbackDistance;
    private readonly float knockbackDuration;
    private float activeKnockbackDistance;
    private float activeKnockbackDuration;
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
        {
            return;
        }
        // * Phản lực đang chạy có ưu tiên hơn đuổi/tách; hai lệnh không triệt tiêu nhau.
        // * Vẫn hoàn tất lùi ngay cả khi đòn vừa rồi làm Player chết.
        if (ApplyKnockback(view))
        {
            return;
        }
        CharacterView target = players.CurrentPlayer;
        if (target == null || target.RuntimeData == null || !target.RuntimeData.IsAlive)
        {
            separationDirection = Vector2.zero;
            view.Move(Vector2.zero, 0f);
            return;
        }

        // Kiểm tra hitbox trực tiếp, không chờ cooldown gây damage mới phá nhóm khi chạm player.
        if (formation != null && !formation.IsBroken && CharacterSearch.Overlaps(view, target))
        {
            TryBreakFormation(view, (Vector2)(view.HitPosition - target.HitPosition));
            view.Move(Vector2.zero, 0f);
            return;
        }
        // Knockback đã được ưu tiên ở trên. Hết phản lực mà còn choáng thì đứng yên trước khi chase.
        if (timers.GetRemaining(stunTimer) > 0f)
        {
            view.Move(Vector2.zero, 0f);
            return;
        }
        // Nhóm quyết định vị trí chung. Không cho mỗi enemy tự chạy đuổi slot bằng tốc độ riêng,
        // vì như vậy khối sẽ bị kéo giãn. Lệnh dưới đưa mọi thành viên tới đúng vị trí của bước này.
        if (formation != null && formation.TryGetTarget(formationSlot, out Vector2 destination))
        {
            Vector2 delta = destination - (Vector2)view.transform.position;
            float speed = delta.magnitude / timers.DeltaTime;
            view.Move(delta, speed, delta.magnitude);
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

    #endregion

    #region Formation Membership And Combat Gate

    // Khi chuyển sang nhóm khác, vô hiệu slot cũ nhưng không thay controller hoặc thuê lại runtime.
    internal void JoinFormation(EnemyFormationRuntime group, int slot)
    {
        formation?.Remove(this);
        formation = group;
        formationSlot = slot;
        separationDirection = Vector2.zero;
    }

    // Chỉ nhóm đang sở hữu mới được trả thành viên về chase; cleanup dùng stunDuration = 0.
    internal void LeaveFormation(EnemyFormationRuntime group, CharacterView view, float stunDuration)
    {
        if (!ReferenceEquals(formation, group))
        {
            return;
        }
        formation = null;
        separationDirection = Vector2.zero;
        if (stunDuration <= 0f || disposed)
        {
            return;
        }
        EnsureTimers(view);
        timers.Restart(stunTimer, stunDuration);
        view.Move(Vector2.zero, 0f);
    }

    // Runtime gọi cho mọi thành viên khi va player: văng trước, rồi đứng choáng đủ thời lượng.
    internal void BreakFormation(EnemyFormationRuntime group, CharacterView view, Vector2 away,
        float distance, float duration, float stunDuration)
    {
        if (!ReferenceEquals(formation, group) || disposed)
        {
            return;
        }

        LeaveFormation(group, view, 0f);
        EnsureTimers(view);
        float recoilTime = distance > 0f && view.isActiveAndEnabled ? Mathf.Max(0.01f, duration) : 0f;

        // Timer choáng bao gồm thời gian phản lực để countdown không làm mất phần choáng sau khi văng.
        // TickMovement ưu tiên phản lực; combat bị khóa trong cả hai giai đoạn.
        timers.Restart(stunTimer, recoilTime + stunDuration);
        StartKnockback(view, away, distance, duration);
        view.Move(Vector2.zero, 0f);
    }

    private bool TryBreakFormation(CharacterView view, Vector2 away)
    {
        EnemyFormationRuntime group = formation;
        CharacterView player = players.CurrentPlayer;
        Vector2 playerPosition = player != null ? (Vector2)player.HitPosition : (Vector2)view.HitPosition - away;
        return group != null && group.OnPlayerContact(this, playerPosition);
    }

    // Chặn damage ngay tại cửa combat, kể cả khi có skill cố đánh trong thời gian bị khóa.
    public override DamageResult DealDamage(CharacterView view, CharacterRuntimeData runtime,
        CharacterTarget target, float multiplier = 1f)
        => CanAttack ? base.DealDamage(view, runtime, target, multiplier) : default;

    #endregion

    #region Separation Queries

    // Chia pha quét theo timer của cá thể; giữa hai lượt dùng lại hướng đã tính.
    private void UpdateSeparation(CharacterView view)
    {
        if (separationStrength <= 0f || !scanReady)
        {
            return;
        }
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
                {
                    continue;
                }

                // Dùng cùng tâm/offset cho cả hai bên; vector chỉ ra xa hàng xóm.
                Vector2 away = position - (Vector2)GetSeparationCenter(neighbor.transform.position);
                float distanceSquared = away.sqrMagnitude;
                if (distanceSquared >= radiusSquared)
                {
                    continue;
                }
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
        finally
        {
            System.Array.Clear(neighborBuffer, 0, neighborBuffer.Length);
        }
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
        // Đường dự phòng khi đòn đánh thành công trước kiểm tra va chạm của TickMovement.
        if (!TryBreakFormation(view, away))
        {
            StartKnockback(view, away, knockbackDistance, knockbackDuration);
        }
    }

    // Chỉ gửi lệnh di chuyển cho view enemy này; không tác động controller hoặc Transform của player.
    private void StartKnockback(CharacterView view, Vector2 away, float distance, float duration)
    {
        if (distance <= 0f || !view.isActiveAndEnabled)
        {
            return;
        }

        activeKnockbackDistance = distance;
        activeKnockbackDuration = Mathf.Max(0.01f, duration);
        if (away.sqrMagnitude < 0.000001f)
        {
            away = Vector2.right;
        }
        EnsureTimers(view);
        knockbackDirection = away.normalized;
        previousRecoilRemaining = activeKnockbackDuration;
        timers.Restart(recoilTimer, activeKnockbackDuration);
        separationDirection = Vector2.zero;
    }

    private bool ApplyKnockback(CharacterView view)
    {
        float remaining = timers.GetRemaining(recoilTimer);
        float stepTime = Mathf.Max(0f, previousRecoilRemaining - remaining);
        previousRecoilRemaining = remaining;
        if (remaining <= 0f && stepTime <= 0f)
        {
            return false;
        }
        // Lượt vừa Restart chờ bước TimerJob đầu tiên; không di chuyển trước khi thời gian đó được tiêu thụ.
        // * MovementManager tích phân theo deltaTime cả frame; tỷ lệ giữ bước cuối đúng quãng lùi cấu hình.
        float speed = (activeKnockbackDistance / activeKnockbackDuration) * (stepTime / timers.DeltaTime);
        CharacterView player = players.CurrentPlayer;
        float facing = player != null ? player.transform.position.x - view.transform.position.x : -knockbackDirection.x;
        view.Move(knockbackDirection, speed, facingDirection: facing);
        return true;
    }

    private void EnsureTimers(CharacterView view)
    {
        if (timers != null || disposed)
        {
            return;
        }
        timers = view.Timers;
        if (separationStrength > 0f)
        {
            float firstDelay = scanInterval * (unchecked((uint)view.GetInstanceID()) % 16u) / 16f;
            scanTimer = timers.RegisterPeriodic(scanInterval, this, firstDelay);
        }
        recoilTimer = timers.Register(0f, this);
        stunTimer = timers.Register(0f, this);
    }

    public void OnTimer(TimerHandle handle, TimerResult result)
    {
        if (!disposed && scanTimer.Equals(handle) && result.DueTickCount > 0)
        {
            scanReady = true;
        }
    }

    public override void SetPaused(bool paused)
    {
        timers?.SetPaused(scanTimer, paused);
        timers?.SetPaused(recoilTimer, paused);
        timers?.SetPaused(stunTimer, paused);
    }

    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        // Bỏ slot trước khi vỏ/runtime quay về pool để nhóm cũ không giữ quyền điều khiển.
        formation?.Remove(this);
        formation = null;
        base.Dispose();
        timers?.Cancel(scanTimer);
        timers?.Cancel(recoilTimer);
        timers?.Cancel(stunTimer);
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
        {
            UnityEditor.Handles.DrawWireDisc(GetSeparationCenter(position), Vector3.forward, separationRadius);
        }
    }

    #endregion
    #endif

    #region Death Behavior

    // Cùng animation Death với Player; controller chỉ quyết định lúc thu hồi quái.
    public override void OnDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        if (!view.WaitForDeathAnimation(() => characters.Despawn(view)))
        {
            characters.Despawn(view); // Không có visual đang hiển thị thì thu hồi ngay.
        }
    }

    #endregion
}
