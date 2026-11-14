using UnityEngine;

// * Hành vi riêng của quái: tự tìm hướng đến Player hiện tại, hoàn toàn không đọc joystick.
public sealed class EnemyController : CharacterControllerBase
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
    private float scanRemaining, recoilRemaining;
    private bool timingInitialized, paused;
    private EnemyFormationRuntime formation; // Chủ sở hữu nhóm, giữ cả khi con đã tách chuyển động.
    private EnemyFormationRuntime pendingPatternContact;
    private int formationMemberIndex;
    private bool patternTouching;
    private float patternKnockbackDistance, patternKnockbackDuration;
    private EnemyPathRoute remainingPath;
    private float pathTravelled, pathSpeed;
    private int pathPasses;
    private Vector2 pathAnchorOffset;
    private bool pathFinished;
    // Va chạm không gây choáng hoặc khóa skill; cooldown vẫn do skill quản lý.
    public bool CanAttack => !disposed;
    /// <summary>Cho biết controller còn đang thực hiện phản lực riêng.</summary>
    internal bool IsKnockedBack => recoilRemaining > 0f;
    private bool scanReady; // Nhiều nhịp trễ được gộp thành một lần quét, không tính hàng xóm lặp lại vô ích.
    private bool disposed;
    private readonly float knockbackDistance;
    private readonly float knockbackDuration;
    private float activeKnockbackDistance;
    private float activeKnockbackDuration;
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
        // * Chết/thu hồi là quyết định của Enemy; vỏ không chứa luật sống chết theo phe.
        if (!runtime.IsAlive)
        {
            characters.Despawn(view);
            return;
        }
        if (paused || Time.deltaTime <= 0f)
        {
            return;
        }
        AdvanceScanClock(view, Time.deltaTime);
        // * Phản lực đang chạy có ưu tiên hơn đuổi/tách; hai lệnh không triệt tiêu nhau.
        // * Vẫn hoàn tất lùi ngay cả khi đòn vừa rồi làm Player chết.
        if (ApplyKnockback(view))
        {
            if (remainingPath != null) pathAnchorOffset = remainingPath.AnchorOffset;
            return;
        }
        if (pathFinished)
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

        if (formation != null)
        {
            // Cho skill tiếp xúc xử lý damage trước khi bắt đầu phản lực cá thể.
            if (CharacterSearch.Overlaps(view, target)) pendingPatternContact = formation;
            else patternTouching = false;
            if (remainingPath != null)
            {
                MoveRemainingPath(view);
                return;
            }
            if (formation.TryGetTarget(formationMemberIndex, out Vector2 destination))
            {
                Vector2 delta = destination - (Vector2)view.transform.position;
                view.Move(delta, delta.magnitude / Time.deltaTime, delta.magnitude);
            }
            else view.Move(Vector2.zero, 0f);
            return; // Pattern enemies never fall back to chasing.
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

    #region Thành viên Pattern và tiếp xúc

    /// <summary>Nhận nhóm sở hữu, chỉ số thành viên và cấu hình phản lực cá thể.</summary>
    internal void JoinFormation(EnemyFormationRuntime group, int memberIndex, float recoilDistance, float recoilDuration)
    {
        formation?.Remove(this);
        formation = group;
        formationMemberIndex = memberIndex;
        patternKnockbackDistance = recoilDistance;
        patternKnockbackDuration = recoilDuration;
        patternTouching = false;
        remainingPath = null;
        pathFinished = false;
        separationDirection = Vector2.zero;
    }

    /// <summary>Chốt tiếp xúc sau skill; chỉ con va chạm bị tách và nhận knockback.</summary>
    internal void CompletePatternContact(CharacterView view, CharacterRuntimeData identity, ulong generation)
    {
        EnemyFormationRuntime group = pendingPatternContact;
        pendingPatternContact = null;
        if (disposed || group == null || group.IsBroken || !ReferenceEquals(group, formation) ||
            view == null || !view.isActiveAndEnabled || !ReferenceEquals(view.RuntimeData, identity) ||
            identity.Generation != generation || !identity.IsAlive || paused || Time.deltaTime <= 0f) return;
        CharacterView player = players.CurrentPlayer;
        if (player == null || player.RuntimeData == null || !player.RuntimeData.IsAlive) return;
        bool touching = CharacterSearch.Overlaps(view, player);
        bool newContact = touching && !patternTouching;
        patternTouching = touching;
        if (!newContact) return;
        group.DetachMember(formationMemberIndex);
        StartKnockback(view, (Vector2)view.HitPosition - (Vector2)player.HitPosition,
            patternKnockbackDistance, patternKnockbackDuration);
    }

    // Controller đã dispose không tiếp tục gửi damage qua callback cũ.
    public override DamageResult DealDamage(CharacterView view, CharacterRuntimeData runtime,
        CharacterTarget target, float multiplier = 1f)
        => CanAttack ? base.DealDamage(view, runtime, target, multiplier) : default;

    #endregion

    #region Path độc lập sau khi tách đội hình

    /// <summary>Giữ Path còn lại và tốc độ nhóm; không nhận lại slot hoặc chuyển sang chase.</summary>
    internal void FollowRemainingPath(EnemyPathRoute path, float travelled, int passes, float speed)
    {
        remainingPath = path;
        pathTravelled = travelled;
        pathPasses = passes;
        pathSpeed = speed;
        pathAnchorOffset = path.AnchorOffset;
        pathFinished = false;
    }

    /// <summary>Di chuyển qua các góc còn lại bằng tốc độ đã nhận; thu hồi sau khi tới đích.</summary>
    private void MoveRemainingPath(CharacterView view)
    {
        Vector2 position = view.transform.position;
        Vector2 destination = position + remainingPath.AnchorOffset - pathAnchorOffset;
        pathAnchorOffset = remainingPath.AnchorOffset;
        float budget = pathSpeed * Time.deltaTime;
        float end = remainingPath.Length * pathPasses;
        while (budget > 0f && !pathFinished)
        {
            float corner = remainingPath.NextCornerDistance(pathTravelled, pathPasses);
            Vector2 point = remainingPath.Sample(corner, pathPasses);
            float distance = Vector2.Distance(destination, point);
            destination = Vector2.MoveTowards(destination, point, budget);
            if (distance > budget) break;
            budget -= distance;
            pathTravelled = corner;
            pathFinished = pathTravelled >= end;
        }
        Vector2 delta = destination - position;
        view.Move(delta, delta.magnitude / Time.deltaTime, delta.magnitude);
    }

    /// <summary>Bù dịch camera cuối frame cho con tách nhóm; bỏ bù khi đang knockback.</summary>
    internal bool ApplyPathCameraMotion(CharacterView view)
    {
        if (remainingPath == null || disposed) return false;
        Vector2 delta = remainingPath.AnchorOffset - pathAnchorOffset;
        pathAnchorOffset = remainingPath.AnchorOffset;
        if (IsKnockedBack || delta.sqrMagnitude <= 0f) return false;
        view.transform.position += new Vector3(delta.x, delta.y, 0f);
        return true;
    }

    #endregion

    #region Separation Queries

    // Chia pha quét bằng bộ đếm riêng của cá thể; giữa hai lượt dùng lại hướng đã tính.
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
        if (formation != null)
        {
            // Pattern chốt tiếp xúc sau skill, dùng chung StartKnockback với enemy thường.
            return;
        }
        StartKnockback(view, away, knockbackDistance, knockbackDuration);
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
        knockbackDirection = away.normalized;
        recoilRemaining = activeKnockbackDuration;
        separationDirection = Vector2.zero;
    }

    private bool ApplyKnockback(CharacterView view)
    {
        if (recoilRemaining <= 0f)
        {
            return false;
        }
        float stepTime = Mathf.Min(Time.deltaTime, recoilRemaining);
        recoilRemaining = Mathf.Max(0f, recoilRemaining - stepTime);
        // Bước cuối chỉ dùng phần duration còn lại, tránh lùi quá quãng đường cấu hình.
        float speed = (activeKnockbackDistance / activeKnockbackDuration) * (stepTime / Time.deltaTime);
        CharacterView player = players.CurrentPlayer;
        float facing = player != null ? player.transform.position.x - view.transform.position.x : -knockbackDirection.x;
        view.Move(knockbackDirection, speed, facingDirection: facing);
        return true;
    }

    // Mỗi controller giữ pha quét riêng; frame dài chỉ quét hàng xóm một lần.
    private void AdvanceScanClock(CharacterView view, float deltaTime)
    {
        if (!timingInitialized)
        {
            timingInitialized = true;
            scanRemaining = scanInterval * (unchecked((uint)view.GetInstanceID()) % 16u) / 16f;
            return;
        }
        if (separationStrength <= 0f) return;
        scanRemaining -= deltaTime;
        if (scanRemaining <= 0f)
        {
            scanReady = true;
            scanRemaining += (Mathf.Floor(-scanRemaining / scanInterval) + 1f) * scanInterval;
        }
    }

    public override void SetPaused(bool paused)
    {
        this.paused = paused;
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
        pendingPatternContact = null;
        remainingPath = null;
        base.Dispose();
        scanRemaining = recoilRemaining = 0f;
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

    // Boss giữ xác sau Death; enemy thường trả về pool khi Death hoàn tất.
    public override void OnDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        if (view.IsBoss)
        {
            // Boss giữ lại trong scene ở frame cuối của animation chết.
            view.WaitForDeathAnimation(null);
            return;
        }

        if (!view.WaitForDeathAnimation(() => characters.Despawn(view)))
        {
            characters.Despawn(view); // Không có visual đang hiển thị thì thu hồi ngay.
        }
    }

    #endregion
}
