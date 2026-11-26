using System;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Vỏ Unity dùng chung cho mọi nhân vật (Player, quái, boss), được pool tái sử dụng cho nhiều lượt spawn.
/// Vai trò của vỏ là <b>điều phối vòng đời</b> của một lượt spawn: đăng ký di chuyển/hitbox, tick controller,
/// chuyển tiếp sát thương cho controller, và chuỗi chết/thu hồi.
/// </summary>
/// <remarks>
/// Phần việc được tách ra (composition), mỗi phần đọc được độc lập:
/// <list type="bullet">
/// <item><see cref="CharacterPresentation"/>: hướng quay, visual state, bật/tắt visual, bounds.</item>
/// <item><see cref="CharacterFeedback"/>: nháy trắng khi trúng đòn, màu trạng thái.</item>
/// <item><see cref="ICharacterController"/>: input hoặc AI, nhận/gây sát thương (strategy Player/Enemy).</item>
/// </list>
/// Vỏ không biết Combat tồn tại: Combat tự quản lý skill/effect của nhân vật bằng cách nghe
/// <see cref="Activated"/>/<see cref="Deactivated"/>/<see cref="Died"/>/<see cref="Released"/> (xem CharacterSkillSystem).
/// Không đổi tên class hoặc các field <c>[SerializeField]</c>: prefab vỏ tham chiếu script qua GUID và lưu giá trị theo tên field.
/// </remarks>
public sealed class CharacterView : MonoBehaviour
{
    #region Feedback Settings (giá trị chỉnh trên prefab Character)

    // Giữ ở đây với đúng tên cũ để không mất giá trị đã chỉnh trên prefab; CharacterFeedback nhận qua settings.
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.12f;
    [SerializeField, Range(0f, 1f)] private float hitWhiteBlend = 0.22f;
    [SerializeField, Range(0f, 1f)] private float statusColorBlend = 0.55f;
    [SerializeField] private Color poisonColor = new Color(0.65f, 0.35f, 1f, 1f);
    [SerializeField] private Color burnColor = new Color(1f, 0.45f, 0.15f, 1f);
    [SerializeField] private Color slowColor = new Color(0.35f, 0.8f, 1f, 1f);
    [Tooltip("VFX lặp bám trên nhân vật khi bị độc. Prefab cần ParticleProjectileVisual trên gốc; trống = chỉ đổi màu.")]
    [SerializeField] private GameObject poisonVfx;
    [Tooltip("VFX lặp bám trên nhân vật khi bị cháy. Prefab cần ParticleProjectileVisual trên gốc; trống = chỉ đổi màu.")]
    [SerializeField] private GameObject burnVfx;
    [Tooltip("VFX lặp bám trên nhân vật khi bị làm chậm. Prefab cần ParticleProjectileVisual trên gốc; trống = chỉ đổi màu.")]
    [SerializeField] private GameObject slowVfx;

    #endregion

    #region Services (từ CharacterContext, dùng chung cho mọi lượt spawn)

    private IMovementRegistration movementRegistration;
    private IMovementCommand movementCommand;
    private ISpatialRegistration spatial;
    private IGameObjectPool pools; // Thuê/trả visual con.
    private CharacterRuntimePool runtimePool; // Trả runtime về pool khi lượt spawn kết thúc.
    private DamageTextSystem damageText; // Số sát thương bay lên.

    #endregion

    #region Spawn State (reset mỗi lượt spawn)

    private ICharacterController controller; // Hành vi gắn lúc spawn, không serialize vào prefab.
    private GameObject visual; // Visual con thuê từ pool theo prefab trong CharacterDataSO.
    private CharacterVisualHandle visualHandle;
    private HitboxData hitbox; // Hitbox local; không cần Collider2D trên vỏ.
    private MovementHandle movementHandle; // Đăng ký di chuyển của lần bật hiện tại; vô hiệu ngay khi tắt vỏ.
    private CharacterFeedback feedback;
    private CharacterPresentation presentation;

    #endregion

    #region Public State

    /// <summary>Runtime của lượt spawn đang dùng vỏ; null khi vỏ đang rảnh trong pool.</summary>
    public CharacterRuntimeData RuntimeData { get; private set; }

    /// <summary>Đăng ký hitbox của lần bật hiện tại trong lưới không gian.</summary>
    public SpatialHandle SpatialHandle { get; private set; }

    /// <summary>Tìm mục tiêu quanh nhân vật; skill (Combat) và AI tách đàn dùng qua đây.</summary>
    public ICharacterSearch Search { get; private set; }

    /// <summary>Phần trình bày: visual state, hướng quay, hiển thị. Tạo lazy vì component khác có thể cần trước Awake của vỏ.</summary>
    public CharacterPresentation Presentation => presentation ??= new CharacterPresentation(this, Feedback);

    /// <summary>
    /// Nhân vật có được dùng skill lúc này không. Trình diễn và hitbox vẫn hoạt động khi quái bị khóa đòn;
    /// đang phát kết quả thắng/thua thì không dùng skill nữa.
    /// </summary>
    public bool CanUseSkills => !Presentation.IsResultLocked && (controller == null || controller.CanAttack);

    /// <summary>Lượt spawn là boss (hiện thanh máu, có animation Attack/Cast, giữ xác khi chết).</summary>
    public bool IsBoss { get; private set; }

    /// <summary>Quái tinh anh trong đợt; spawn quái đặt sau khi tạo.</summary>
    public bool IsElite { get; internal set; }

    /// <summary>Tâm hitbox trong world, dùng làm vị trí ngắm và tìm mục tiêu.</summary>
    public Vector3 HitPosition => transform.TransformPoint(hitbox.Offset);

    private CharacterFeedback Feedback => feedback ??= new CharacterFeedback(this);

    #endregion

    #region Lifecycle Events

    /// <summary>Đòn sát thương vừa được nhận (kể cả chỉ mất giáp).</summary>
    public event Action<DamageInfo, DamageResult> DamageReceived;

    /// <summary>Vỏ vừa được bật khi nhân vật còn sống. Combat dùng để tiếp tục skill và effect sau một lần tạm tắt.</summary>
    /// <remarks>
    /// Lần bật đầu tiên của lượt spawn cũng phát, nhưng lúc đó Combat chưa nhận nhân vật (CharacterAdded của registry
    /// phát sau khi vỏ đã bật) nên bỏ qua; skill được tạo ở trạng thái đang chạy.
    /// </remarks>
    public event Action Activated;

    /// <summary>
    /// Vỏ bị tắt tạm, hoặc sắp bị thu hồi. Phát trước khi visual bị dọn, để bên sở hữu dừng skill, effect
    /// và các tác vụ trình diễn.
    /// </summary>
    public event Action Deactivated;

    /// <summary>Lượt spawn vừa chết. Vỏ vẫn còn để phát animation chết cho tới khi bị thu hồi.</summary>
    public event Action<CharacterView> Died;

    /// <summary>Lượt spawn kết thúc hẳn (bị thu hồi hoặc Destroy). Tắt tạm thời không phát event này.</summary>
    public event Action<CharacterView> Released;

    #endregion

    #region Spawn Initialization

    /// <summary>
    /// Chuẩn bị một lượt spawn. Factory gọi khi vỏ còn nằm dưới nhánh Preparing đang tắt,
    /// nên <c>OnEnable</c> chưa chạy hành vi nào cho tới khi pool bật vỏ.
    /// </summary>
    /// <remarks>
    /// Dịch vụ được nhận ở đây chứ không qua Zenject inject: vỏ không phụ thuộc container,
    /// và các system nội bộ của miền Character không phải bind công khai (xem <see cref="CharacterContext"/>).
    /// </remarks>
    public void Initialize(
        CharacterContext context,
        CharacterDataSO template,
        CharacterRuntimeData runtime,
        ICharacterController behavior)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        // Gán dịch vụ trước mọi kiểm tra: nếu khởi tạo lỗi, factory gọi ClearSpawn và ClearSpawn cần pool.
        BindServices(context);
        ValidateSpawn(template, runtime, behavior);
        ApplyTeamLayer(template.Team);

        CharacterVisualSettings visualSettings = template.VisualSettings;
        visualSettings.Validate();
        hitbox = visualSettings.Hitbox;

        RuntimeData = runtime;
        IsBoss = template.IsBoss;
        IsElite = false;
        controller = behavior;

        AttachVisual(template, visualSettings);

        // Skill vốn có không được tạo ở đây: Combat tạo khi nghe CharacterAdded, ngay trong lời gọi Spawn.
        // Quái bắt đầu ẩn; EnemyVisibility sẽ bật nếu visual giao khung hình trước khi render.
        visual.SetActive(runtime.Team != CharacterTeam.Enemy);
    }

    // Mọi vỏ dùng chung một context, nên gán lại ở mỗi lượt spawn là vô hại.
    private void BindServices(CharacterContext context)
    {
        movementRegistration = context.MovementRegistration;
        movementCommand = context.MovementCommand;
        spatial = context.SpatialRegistration;
        pools = context.Pools;
        runtimePool = context.Runtimes;
        damageText = context.DamageText;
        Search = context.Search;
    }

    // Chỉ nhận vỏ đã thu hồi và đang tắt, cùng dữ liệu khớp nhau giữa template và runtime.
    private void ValidateSpawn(CharacterDataSO template, CharacterRuntimeData runtime, ICharacterController behavior)
    {
        if (gameObject.activeInHierarchy || RuntimeData != null)
            throw new InvalidOperationException("Initialize a released, inactive character shell.");
        if (template == null || runtime == null || behavior == null || template.VisualPrefab == null)
            throw new ArgumentException("Character needs data, runtime, behavior and a visual prefab.");
        if (template.CharacterType != runtime.CharacterType || template.Team != runtime.Team)
            throw new ArgumentException("Character template and runtime must match.");
    }

    // Giữ layer của vỏ theo phe cho các bộ lọc Unity khác; tìm kiếm gameplay dùng Team trong dữ liệu native.
    private void ApplyTeamLayer(CharacterTeam team)
    {
        string layerName = team == CharacterTeam.Enemy
            ? GameConstants.PhysicsLayers.Enemy
            : GameConstants.PhysicsLayers.Player;
        int characterLayer = LayerMask.NameToLayer(layerName);
        if (characterLayer < 0)
            throw new InvalidOperationException($"Add the {layerName} layer in Project Settings.");
        gameObject.layer = characterLayer;
    }

    // Thuê visual của loại nhân vật, áp layout và nối phần hiển thị (animation, màu trúng đòn).
    // Vỏ vẫn đang tắt nên visual được chuẩn bị xong trước khi xuất hiện trong scene.
    private void AttachVisual(CharacterDataSO template, CharacterVisualSettings visualSettings)
    {
        visual = pools.Spawn(template.VisualPrefab, transform.position, transform.rotation, PoolGroup.Other, transform);
        if (visual == null)
            throw new InvalidOperationException("Could not spawn the character visual.");
        visualSettings.ApplyVisual(visual.transform);
        // Áp layout mỗi lượt spawn để vỏ pool không giữ kích thước thanh máu của loại boss trước.
        if (TryGetComponent<CharacterUI>(out var characterUI)) characterUI.ApplyLayout(visualSettings);

        SpriteRenderer[] sprites = visual.GetComponentsInChildren<SpriteRenderer>(true);
        Presentation.Begin(visual, sprites); // Trước khi visual handle bind, vì handle đọc trạng thái ban đầu.
        visualHandle = visual.GetComponent<CharacterVisualHandle>();
        visualHandle?.CaptureFacingScale();
        visualHandle?.Bind(Presentation);
        Feedback.Bind(new CharacterFeedbackSettings(hitFlashDuration, hitWhiteBlend, statusColorBlend,
            poisonColor, burnColor, slowColor, poisonVfx, burnVfx, slowVfx), sprites, controller, pools);
    }

    #endregion

    #region Activation And Frame

    private void OnEnable()
    {
        if (RuntimeData == null) return; // Vỏ prewarm chưa có lượt spawn nào.
        if (!RuntimeData.IsAlive) return; // Bật lại xác đang phát animation chết: không đăng ký lại movement/hitbox/AI.
        movementHandle = movementRegistration.Register(transform);
        SpatialHandle = spatial.Register(this);
        controller?.SetPaused(false);
        Feedback.Apply();
        Activated?.Invoke(); // Combat tiếp tục skill và effect của nhân vật.
    }

    // Unity đang đổi trạng thái cả hierarchy trong OnDisable: chỉ dừng movement, không thu hồi/reparent visual.
    // Giữ data và hành vi để tắt rồi bật lại vẫn tiếp tục đúng lượt spawn.
    private void OnDisable()
    {
        Feedback.StopFlash();
        StopMovement();
        StopSpatial();
        controller?.SetPaused(true);
        Deactivated?.Invoke(); // Combat dừng skill và effect của nhân vật, giữ nguyên cooldown.
    }

    /// <summary>CharacterRegistry gọi một lần mỗi frame (không có Update riêng trên từng vỏ pool).</summary>
    internal void TickFrame(float deltaTime)
    {
        if (RuntimeData == null || Presentation.IsResultLocked || !isActiveAndEnabled) return;
        Feedback.Tick(deltaTime);

        // Skill vốn có đã được Combat tick trước đó trong cùng frame, nên va chạm đội hình bên dưới
        // vẫn thấy sát thương mà skill vừa gây.
        ICharacterController behavior = controller;
        var spawn = new CharacterHandle(this);
        behavior.Tick(this, RuntimeData);
        // Controller tự bỏ qua nếu lượt spawn đã đổi (chết hoặc bị thu hồi trong Tick).
        behavior.CompletePatternContact(spawn);
    }

    /// <summary>
    /// CharacterRegistry gọi ở LateTick: kiểm tra animation của visual và cho VFX trạng thái bám theo vị trí mới.
    /// Không chặn khi đã khóa kết quả hay đã chết, vì animation Death/Win/Lose phải chạy hết để báo hoàn tất cho gameplay.
    /// </summary>
    internal void LateTickFrame()
    {
        visualHandle?.LateTick();
        Feedback.LateTick();
    }

    #endregion

    #region Movement

    /// <summary>
    /// Gửi lệnh di chuyển của frame (controller gọi). Mặc định quay mặt theo hướng chạy; quái có thể truyền
    /// <paramref name="facingDirection"/> để nhìn về Player khi đang lùi khỏi hàng xóm.
    /// </summary>
    public void Move(Vector2 direction, float speed, float maxDistance = float.PositiveInfinity, float? facingDirection = null)
    {
        if (Presentation.IsResultLocked) return;
        Presentation.Face(facingDirection ?? direction.x);
        movementCommand?.SetMovement(movementHandle, direction, speed, maxDistance);
        Presentation.UpdateLocomotion(direction, speed, maxDistance);
    }

    private void StopMovement()
    {
        movementRegistration?.Unregister(movementHandle);
        movementHandle = default;
    }

    private void StopSpatial()
    {
        spatial?.Unregister(SpatialHandle);
        SpatialHandle = default;
    }

    /// <summary>Chụp hình hộp world XY cho job không gian. Hỗ trợ xoay Z và scale; không dùng hierarchy gây shear.</summary>
    internal SpatialEntity GetSpatialEntity()
    {
        Vector3 center = HitPosition;
        Vector3 scale = transform.lossyScale;
        float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
        return new SpatialEntity
        {
            Center = new float2(center.x, center.y),
            HalfSize = new float2(hitbox.Size.x * Mathf.Abs(scale.x), hitbox.Size.y * Mathf.Abs(scale.y)) * 0.5f,
            AxisX = new float2(Mathf.Cos(angle), Mathf.Sin(angle)),
            AxisY = new float2(-Mathf.Sin(angle), Mathf.Cos(angle))
        };
    }

    #endregion

    #region Combat API (chuyển tiếp cho controller)

    /// <summary>Nhận một đòn. Controller quyết định giáp, HP và chết; đang phát kết quả thì bỏ qua.</summary>
    public DamageResult ReceiveDamage(DamageInfo damage)
    {
        if (Presentation.IsResultLocked) return default;
        return controller != null ? controller.ReceiveDamage(this, RuntimeData, damage) : default;
    }

    /// <summary>Cộng/trừ HP trực tiếp, bỏ qua giáp.</summary>
    public void ChangeHealth(float amount) { if (!Presentation.IsResultLocked) controller?.ChangeHealth(this, RuntimeData, amount); }

    /// <summary>Cấp thêm giáp.</summary>
    public void GrantShield(float amount) { if (!Presentation.IsResultLocked) controller?.GrantShield(this, RuntimeData, amount); }

    /// <summary>Gây sát thương lên đúng lượt spawn của <paramref name="target"/>, tính từ chỉ số của nhân vật này.</summary>
    public DamageResult DealDamage(CharacterHandle target, float multiplier = 1f)
        => !Presentation.IsResultLocked && controller != null ? controller.DealDamage(this, RuntimeData, target, multiplier) : default;

    /// <summary>Controller gọi sau khi đã trừ giáp/HP: phát animation trúng đòn và event cho bên ngoài.</summary>
    internal void NotifyDamageReceived(DamageInfo damage, DamageResult result)
    {
        if (!result.Accepted) return;
        if (!result.Killed) Presentation.RequestVisualState(CharacterVisualState.Hit);
        // Quái ngoài khung hình (visual đang ẩn) thì không hiện số.
        if (damageText != null && Presentation.IsVisible) damageText.Show(this, damage, result);
        DamageReceived?.Invoke(damage, result);
    }

    /// <summary>
    /// EffectSystem báo một effect trạng thái bắt đầu/kết thúc. Controller đếm và quyết định có đổi màu không,
    /// rồi báo lại qua FeedbackChanged mà <see cref="CharacterFeedback"/> đang nghe.
    /// </summary>
    internal void NotifyEffectFeedback(CharacterHandle target, CharacterFeedbackKind kind, bool active)
        => controller?.NotifyEffect(target, kind, active);

    #endregion

    #region Death And Result

    /// <summary>
    /// Chuỗi kết thúc combat khi chết (controller gọi). Dừng di chuyển/hitbox, phát animation chết, báo Died,
    /// rồi dọn feedback và controller. Vỏ vẫn còn cho tới khi bị thu hồi.
    /// </summary>
    internal void EndCombatLifetime()
    {
        StopMovement(); // Chặn lệnh di chuyển còn lưu trước khi giữ hình ảnh để phát animation chết.
        StopSpatial();
        var spawn = new CharacterHandle(this);
        Presentation.RequestVisualState(RuntimeData != null && RuntimeData.Team == CharacterTeam.Player
            ? CharacterVisualState.Lose : CharacterVisualState.Death);
        // Listener của Died (Combat dọn skill/effect, GameManager kết thúc trận, loot…) chạy trong lời gọi này.
        try { Died?.Invoke(this); }
        finally
        {
            // Listener có thể đã thu hồi rồi spawn lại vỏ này; chỉ dọn tiếp nếu vẫn đúng lượt spawn vừa chết.
            if (spawn.IsCurrent)
            {
                Feedback.Clear();
                controller?.Dispose();
            }
        }
    }

    /// <summary>
    /// Chờ animation chết xong rồi gọi <paramref name="onComplete"/>. Boss luôn hiện và chờ;
    /// quái thường ngoài camera trả về false để được thu hồi ngay.
    /// </summary>
    internal bool WaitForDeathAnimation(Action onComplete)
    {
        if (IsBoss) Presentation.LockForDeath();
        return visualHandle != null && Presentation.IsVisible && visualHandle.WaitForDeathAnimation(onComplete);
    }

    /// <summary>
    /// Khóa nhân vật và phát animation thắng/thua. Skill và effect đã được GameManager dọn trước đó
    /// qua <see cref="ICombatLifecycle.ClearCombat"/>. Trả về false nếu không có animation để chờ.
    /// </summary>
    internal bool PlayResultAnimation(CharacterVisualState state, Action onComplete)
    {
        if (state != CharacterVisualState.Win && state != CharacterVisualState.Lose)
            throw new ArgumentOutOfRangeException(nameof(state));
        // Chỉ Player có Win/Lose; quái chỉ ăn mừng (Win) khi Player thua.
        if (RuntimeData == null || (RuntimeData.Team != CharacterTeam.Player &&
            !(RuntimeData.Team == CharacterTeam.Enemy && state == CharacterVisualState.Win))) return false;
        Presentation.LockForResult(); // Từ đây CanUseSkills = false và chỉ nhận state kết quả.
        StopMovement();
        StopSpatial();
        Feedback.Clear();
        Presentation.SetVisible(true);
        Presentation.RequestVisualState(state);
        return visualHandle != null && Presentation.IsVisible && visualHandle.WaitForResultAnimation(state, onComplete);
    }

    #endregion

    #region Release

    /// <summary>
    /// Thu hồi lượt spawn. Chỉ factory gọi, TRƯỚC khi tắt vỏ, hoặc khi khởi tạo lỗi mà vỏ chưa bật.
    /// Visual được trả xong thì mới báo Released, sau đó factory trả vỏ về pool.
    /// </summary>
    /// <remarks>
    /// Factory.Despawn đã gỡ nhân vật khỏi registry trước khi gọi hàm này, nên Combat dọn skill/effect
    /// (qua CharacterRemoved) khi vỏ vẫn còn nguyên dữ liệu.
    /// </remarks>
    internal void ClearSpawn()
    {
        GameObject previousVisual = visual;
        visual = null;
        EndSpawn(out CharacterRuntimeData previousRuntime, out ulong previousGeneration);
        if (previousVisual != null) pools.Despawn(previousVisual);
        try { NotifyReleased(); }
        finally { runtimePool?.TryReturn(previousRuntime, previousGeneration); }
    }

    // Bị Destroy trực tiếp: Unity hủy cả visual con, nên chỉ gỡ tham chiếu và báo chủ sở hữu.
    // Không trả visual về pool từ callback này vì hierarchy có thể đang trong quá trình hủy.
    private void OnDestroy()
    {
        visual = null;
        Feedback.ForgetStatusVfx(); // Cùng lý do với visual: không trả về pool từ callback hủy.
        EndSpawn(out CharacterRuntimeData previousRuntime, out ulong previousGeneration);
        // Released → registry phát CharacterRemoved → Combat dọn skill/effect bằng identity nó đã giữ.
        try { NotifyReleased(); }
        finally { runtimePool?.TryReturn(previousRuntime, previousGeneration); }
    }

    // Phần dọn chung của ClearSpawn và OnDestroy; trả về runtime cũ để trả về pool sau khi đã báo Released.
    private void EndSpawn(out CharacterRuntimeData previousRuntime, out ulong previousGeneration)
    {
        visualHandle?.Unbind();
        visualHandle = null;
        previousRuntime = RuntimeData;
        previousGeneration = previousRuntime != null ? previousRuntime.Generation : 0;
        StopMovement();
        StopSpatial();
        Deactivated?.Invoke();
        ClearController();
        RuntimeData = null;
        IsBoss = false;
        IsElite = false;
        Presentation.End();
    }

    private void ClearController()
    {
        Feedback.Clear();
        ICharacterController previous = controller;
        controller = null;
        try { previous?.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    // Gỡ mọi listener trước khi báo, để listener của lượt cũ không nhận event của lượt spawn sau trên cùng vỏ.
    private void NotifyReleased()
    {
        Action<CharacterView> released = Released;
        Released = null;
        Died = null;
        DamageReceived = null;
        Activated = null;
        Deactivated = null;
        released?.Invoke(this);
    }

    #endregion

#if UNITY_EDITOR
    #region Editor Gizmos

    // Chọn vỏ khi đang chơi để xem hitbox (xanh lá) và phạm vi tách đàn của quái (vàng).
    // Chỉ vẽ cho lượt spawn đang hoạt động; vỏ rảnh trong pool không có gì để vẽ.
    private void OnDrawGizmosSelected()
    {
        if (RuntimeData == null) return;
        // Vẽ đúng tâm, kích thước và góc của hộp mà job dùng, không theo flip của visual con.
        using (new UnityEditor.Handles.DrawingScope(Color.green, Matrix4x4.TRS(HitPosition, transform.rotation, Vector3.one)))
            UnityEditor.Handles.DrawWireCube(Vector3.zero, new Vector3(
                hitbox.Size.x * Mathf.Abs(transform.lossyScale.x), hitbox.Size.y * Mathf.Abs(transform.lossyScale.y), 0f));
        if (isActiveAndEnabled && RuntimeData.IsAlive && controller != null)
            controller.DrawSeparationGizmo(transform.position);
    }

    #endregion
#endif
}
