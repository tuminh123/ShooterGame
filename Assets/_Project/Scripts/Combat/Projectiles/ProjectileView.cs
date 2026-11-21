using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vỏ của một viên đạn (MonoBehaviour trên prefab, được pool tái sử dụng).
/// </summary>
/// <remarks>
/// Vòng đời của một lượt bay:
/// <list type="number">
/// <item><see cref="ProjectileSystem.SpawnBullet"/> gọi <see cref="Initialize"/> khi vỏ còn tắt, truyền dịch vụ và thông số của viên đạn.</item>
/// <item>Pool bật vỏ, <c>OnEnable</c> đăng ký movement và ProjectileSystem.</item>
/// <item>Mỗi frame, ProjectileSystem gọi <see cref="AdvanceLifetime"/> → <see cref="PrepareQuery"/> → quét va chạm theo lô → <see cref="CompleteQuery"/>.</item>
/// <item>Trúng mục tiêu hoặc hết tuổi thọ thì đạn trả vỏ về pool qua <see cref="ProjectileSystem.Release"/>; <c>OnDisable</c> xóa toàn bộ trạng thái của lượt bay.</item>
/// </list>
/// Không đổi tên class này: prefab đạn tham chiếu script qua GUID trong file .meta.
/// </remarks>
public sealed class ProjectileView : MonoBehaviour
{
    #region Services

    // Gán ở Initialize đầu tiên và giữ lại qua các lượt bay, vì mọi viên đạn dùng chung một context.
    // Vỏ được pool tạo sẵn (prewarm) mà chưa bay lần nào thì context vẫn null, nên OnDisable phải kiểm tra null.
    private ProjectileContext context;

    #endregion

    #region Flight State (reset mỗi lượt bay)

    private MovementHandle movementHandle; // Đăng ký di chuyển của lượt bay hiện tại.
    private Vector2 flightDirection; // Hướng bay hiện tại; có thể đổi mỗi frame nếu là đạn đuổi.
    private CharacterHandle homingTarget; // Đúng lượt spawn của mục tiêu, để không đuổi theo vỏ đã đổi chủ.
    private DamageInfo damageInfo; // Sát thương và phe, chốt lúc bắn.
    private IReadOnlyList<IGameplayEffect> effects; // Effect áp khi trúng, chốt lúc bắn.
    private float effectHit; // Chỉ số chính xác hiệu ứng của nguồn, chốt lúc bắn.
    private IProjectileMove projectileMovement; // Strategy quyết định hướng bay.
    private float movementSpeed; // World unit mỗi giây.
    private float hitRadius; // Bán kính quét va chạm.
    private Vector2 hitOffset; // Offset của hitbox theo hướng bay, không phụ thuộc scale của hình ảnh.
    private float lifetime; // Thời gian bay còn lại (giây).
    private EffectType impactEffect; // VFX trúng đòn (chỉ là hình ảnh, không phải effect gameplay).
    private bool isFlying; // Chặn cập nhật hoặc gây sát thương lặp sau khi đã trúng.

    #endregion

    #region Visual

    private GameObject visualObject; // Visual con thuê riêng từ pool, giống cơ chế của CharacterView.
    private Renderer[] visualRenderers;

    private Vector3 HitboxCenter => transform.position + transform.rotation * (Vector3)hitOffset;

    /// <summary>
    /// Bounds gộp mọi renderer đang hiện của visual (sprite, mesh, particle…).
    /// ProjectileSystem dùng để thu hồi đạn đã bay ra khỏi camera.
    /// </summary>
    internal Bounds VisualBounds
    {
        get
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;
            if (visualRenderers == null) return bounds;
            foreach (Renderer renderer in visualRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Chuẩn bị một lượt bay. Phải được gọi khi vỏ còn tắt (callback <c>beforeActivate</c> của pool),
    /// vì <c>OnEnable</c> dùng ngay các dịch vụ và thông số được gán ở đây.
    /// </summary>
    internal void Initialize(
        ProjectileContext projectileContext,
        CombatAssetsSO.BulletVisual bulletVisual,
        ProjectileSpawnData projectileData,
        Vector2 direction,
        CharacterHandle target)
    {
        // Gán context trước ClearVisual, vì ClearVisual cần pool để trả visual cũ.
        context = projectileContext;
        ClearVisual();

        flightDirection = direction.normalized;
        homingTarget = target;
        damageInfo = projectileData.Damage;
        effects = projectileData.Effects;
        effectHit = projectileData.EffectHit;
        projectileMovement = projectileData.Movement;
        movementSpeed = projectileData.Speed;
        lifetime = projectileData.Lifetime;
        hitRadius = bulletVisual.HitRadius;
        hitOffset = bulletVisual.HitOffset;
        impactEffect = bulletVisual.ImpactEffect;
        FaceFlightDirection();

        // Gắn visual con và giữ đúng transform local đã cấu hình trong prefab visual.
        GameObject prefab = bulletVisual.VisualPrefab;
        visualObject = context.Pools.Spawn(prefab, transform.position, transform.rotation, PoolGroup.Other, transform);
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
        // Pool có thể bật vỏ lúc prewarm mà không qua Initialize; khi đó chưa có lượt bay nào để đăng ký.
        if (!isFlying) return;
        movementHandle = context.MovementRegistration.Register(transform);
        context.Projectiles.Register(this, movementHandle);
    }

    private void OnDisable()
    {
        // context null nghĩa là vỏ prewarm chưa bay lần nào, nên không có gì để gỡ đăng ký.
        context?.Projectiles.Unregister(movementHandle);
        context?.MovementRegistration.Unregister(movementHandle);

        movementHandle = default;
        isFlying = false;
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

    #region Per-Frame Flight (ProjectileSystem gọi)

    /// <summary>
    /// Bước 2 (sau AdvanceLifetime): cập nhật hướng bay và tạo query quét đoạn đường sắp bay trong frame này.
    /// Được gọi trước MovementSystem.LateTick; hàm này không tự đổi vị trí.
    /// </summary>
    internal bool PrepareQuery(float deltaTime, out SpatialQuery query)
    {
        query = default;
        if (!isFlying) return false;

        flightDirection = projectileMovement.GetDirection(transform.position, flightDirection, homingTarget).normalized;
        FaceFlightDirection();

        // Một lần sweep đã bao gồm cả overlap ở đầu đoạn; mọi viên được gom vào cùng một job thay vì query từng viên.
        CharacterTeam opponentTeam = CharacterSearch.OpponentOf(damageInfo.SourceTeam);
        float travelDistance = movementSpeed * deltaTime;
        Vector2 origin = HitboxCenter;
        query = context.QueryBuilder.Sweep(origin, origin + flightDirection * travelDistance, hitRadius, opponentTeam);
        CharacterSearch.DrawSearchPath(origin, hitRadius, flightDirection, travelDistance);
        return true;
    }

    /// <summary>Bước 3: nhận kết quả quét. Trúng thì gây sát thương; không trúng thì bay tiếp.</summary>
    internal void CompleteQuery(CharacterHandle hitTarget, Vector2 hitPosition)
    {
        if (!isFlying) return;
        if (hitTarget.TryGetCharacter(out _)) ApplyHitAndRelease(hitTarget, hitPosition);
        else context.MovementCommand.SetMovement(movementHandle, flightDirection, movementSpeed);
    }

    /// <summary>Bước 1: trừ tuổi thọ. Hết tuổi thọ thì trả vỏ về pool và trả về false.</summary>
    internal bool AdvanceLifetime(float deltaTime)
    {
        if (!isFlying) return false;
        lifetime = Mathf.Max(0f, lifetime - deltaTime);
        if (lifetime > 0f) return true;
        Release();
        return false;
    }

    #endregion

    #region Hit And Release

    private void ApplyHitAndRelease(CharacterHandle hitTarget, Vector2 hitPosition)
    {
        isFlying = false;

        // Callback sát thương có thể giết mục tiêu, kéo theo despawn hoặc spawn lại, và có thể tái sử dụng
        // chính viên đạn này. Vì vậy chốt mọi dữ liệu của lượt trúng TRƯỚC khi gọi ra ngoài.
        MovementHandle hitRegistration = movementHandle;
        IReadOnlyList<IGameplayEffect> hitEffects = effects;
        DamageInfo hitDamage = damageInfo;
        float sourceEffectHit = effectHit;
        EffectType hitVisual = impactEffect;
        Vector3 hitVisualPosition = new Vector3(hitPosition.x, hitPosition.y, transform.position.z);

        try
        {
            if (!hitTarget.TryGetCharacter(out CharacterView character)) return;
            DamageResult result = character.ReceiveDamage(hitDamage);
            if (!result.Accepted) return;

            // EffectSystem kiểm tra lại handle: quái vừa chết hoặc despawn thì effect không áp sang cá thể mới.
            for (int index = 0; index < hitEffects.Count; index++)
                context.Effects.Apply(hitEffects[index], hitTarget, hitDamage.SourceTeam, sourceEffectHit);
            context.Vfx.Spawn(hitVisual, hitVisualPosition);
        }
        finally
        {
            // Chỉ thu hồi nếu vỏ vẫn thuộc đúng lượt bay vừa gây sát thương (callback có thể đã thu hồi nó).
            if (context.MovementRegistration.IsRegistered(hitRegistration)) context.Projectiles.Release(this);
        }
    }

    /// <summary>Kết thúc lượt bay và trả vỏ về pool. ProjectileSystem cũng gọi khi dọn đạn hàng loạt.</summary>
    internal void Release()
    {
        isFlying = false;
        context.Projectiles.Release(this);
    }

    /// <summary>
    /// Trả visual con về pool. ProjectileSystem gọi TRƯỚC khi trả vỏ về pool; xóa reference trước khi trả
    /// để không trả cùng một visual hai lần.
    /// </summary>
    internal void ClearVisual()
    {
        GameObject previous = visualObject;
        visualObject = null;
        visualRenderers = null;
        if (previous != null) context.Pools.Despawn(previous);
    }

    #endregion

    #region Helpers

    private void FaceFlightDirection()
        => transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg);

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
