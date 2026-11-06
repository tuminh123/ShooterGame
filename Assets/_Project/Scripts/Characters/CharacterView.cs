using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

// * Vỏ Unity dùng chung cho mọi nhân vật; không quyết định input hay AI.
// * Một vỏ được pool tái sử dụng cho nhiều cá thể, mỗi lượt được gắn runtime/controller mới trước khi bật.
public sealed class CharacterView : MonoBehaviour, IDamageReceiver
{
    #region Dependencies & Spawn State

    private MovementManager movement; // * Dịch vụ áp dụng chuyển động, nhận lệnh từ hành vi hiện tại qua Move.
    private PoolManager pools; // * Pool của vỏ và visual; visual được thu hồi riêng khi tháo vỏ.
    private MovementHandle movementHandle; // * Đăng ký của lần hoạt động hiện tại; vô hiệu ngay khi tắt vỏ.
    private ICharacterController controller; // * Hành vi được gắn lúc spawn, không serialize vào prefab.
    private GameObject visual; // * Bản sao visual con, lấy từ prefab được khai báo trong CharacterDataSO.
    private SpriteRenderer[] sprites; // * Cache để lật hướng hiển thị, không tìm component trong từng frame.
    private Animator[] animators; // * Reset animation khi bật lại để không giữ animation của lượt sử dụng trước.
    private HitboxData hitbox; // Cấu hình local; không cần Collider2D trên vỏ nhân vật.
    private ISpatialManager spatial;
    private EffectManager effectManager; // Quản lý gameplay effect tập trung, không quản lý prefab VFX.
    private SkillFactory skillFactory;
    private CharacterRuntimePool runtimePool;

    #endregion

    #region Public State

    public CharacterSkills Skills { get; private set; }
    public CharacterEffectHandler Effects { get; private set; }
    public CharacterRuntimeData RuntimeData { get; private set; } // * Dữ liệu cá thể đang sử dụng vỏ; null khi đã thu hồi.
    public IReadOnlyList<SpriteRenderer> Sprites => sprites;
    public SpatialHandle SpatialHandle { get; private set; }
    public ICharacterSearch Search { get; private set; } // Chỉ cần hợp đồng tìm kiếm, không giữ implementation/factory cụ thể.
    public ITimerManager Timers { get; private set; }
    public bool IsBoss { get; private set; } // Metadata của lượt spawn; UI và các hệ thống khác có thể đọc.
    public bool IsVisualVisible => visual != null && visual.activeSelf;
    public Vector3 HitPosition => transform.TransformPoint(hitbox.Offset);

    #endregion

    #region Lifecycle & Combat Events

    public event Action<DamageInfo, DamageResult> DamageReceived;
    // Raised before visual cleanup, and on temporary disable, so owners can cancel presentation tasks.
    public event Action Deactivated;
    public event Action<bool> VisualVisibilityChanged; // Báo thay đổi hiển thị mà không phụ thuộc script UI cụ thể.
    public event Action<CharacterView> Died;
    public event Action<CharacterView> Released; // * Báo chủ sở hữu khi despawn chủ động hoặc bị hủy; tắt tạm thời không kết thúc cá thể.

    #endregion

    #region Injection & Spawn Initialization

    [Inject]
    public void Construct(MovementManager movement, PoolManager pools, EffectManager effectManager,
        SkillFactory skillFactory, ISpatialManager spatial, ICharacterSearch search, ITimerManager timers,
        CharacterRuntimePool runtimePool)
    {
        this.movement = movement;
        this.pools = pools;
        this.effectManager = effectManager;
        this.skillFactory = skillFactory;
        this.runtimePool = runtimePool;
        this.spatial = spatial;
        Search = search;
        Timers = timers;
    }

    // * Factory gọi khi vỏ còn nằm dưới nhánh Preparing đang tắt; OnEnable chưa được phép chạy hành vi.
    public void Initialize(CharacterDataSO template, CharacterRuntimeData runtime, ICharacterController behavior)
    {
        if (gameObject.activeInHierarchy || RuntimeData != null)
            throw new InvalidOperationException("Initialize a released, inactive character shell.");
        if (template == null || runtime == null || behavior == null || template.VisualPrefab == null)
            throw new ArgumentException("Character needs data, runtime, behavior and a visual prefab.");
        if (template.CharacterType != runtime.CharacterType || template.Team != runtime.Team)
            throw new ArgumentException("Character template and runtime must match.");

        // Giữ layer của vỏ theo phe cho các bộ lọc Unity khác; tìm kiếm gameplay dùng Team trong dữ liệu native.
        string layerName = template.Team == CharacterTeam.Enemy
            ? GameConstants.PhysicsLayers.Enemy
            : GameConstants.PhysicsLayers.Player;
        int characterLayer = LayerMask.NameToLayer(layerName);
        if (characterLayer < 0)
            throw new InvalidOperationException($"Add the {layerName} layer in Project Settings.");
        gameObject.layer = characterLayer;

        CharacterVisualSettings presentation = template.VisualSettings;
        presentation.Validate();
        hitbox = presentation.Hitbox;

        RuntimeData = runtime;
        IsBoss = template.IsBoss;
        controller = behavior;
        // * Parent vẫn đang tắt nên visual cũng được chuẩn bị trước khi xuất hiện trong scene.
        visual = pools.Spawn(template.VisualPrefab, transform.position, transform.rotation, PoolGroup.Other, transform);
        if (visual == null)
            throw new InvalidOperationException("Could not spawn the character visual.");
        presentation.ApplyVisual(visual.transform);
        // Áp dụng mỗi lần spawn để vỏ pool không giữ kích thước thanh máu của loại boss trước.
        if (TryGetComponent<CharacterUI>(out var characterUI)) characterUI.ApplyLayout(presentation);
        sprites = visual.GetComponentsInChildren<SpriteRenderer>(true);
        animators = visual.GetComponentsInChildren<Animator>(true);
        foreach (SpriteRenderer sprite in sprites)
            sprite.flipX = false;
        Effects = new CharacterEffectHandler(this, effectManager);
        Skills = new CharacterSkills(this, template.Skills, skillFactory);
        // Enemy bắt đầu ẩn; manager camera sẽ bật nếu visual giao khung hình trước khi render.
        visual.SetActive(runtime.Team != CharacterTeam.Enemy);
    }

    #endregion

    #region Activation & Tick

    private void OnEnable()
    {
        if (RuntimeData == null)
            return;
        movementHandle = movement.Register(transform);
        SpatialHandle = spatial.Register(this);
        Skills?.SetPaused(false);
        effectManager.SetPaused(this, RuntimeData, false);
        controller?.SetPaused(false);
        ResetVisualAnimation();
    }

    private void Update()
    {
        if (RuntimeData == null)
            return;
        if (!isActiveAndEnabled)
            return;
        // * Unity gọi Update trên vỏ; phần quyết định hướng chạy thuộc class hành vi thuần.
        controller.Tick(this, RuntimeData);
    }

    #endregion

    #region Visual Visibility & Bounds

    // Chỉ đổi trạng thái object visual con; vỏ vẫn Update, di chuyển và nhận sát thương bình thường.
    internal void SetVisualVisible(bool visible)
    {
        if (visual == null || visual.activeSelf == visible) return;
        visual.SetActive(visible);
        if (visible) ResetVisualAnimation();
        VisualVisibilityChanged?.Invoke(visible);
    }

    // Reset khi vừa hiện hoặc bật lại vỏ; không cập nhật Animator trên visual đang tắt.
    private void ResetVisualAnimation()
    {
        foreach (Animator animator in animators)
        {
            if (!animator.isActiveAndEnabled) continue;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    // Tính từ local bounds và Transform mới, kể cả khi visual đang tắt và Renderer.bounds chưa được cập nhật.
    internal Bounds GetVisualBounds()
    {
        Bounds combined = new Bounds(transform.position, Vector3.zero);
        bool hasSprite = false;
        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite == null || sprite.sprite == null) continue;
            Bounds local = sprite.localBounds;
            Matrix4x4 matrix = sprite.transform.localToWorldMatrix;
            Vector3 x = matrix.MultiplyVector(new Vector3(local.extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, local.extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, local.extents.z));
            Vector3 extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            Bounds world = new Bounds(matrix.MultiplyPoint3x4(local.center), extent * 2f);
            if (hasSprite) combined.Encapsulate(world);
            else { combined = world; hasSprite = true; }
        }
        return combined;
    }

    #endregion

    #region Movement & Facing

    // * Mặc định nhìn theo hướng chạy; Enemy có thể truyền hướng đến Player khi đang lùi ra khỏi hàng xóm.
    // * facingDirection chỉ là lệnh hiển thị của frame hiện tại, không thêm dữ liệu cấu hình vào template chung.
    public void Move(Vector2 direction, float speed, float maxDistance = float.PositiveInfinity,
        float? facingDirection = null)
    {
        FaceDirection(facingDirection ?? direction.x);
        movement?.SetMovement(movementHandle, direction, speed, maxDistance);
    }

    private void FaceDirection(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) <= 0.01f || sprites == null)
            return;

        bool flip = horizontalDirection < 0f;
        foreach (SpriteRenderer sprite in sprites)
            sprite.flipX = flip;
    }

    #endregion

    #if UNITY_EDITOR
    #region Hitbox & Search Gizmos
    // * Bật Gizmos và chọn vỏ Enemy khi đang chơi để xem phạm vi quét tránh nhau.
    // * Chỉ vẽ cho cá thể đang hoạt động; vỏ chưa khởi tạo hoặc đã trả về pool không có vòng debug.
    // * Controller giữ bán kính và cách vẽ riêng của Enemy, vỏ chỉ chuyển tiếp callback từ Unity Editor.
    private void OnDrawGizmosSelected()
    {
        if (RuntimeData != null)
        {
            // Vẽ đúng tâm, kích thước và góc hộp mà job sử dụng, không theo flip của visual con.
            using (new UnityEditor.Handles.DrawingScope(Color.green, Matrix4x4.TRS(HitPosition,
                transform.rotation, Vector3.one)))
                UnityEditor.Handles.DrawWireCube(Vector3.zero, new Vector3(
                    hitbox.Size.x * Mathf.Abs(transform.lossyScale.x), hitbox.Size.y * Mathf.Abs(transform.lossyScale.y), 0f));
        }
        if (isActiveAndEnabled && RuntimeData != null && RuntimeData.IsAlive &&
            controller is EnemyController enemy)
            enemy.DrawSeparationGizmo(transform.position);
    }
    #endregion
    #endif

    #region Combat API & Death

    // Unity damage endpoint only; the controller owns shield/HP/death behavior.
    public DamageResult ReceiveDamage(DamageInfo damage)
    {
        return controller != null ? controller.ReceiveDamage(this, RuntimeData, damage) : default;
    }

    public void ChangeHealth(float amount) => controller?.ChangeHealth(this, RuntimeData, amount);
    public void GrantShield(float amount) => controller?.GrantShield(this, RuntimeData, amount);

    public DamageResult DealDamage(CharacterTarget target, float multiplier = 1f)
        => controller != null ? controller.DealDamage(this, RuntimeData, target, multiplier) : default;

    internal void NotifyDamageReceived(DamageInfo damage, DamageResult result)
    {
        if (result.Accepted) DamageReceived?.Invoke(damage, result);
    }

    internal void EndCombatLifetime()
    {
        StopSpatial();
        CharacterRuntimeData identity = RuntimeData;
        ulong generation = identity != null ? identity.Generation : 0;
        try { Died?.Invoke(this); }
        finally
        {
            if (identity != null && ReferenceEquals(RuntimeData, identity) && identity.Generation == generation)
            {
                try { ClearSkills(); }
                finally
                {
                    if (ReferenceEquals(RuntimeData, identity) && identity.Generation == generation)
                    {
                        try { Effects?.Dispose(); }
                        finally { if (ReferenceEquals(RuntimeData, identity) && identity.Generation == generation) controller?.Dispose(); }
                    }
                }
            }
        }
    }

    #endregion

    #region Disable & Runtime Cleanup

    private void ClearSkills()
    {
        CharacterSkills previous = Skills;
        Skills = null;
        try { previous?.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    // * Unity đang thay đổi trạng thái cả hierarchy trong OnDisable: chỉ dừng movement, không thu hồi/reparent visual.
    // * Giữ data/hành vi để việc tắt rồi bật tạm thời có thể tiếp tục cùng cá thể.
    private void OnDisable()
    {
        StopMovement();
        StopSpatial();
        Skills?.SetPaused(true);
        controller?.SetPaused(true);
        if (RuntimeData != null) effectManager?.SetPaused(this, RuntimeData, true);
        Deactivated?.Invoke();
    }

    private void StopMovement()
    {
        movement?.Unregister(movementHandle);
        movementHandle = default;
    }

    private void ClearController()
    {
        ICharacterController previous = controller;
        controller = null;
        try { previous?.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void StopSpatial()
    {
        spatial?.Unregister(SpatialHandle);
        SpatialHandle = default;
    }

    #endregion

    #region Spatial Snapshot

    // Chụp hình hộp world XY cho job. Vỏ hỗ trợ xoay Z và scale; không dùng hierarchy gây shear.
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

    #region Destruction & Pool Release

    // * Khi bị Destroy trực tiếp, Unity hủy cả visual con; chỉ gỡ tham chiếu và báo chủ sở hữu.
    // * Không trả visual về pool từ callback này vì hierarchy có thể đang trong quá trình hủy/tắt.
    private void OnDestroy()
    {
        CharacterRuntimeData previousRuntime = RuntimeData;
        ulong previousGeneration = previousRuntime != null ? previousRuntime.Generation : 0;
        StopMovement();
        StopSpatial();
        ClearSkills();
        Deactivated?.Invoke();
        Effects?.Dispose();
        Effects = null;
        ClearController();
        RuntimeData = null;
        IsBoss = false;
        visual = null;
        sprites = null;
        animators = null;
        try { NotifyReleased(); }
        finally { runtimePool?.TryReturn(previousRuntime, previousGeneration); }
    }

    // * Chỉ factory gọi chủ động TRƯỚC khi tắt vỏ, hoặc khi khởi tạo lỗi mà vỏ chưa bật.
    // * Visual được thu hồi xong rồi chủ sở hữu mới được thông báo, sau đó factory trả vỏ về pool.
    internal void ClearSpawn()
    {
        CharacterRuntimeData previousRuntime = RuntimeData;
        ulong previousGeneration = previousRuntime != null ? previousRuntime.Generation : 0;
        StopMovement();
        StopSpatial();
        ClearSkills();
        Deactivated?.Invoke();
        Effects?.Dispose();
        Effects = null;
        ClearController();
        RuntimeData = null;
        IsBoss = false;
        sprites = null;
        animators = null;
        if (visual != null)
        {
            GameObject previousVisual = visual;
            visual = null;
            pools.Despawn(previousVisual);
        }
        try { NotifyReleased(); }
        finally { runtimePool?.TryReturn(previousRuntime, previousGeneration); }
    }

    private void NotifyReleased()
    {
        Action<CharacterView> released = Released;
        Released = null;
        Died = null;
        DamageReceived = null;
        Deactivated = null;
        VisualVisibilityChanged = null;
        released?.Invoke(this);
    }

    #endregion
}
