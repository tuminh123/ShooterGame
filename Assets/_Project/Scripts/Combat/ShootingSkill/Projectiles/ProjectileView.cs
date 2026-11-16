using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class ProjectileView : MonoBehaviour
{

    #region Dependencies And State
    private IMovementRegistration movementRegistration; // Đăng ký và thực hiện di chuyển Transform.
    private IMovementCommand movementCommand;
    private IProjectileRegistration projectileManager; // Gọi Tick chung, thay Update trên từng viên.
    private IEffectApplication effectManager; // Nhận yêu cầu áp dụng effect; đạn không quản lý runtime.
    private ISpatialQueryBuilder queryBuilder; // Tạo query; SpatialManager sẽ chọn strategy và xử lý theo nhóm.
    private IReadOnlyList<IGameplayEffect> effects; // Các cấu hình được truyền vào khi spawn.
    private float effectHit; // Chính xác hiệu ứng của nguồn lúc bắn.
    private MovementHandle movementHandle; // Đăng ký di chuyển của lượt spawn hiện tại.
    private IEffectVisualSpawner effectVisuals;
    private ICombatObjectRelease combatRelease; // Trả viên đạn về pool khi trúng hoặc hết tuổi thọ.
    private IGameObjectPool pools;
    private GameObject visualObject; // Visual con được thuê riêng, giống cơ chế CharacterView.
    private Renderer[] visualRenderers;
    private Vector2 flightDirection; // Hướng bay hiện tại, có thể đổi khi đuổi mục tiêu.
    private CharacterTarget homingTarget; // Đúng lượt spawn của mục tiêu, tránh bám object pool đã đổi chủ.
    private DamageInfo damageInfo; // Sát thương và phe được chốt khi tạo đạn.
    private IProjectileMove projectileMovement; // Strategy chỉ tính hướng bay.
    private float movementSpeed; // Tốc độ lấy từ skill, world unit/giây.
    private float hitRadius; // Bán kính kiểm tra trúng quái.
    private Vector2 hitOffset; // Offset theo hướng bay, không phụ thuộc scale hình ảnh.
    private Vector3 HitboxCenter => transform.position + transform.rotation * (Vector3)hitOffset;
    private float lifetime; // Thời gian sống còn lại; IProjectileRegistration cập nhật cùng vòng duyệt đạn.
    private EffectType impactEffect; // ID visual trúng đòn từ catalog; không phải gameplay effect.
    private bool isFlying; // Chặn cập nhật hoặc gây sát thương lặp sau khi trúng.

    // Hợp nhất mọi renderer của prefab (sprite, mesh, particle...), không phụ thuộc sprite trên vỏ.
    internal Bounds VisualBounds
    {
        get
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;
            if (visualRenderers != null)
                foreach (Renderer renderer in visualRenderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            return bounds;
        }
    }

    #endregion

    #region Initialization

    [Inject]
    public void Construct(
        IMovementSystem movementSystem,
        IProjectileSystem projectileSystem,
        IEffectSystem effectSystem,
        ICombatSpawnSystem combatSpawnSystem,
        ISpatialSearchFactory spatialSearchFactory,
        IPoolSystem poolSystem)
    {
        this.movementRegistration = movementSystem.GetRegistration();
        this.movementCommand = movementSystem.GetCommands();
        this.projectileManager = projectileSystem.GetProjectileRegistration();
        this.effectManager = effectSystem.GetEffectApplication();
        this.effectVisuals = combatSpawnSystem.GetEffectVisualSpawner();
        this.queryBuilder = spatialSearchFactory.GetQueryBuilder();
        this.pools = poolSystem.GetGameObjectPool();
    }

    internal void Initialize(
        ICombatObjectRelease combatRelease,
        CombatAssetsSO.BulletVisual bulletVisual,
        ProjectileSpawnData projectileData,
        Vector2 flightDirection,
        CharacterTarget homingTarget)
    {
        ClearVisual();
        this.combatRelease = combatRelease;
        this.flightDirection = flightDirection.normalized;
        this.homingTarget = homingTarget;
        damageInfo = projectileData.Damage;
        effects = projectileData.Effects;
        effectHit = projectileData.EffectHit;
        projectileMovement = projectileData.Movement;
        movementSpeed = projectileData.Speed;
        hitRadius = bulletVisual.hitRadius;
        hitOffset = bulletVisual.hitOffset;
        lifetime = projectileData.Lifetime;
        impactEffect = bulletVisual.impactEffect;
        FaceFlightDirection();
        // Gắn visual con và giữ transform đã cấu hình trong prefab; visual tự quản lý cách hiển thị.
        GameObject prefab = bulletVisual.visualPrefab;
        visualObject = pools.Spawn(prefab, transform.position, transform.rotation, PoolGroup.Other, transform);
        if (visualObject == null) throw new System.InvalidOperationException("Could not spawn projectile visual.");
        visualObject.transform.localPosition = prefab.transform.localPosition;
        visualObject.transform.localRotation = prefab.transform.localRotation;
        visualObject.transform.localScale = prefab.transform.localScale;
        visualRenderers = visualObject.GetComponentsInChildren<Renderer>(true);
        isFlying = true;
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!isFlying) return;
        movementHandle = movementRegistration.Register(transform);
        projectileManager.Register(this, movementHandle);
    }

    #endregion

    #region Runtime Operations

    // IProjectileRegistration gọi trước MovementManager.LateTick; không tự đổi vị trí tại đây.
    internal bool PrepareQuery(float deltaTime, out SpatialQuery query)
    {
        query = default;
        if (!isFlying) return false;

        flightDirection = projectileMovement.GetDirection(transform.position, flightDirection, homingTarget).normalized;
        FaceFlightDirection();

        CharacterTeam opponentTeam = CharacterSearch.OpponentOf(damageInfo.SourceTeam);
        // Một sweep đã bao gồm overlap đầu đoạn; gom tất cả đạn vào cùng job thay vì query từng viên.
        float travelDistance = movementSpeed * deltaTime;
        Vector2 origin = HitboxCenter;
        query = queryBuilder.Sweep(origin, origin + flightDirection * travelDistance, hitRadius, opponentTeam);
        CharacterSearch.DrawSearchPath(origin, hitRadius, flightDirection, travelDistance);
        return true;
    }

    internal void CompleteQuery(CharacterTarget hitTarget, Vector2 hitPosition)
    {
        if (!isFlying) return;
        if (hitTarget.TryGetCharacter(out _)) ApplyHitAndRelease(hitTarget, hitPosition);
        else movementCommand.SetMovement(movementHandle, flightDirection, movementSpeed);
    }

    internal bool AdvanceLifetime(float deltaTime)
    {
        if (!isFlying) return false;
        lifetime = Mathf.Max(0f, lifetime - deltaTime);
        if (lifetime > 0f) return true;
        Release();
        return false;
    }

    private void ApplyHitAndRelease(CharacterTarget hitTarget, Vector2 hitPosition)
    {
        isFlying = false;
        ICombatObjectRelease owningSpawner = combatRelease;
        IEffectVisualSpawner hitVisuals = effectVisuals;
        MovementHandle hitRegistration = movementHandle;
        // Damage callback có thể tái sử dụng chính viên đạn; chốt dữ liệu của lượt trúng trước khi gọi ra ngoài.
        IReadOnlyList<IGameplayEffect> hitEffects = effects;
        DamageInfo hitDamage = damageInfo;
        float sourceEffectHit = effectHit;
        EffectType hitVisual = impactEffect; // Chốt trước callback damage vì callback có thể tái sử dụng chính viên đạn.
        Vector3 hitVisualPosition = new Vector3(hitPosition.x, hitPosition.y, transform.position.z);
        try
        {
            if (!hitTarget.TryGetCharacter(out CharacterView character)) return;
            DamageResult result = character.ReceiveDamage(hitDamage);
            if (!result.Accepted) return;
            for (int index = 0; index < hitEffects.Count; index++)
            {
                // Manager kiểm tra lại CharacterTarget: không áp dụng sang cá thể mới nếu quái vừa chết/despawn.
                effectManager.Apply(hitEffects[index], hitTarget, hitDamage.SourceTeam, sourceEffectHit);
            }
            hitVisuals.SpawnEffect(hitVisual, hitVisualPosition);
        }
        finally
        {
            // Callback có thể despawn/spawn lại object; chỉ thu hồi đúng lượt vừa gây sát thương.
            if (movementRegistration.IsRegistered(hitRegistration)) owningSpawner.Release(gameObject);
        }
    }

    #endregion

    #region Cleanup

    internal void Release()
    {
        isFlying = false;
        combatRelease.Release(gameObject);
    }

    #endregion

    #region Runtime Operations

    private void FaceFlightDirection()
        => transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg);

    #endregion

    #region Cleanup

    // CombatSpawner gọi TRƯỚC khi trả vỏ về pool. Xóa ownership trước callback để tránh trả con hai lần.
    internal void ClearVisual()
    {
        GameObject previous = visualObject;
        visualObject = null;
        visualRenderers = null;
        if (previous != null) pools.Despawn(previous);
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        projectileManager?.Unregister(movementHandle);
        movementRegistration?.Unregister(movementHandle);
        movementHandle = default;
        isFlying = false;
        combatRelease = null;
        projectileMovement = null;
        homingTarget = default;
        damageInfo = default;
        effects = null;
        effectHit = 0f;
        impactEffect = EffectType.None;
        flightDirection = default;
        hitOffset = default;
        movementSpeed = hitRadius = lifetime = 0f;
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!isFlying) return;
        using (new UnityEditor.Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(HitboxCenter, Vector3.forward, hitRadius);
    }
#endif
}
