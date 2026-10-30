using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// * Vỏ Unity dùng chung cho mọi nhân vật; không quyết định input hay AI.
// * Một vỏ được pool tái sử dụng cho nhiều cá thể, mỗi lượt được gắn runtime/controller mới trước khi bật.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CharacterView : MonoBehaviour, IDamageReceiver
{
    private MovementManager movement; // * Dịch vụ áp dụng chuyển động, nhận lệnh từ hành vi hiện tại qua Move.
    private PoolManager pools; // * Pool của vỏ và visual; visual được thu hồi riêng khi tháo vỏ.
    private MovementHandle movementHandle; // * Đăng ký của lần hoạt động hiện tại; vô hiệu ngay khi tắt vỏ.
    private ICharacterController controller; // * Hành vi được gắn lúc spawn, không serialize vào prefab.
    private GameObject visual; // * Bản sao visual con, lấy từ prefab được khai báo trong CharacterDataSO.
    private SpriteRenderer[] sprites; // * Cache để lật hướng hiển thị, không tìm component trong từng frame.
    private Animator[] animators; // * Reset animation khi bật lại để không giữ animation của lượt sử dụng trước.
    private BoxCollider2D hitbox; // * Một hitbox trên vỏ gốc; tìm kiếm dùng TryGetComponent tại cùng object.
    private ShootingService shooting;
    private SkillFactory skillFactory;
    private StatusEffectFactory effectFactory;
    public CharacterSkillHandler Skills { get; private set; }
    public CharacterEffectHandler Effects { get; private set; }
    public CharacterRuntimeData RuntimeData { get; private set; } // * Dữ liệu cá thể đang sử dụng vỏ; null khi đã thu hồi.
    public IReadOnlyList<SpriteRenderer> Sprites => sprites;
    public Collider2D Hitbox => hitbox;
    public event Action<DamageInfo, DamageResult> DamageReceived;
    // Raised before visual cleanup, and on temporary disable, so owners can cancel presentation tasks.
    public event Action Deactivated;
    public Vector3 HitPosition => hitbox != null ? transform.TransformPoint(hitbox.offset) : transform.position;
    public event Action<CharacterView> Released; // * Báo chủ sở hữu khi despawn chủ động hoặc bị hủy; tắt tạm thời không kết thúc cá thể.

    [Inject]
    public void Construct(MovementManager movement, PoolManager pools, ShootingService shooting,
        SkillFactory skillFactory, StatusEffectFactory effectFactory)
    {
        this.movement = movement;
        this.pools = pools;
        this.shooting = shooting;
        this.skillFactory = skillFactory;
        this.effectFactory = effectFactory;
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

        // * Vỏ pool có thể vừa là Enemy rồi được dùng làm Player; luôn gán lại layer chứa hitbox.
        // * Chỉ đổi layer của vỏ; visual con không tham gia truy vấn hàng xóm hoặc va chạm gameplay.
        string layerName = template.Team == CharacterTeam.Enemy
            ? GameConstants.PhysicsLayers.Enemy
            : GameConstants.PhysicsLayers.Player;
        int characterLayer = LayerMask.NameToLayer(layerName);
        if (characterLayer < 0)
            throw new InvalidOperationException($"Add the {layerName} layer in Project Settings.");
        gameObject.layer = characterLayer;

        // * Di chuyển bằng MovementManager; collider chỉ phục vụ query, không cần Rigidbody2D.
        hitbox = GetComponent<BoxCollider2D>();
        hitbox.enabled = true;
        hitbox.isTrigger = true;
        hitbox.size = template.ColliderSize;
        hitbox.offset = template.ColliderOffset;

        RuntimeData = runtime;
        controller = behavior;
        // * Parent vẫn đang tắt nên visual cũng được chuẩn bị trước khi xuất hiện trong scene.
        visual = pools.Spawn(template.VisualPrefab, transform.position, transform.rotation, PoolGroup.Other, transform);
        if (visual == null)
            throw new InvalidOperationException("Could not spawn the character visual.");
        visual.transform.localPosition = template.VisualPrefab.transform.localPosition;
        visual.transform.localRotation = template.VisualPrefab.transform.localRotation;
        visual.transform.localScale = template.VisualPrefab.transform.localScale;
        sprites = visual.GetComponentsInChildren<SpriteRenderer>(true);
        animators = visual.GetComponentsInChildren<Animator>(true);
        foreach (SpriteRenderer sprite in sprites)
            sprite.flipX = false;
        Effects = new CharacterEffectHandler(this, effectFactory);
        Skills = new CharacterSkillHandler(template.Skills, skillFactory, new SkillContext(this, shooting));
    }

    private void OnEnable()
    {
        if (RuntimeData == null)
            return;
        movementHandle = movement.Register(transform);
        CharacterSearch.InvalidatePhysics();
        foreach (Animator animator in animators)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private void Update()
    {
        if (RuntimeData == null)
            return;
        CharacterRuntimeData identity = RuntimeData;
        Effects?.Tick(Time.deltaTime);
        if (!ReferenceEquals(RuntimeData, identity))
            return;
        // * Unity gọi Update trên vỏ; phần quyết định hướng chạy thuộc class hành vi thuần.
        controller.Tick(this, RuntimeData);
        // * Controller có thể despawn ngay trong Tick; luôn kiểm tra lại lượt đang hoạt động.
        if (RuntimeData != null && RuntimeData.IsAlive && Time.deltaTime > 0f)
            Skills?.Tick(Time.deltaTime);
    }

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

    #if UNITY_EDITOR
    // * Bật Gizmos và chọn vỏ Enemy khi đang chơi để xem phạm vi quét tránh nhau.
    // * Chỉ vẽ cho cá thể đang hoạt động; vỏ chưa khởi tạo hoặc đã trả về pool không có vòng debug.
    // * Controller giữ bán kính và cách vẽ riêng của Enemy, vỏ chỉ chuyển tiếp callback từ Unity Editor.
    private void OnDrawGizmosSelected()
    {
        if (isActiveAndEnabled && RuntimeData != null && RuntimeData.IsAlive &&
            controller is EnemyController enemy)
            enemy.DrawSeparationGizmo(transform.position);
    }
    #endif

    // * View chỉ nối hitbox tới luật nhận damage của Character runtime.
    // * Hành vi riêng quyết định chết; Enemy thu hồi, Player dừng lại.
    public DamageResult ReceiveDamage(DamageInfo damage)
    {
        if (!isActiveAndEnabled || RuntimeData == null)
            return default;
        CharacterRuntimeData current = RuntimeData;
        DamageResult result = current.ReceiveDamage(damage);
        if (result.Accepted && ReferenceEquals(RuntimeData, current))
            DamageReceived?.Invoke(damage, result);
        if (result.Killed && ReferenceEquals(RuntimeData, current))
        {
            Skills?.Dispose();
            Effects?.Dispose();
            if (ReferenceEquals(RuntimeData, current))
                controller.OnDeath(this, current);
        }
        return result;
    }

    // * Unity đang thay đổi trạng thái cả hierarchy trong OnDisable: chỉ dừng movement, không thu hồi/reparent visual.
    // * Giữ data/hành vi để việc tắt rồi bật tạm thời có thể tiếp tục cùng cá thể.
    private void OnDisable()
    {
        StopMovement();
        Deactivated?.Invoke();
        CharacterSearch.InvalidatePhysics();
    }

    private void StopMovement()
    {
        movement?.Unregister(movementHandle);
        movementHandle = default;
    }

    // * Khi bị Destroy trực tiếp, Unity hủy cả visual con; chỉ gỡ tham chiếu và báo chủ sở hữu.
    // * Không trả visual về pool từ callback này vì hierarchy có thể đang trong quá trình hủy/tắt.
    private void OnDestroy()
    {
        StopMovement();
        Deactivated?.Invoke();
        Skills?.Dispose();
        Skills = null;
        Effects?.Dispose();
        Effects = null;
        controller = null;
        RuntimeData = null;
        visual = null;
        sprites = null;
        animators = null;
        NotifyReleased();
    }

    // * Chỉ factory gọi chủ động TRƯỚC khi tắt vỏ, hoặc khi khởi tạo lỗi mà vỏ chưa bật.
    // * Visual được thu hồi xong rồi chủ sở hữu mới được thông báo, sau đó factory trả vỏ về pool.
    internal void ClearSpawn()
    {
        StopMovement();
        Deactivated?.Invoke();
        Skills?.Dispose();
        Skills = null;
        Effects?.Dispose();
        Effects = null;
        CharacterSearch.InvalidatePhysics();
        controller = null;
        RuntimeData = null;
        sprites = null;
        animators = null;
        if (visual != null)
        {
            GameObject previousVisual = visual;
            visual = null;
            pools.Despawn(previousVisual);
        }
        NotifyReleased();
    }

    private void NotifyReleased()
    {
        Action<CharacterView> released = Released;
        Released = null;
        DamageReceived = null;
        Deactivated = null;
        released?.Invoke(this);
    }
}
