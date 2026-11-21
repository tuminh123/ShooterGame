using UnityEngine;

/// <summary>
/// Hành vi của quái: đuổi theo Player hiện tại, tách đàn khỏi quái xung quanh, bị đẩy lùi khi đánh trúng,
/// và đi theo đường/đội hình nếu được spawn theo pattern. Không đọc joystick.
/// </summary>
/// <remarks>Override các khả năng mà chỉ quái có: <see cref="CanAttack"/>, <see cref="CompletePatternContact"/>, <see cref="DrawSeparationGizmo"/>.</remarks>
public sealed class EnemyController : CharacterControllerBase
{
    #region Separation Configuration

    // Cấu hình chung ở GameSceneInstaller > Stage (EnemySpawner.Settings); mỗi controller chỉ giữ bản sao giá trị cần dùng.
    [System.Serializable]
    public sealed class SeparationSettings
    {
        [Min(0.01f)] public float radius = 0.2f; // Khoảng cách mong muốn giữa hai tâm tránh nhau, tính bằng world unit.
        public float offsetY = -0.05f; // Cộng vào world Y của vỏ; âm dịch xuống, dương dịch lên, không dời visual/collider.
        [Min(0f)] public float strength = 2f; // Trọng số hướng ra xa hàng xóm so với hướng đuổi Player có độ dài 1.
        [Min(0.01f)] public float scanInterval = 0.1f; // Số giây game giữa hai lần quét; không quét lại trong mọi Update.
    }

    // Độ giật là cấu hình di chuyển; hành vi tấn công và cooldown lấy từ asset/manager của skill.
    [System.Serializable]
    public sealed class RecoilSettings
    {
        [Min(0f)] public float knockbackDistance = 0.35f; // Chính Enemy lùi ra xa Player sau khi gây damage thành công.
        [Min(0.01f)] public float knockbackDuration = 0.18f; // Dịch chuyển bằng MovementSystem, không dùng lực Rigidbody.
    }

    #endregion

    #region Dependencies & Per-Spawn State
    private readonly IPlayerReader players; // Tra mục tiêu hiện tại để không giữ Transform Player cũ sau khi đổi nhân vật.
    private readonly ICharacterDespawn characters; // Thu hồi visual trước vỏ qua một đầu mối, không gọi pool trực tiếp cho nhân vật.
    private readonly EnemySeparation separation; // Lực tách đàn riêng của con quái này.
    private readonly EnemyKnockback knockback = new EnemyKnockback(); // Phản lực riêng sau khi đánh trúng Player.
    private bool paused;
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
    public override bool CanAttack => !disposed;
    /// <summary>Cho biết controller còn đang thực hiện phản lực riêng.</summary>
    internal bool IsKnockedBack => knockback.IsActive;
    private bool disposed;
    private readonly float knockbackDistance;
    private readonly float knockbackDuration;

    public EnemyController(
        IPlayerReader players,
        ICharacterDespawn characters,
        SeparationSettings separation = null,
        RecoilSettings recoil = null)
    {
        this.players = players;
        this.characters = characters;
        this.separation = new EnemySeparation(separation ?? new SeparationSettings());
        recoil = recoil ?? new RecoilSettings();
        knockbackDistance = Mathf.Max(0f, recoil.knockbackDistance);
        knockbackDuration = Mathf.Max(0.01f, recoil.knockbackDuration);
    }

    #endregion

    #region Chase & Separation Movement

    /// <summary>
    /// Một bước di chuyển của quái. Đọc từ trên xuống theo thứ tự ưu tiên:
    /// chết → pause → phản lực → hết Path → không có Player → đội hình (Pattern) hoặc đuổi Player.
    /// </summary>
    protected override void TickMovement(CharacterView view, CharacterRuntimeData runtime)
    {
        // Chết/thu hồi là quyết định của Enemy; vỏ không chứa luật sống chết theo phe.
        if (!runtime.IsAlive)
        {
            characters.Despawn(view);
            return;
        }

        if (paused || Time.deltaTime <= 0f) return;

        separation.AdvanceClock(view, Time.deltaTime);

        if (TickKnockback(view)) return;

        if (pathFinished)
        {
            characters.Despawn(view);
            return;
        }

        CharacterView target = players.CurrentPlayer;
        if (!IsAliveTarget(target))
        {
            StopMoving(view);
            return;
        }

        if (formation != null) MoveInFormation(view, target);
        else ChasePlayer(view, runtime, target);
    }

    // Phản lực đang chạy có ưu tiên hơn đuổi/tách; hai lệnh không triệt tiêu nhau.
    // Vẫn hoàn tất lùi ngay cả khi đòn vừa rồi làm Player chết. Trả true nếu frame này đang lùi.
    private bool TickKnockback(CharacterView view)
    {
        if (!knockback.Apply(view, players.CurrentPlayer)) return false;
        if (remainingPath != null) pathAnchorOffset = remainingPath.AnchorOffset;
        return true;
    }

    private static bool IsAliveTarget(CharacterView target)
        => target != null && target.RuntimeData != null && target.RuntimeData.IsAlive;

    // Không còn Player để đuổi: đứng yên và bỏ lực tách cũ.
    private void StopMoving(CharacterView view)
    {
        separation.Clear();
        view.Move(Vector2.zero, 0f);
    }

    // Quái thuộc Pattern: đi theo slot của nhóm, hoặc đi nốt Path sau khi tách. Không bao giờ chuyển sang đuổi Player.
    private void MoveInFormation(CharacterView view, CharacterView target)
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
    }

    // Quái thường: đuổi Player, cộng lực tách để không chồng lên nhau trong đám đông.
    private void ChasePlayer(CharacterView view, CharacterRuntimeData runtime, CharacterView target)
    {
        separation.Update(view);
        Vector2 toPlayer = target.transform.position - view.transform.position;

        // Hướng đuổi luôn được đọc mới mỗi frame; hướng tách chỉ cập nhật khi đến lịch quét.
        // Giữ độ lớn nhỏ hơn 1 khi hai hướng triệt tiêu một phần để quái có thể chậm lại trong đám đông.
        Vector2 direction = Vector2.ClampMagnitude(
            toPlayer.normalized + separation.Direction * separation.Strength, 1f);

        // MovementSystem chuẩn hóa hướng: nhân tốc độ với độ lớn để giữ mức giảm tốc ở trên.
        // Tổng tốc độ không vượt moveSpeed; giới hạn bước đi vẫn ngăn chạy vượt khoảng cách đến Player.
        // Hướng nhìn vẫn theo Player, không bị lực tách làm flip ngược lúc quái phải lùi ra xa hàng xóm.
        view.Move(direction, runtime.FinalStats.moveSpeed * direction.magnitude, toPlayer.magnitude, toPlayer.x);
    }

    #endregion

    #region Pattern Membership & Contact

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
        separation.Clear();
    }

    /// <summary>Chốt tiếp xúc sau skill; chỉ con va chạm bị tách và nhận knockback.</summary>
    public override void CompletePatternContact(CharacterHandle spawn)
    {
        EnemyFormationRuntime group = pendingPatternContact;
        pendingPatternContact = null;
        // TryGetCharacter: vẫn đúng lượt spawn, còn sống và đang hoạt động.
        if (disposed || group == null || group.IsBroken || !ReferenceEquals(group, formation) ||
            !spawn.TryGetCharacter(out CharacterView view) || paused || Time.deltaTime <= 0f) return;
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
        CharacterHandle target, float multiplier = 1f)
        => CanAttack ? base.DealDamage(view, runtime, target, multiplier) : default;

    #endregion

    #region Detached Path Following

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
        if (knockback.Start(view, away, distance, duration)) separation.Clear();
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
        separation.Reset();
        knockback.Clear();
    }

    #endregion

    #if UNITY_EDITOR
    #region Separation Debug

    // CharacterView gọi khi chọn Enemy trong Scene view; dùng đúng bán kính đã chốt cho lượt spawn này.
    // Vẽ vùng tìm hàng xóm theo world XY, không nhân theo scale của vỏ hoặc visual.
    // Lưu ý: Chỉ hiển thị phạm vi quét, không thực hiện truy vấn Physics2D hoặc thay đổi hành vi di chuyển.
    public override void DrawSeparationGizmo(Vector3 position)
    {
        // DrawingScope tự khôi phục màu và ma trận để không ảnh hưởng gizmo của đối tượng khác.
        using (new UnityEditor.Handles.DrawingScope(Color.yellow, Matrix4x4.identity))
        {
            UnityEditor.Handles.DrawWireDisc(separation.GetCenter(position), Vector3.forward, separation.Radius);
        }
    }

    #endregion
    #endif

    #region Death Behavior

    // Boss giữ xác sau Death; enemy thường trả về pool khi Death hoàn tất.
    protected override void OnDeath(CharacterView view, CharacterRuntimeData runtime)
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
