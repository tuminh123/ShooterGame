using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Vỏ của một viên đạn (MonoBehaviour trên prefab, được pool tái sử dụng).
/// </summary>
/// <remarks>
/// Vòng đời của một lượt bay:
/// <list type="number">
/// <item><see cref="ProjectileSystem.SpawnBullet"/> thuê vỏ rồi gọi ngay <see cref="Launch"/>: gán thông số, thuê visual con
/// theo BulletType, đăng ký movement và ProjectileSystem.</item>
/// <item>Mỗi frame, ProjectileSystem gọi <see cref="AdvanceLifetime"/> → <see cref="PrepareQuery"/> → quét va chạm theo lô → <see cref="CompleteQuery"/>.
/// Đạn thường (trúng là mất) quét theo lô; đạn xuyên/nảy tự quét mọi mục tiêu trên đoạn bay ngay trong PrepareQuery.</item>
/// <item>Hết lượt xuyên, hết thời gian, hết quãng hoặc về tới tay thì đạn trả vỏ về pool; kết thúc tự nhiên thì chạy
/// tác động "khi hết đường bay" (<see cref="ProjectileTraits.OnExpire"/>). <c>OnDisable</c> xóa toàn bộ trạng thái.</item>
/// </list>
/// Không đổi tên class này: prefab đạn tham chiếu script qua GUID trong file .meta.
/// </remarks>
public sealed class ProjectileView : MonoBehaviour
{
    #region Services

    // Gán ở Launch đầu tiên và giữ lại qua các lượt bay, vì mọi viên đạn dùng chung một context.
    private ProjectileContext context;

    #endregion

    #region Flight State (reset mỗi lượt bay)

    private const int HitMemorySize = 24; // Số mục tiêu nhớ để đạn xuyên không đánh lại; vòng tròn, cũ nhất bị ghi đè.

    private MovementHandle movementHandle; // Đăng ký di chuyển của lượt bay hiện tại.
    private ProjectileMoveState state; // Vị trí/hướng/mục tiêu/quãng bay; quỹ đạo đọc và sửa mỗi frame.
    private HitPayload hit; // Thông tin đòn của bên bắn, chốt lúc bắn; sát thương tính lúc trúng.
    private IProjectileMove projectileMovement; // Strategy quyết định hướng bay.
    private ProjectileTraits traits; // Xuyên, kích thước, tác động phụ.
    private ICharacterSearch search; // Cho đạn xuyên/nảy và tác động phụ.
    private float movementSpeed; // World unit mỗi giây.
    private float hitRadius; // Bán kính quét va chạm (đã nhân kích thước).
    private Vector2 hitOffset; // Offset của hitbox theo hướng bay, không phụ thuộc scale của hình ảnh.
    private float lifetime; // Thời gian bay còn lại (giây).
    private float pulseTimer; // Đếm nhịp tác động khi đang bay.
    private int pierceLeft; // -1 = xuyên tất cả.
    private EffectType impactEffect; // VFX trúng đòn (chỉ là hình ảnh, không phải effect gameplay).
    private bool isFlying; // Chặn cập nhật hoặc gây sát thương lặp sau khi đã kết thúc.
    private readonly CharacterHandle[] hitMemory = new CharacterHandle[HitMemorySize];
    private int hitCount, hitCursor;
    private Func<CharacterHandle, bool> alreadyHit; // Tạo một lần: quỹ đạo nảy hỏi "đã trúng chưa" mà không cấp phát.

    // Buffer quét của đạn tự quét: dùng chung (main thread, không lồng vì chỉ đọc trong một lời gọi).
    private static CharacterHandle[] sweepCandidates = new CharacterHandle[32];
    private static readonly CharacterHandle[] sweepTargets = new CharacterHandle[32];
    private static readonly float[] sweepFractions = new float[32];

    #endregion

    #region Visual

    private GameObject visualObject; // Visual con thuê riêng từ pool, giống cơ chế của CharacterView.
    private readonly List<Renderer> visualRenderers = new List<Renderer>(8);
    private float spin; // Góc xoay hiện tại của visual (rìu/khiên ném).

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
            for (int index = 0; index < visualRenderers.Count; index++)
            {
                Renderer renderer = visualRenderers[index];
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

    private void Awake() => alreadyHit = Remembers;

    /// <summary>
    /// Bắt đầu một lượt bay. <see cref="ProjectileSystem.SpawnBullet"/> gọi ngay sau khi thuê vỏ (vỏ đã bật),
    /// trong cùng lời gọi nên không có frame nào thấy vỏ chưa có dữ liệu. Lỗi giữa chừng thì ProjectileSystem trả vỏ ngay.
    /// </summary>
    internal void Launch(
        ProjectileContext projectileContext,
        CombatAssetsSO.BulletVisual bulletVisual,
        in ProjectileSpawnData projectileData,
        Vector2 direction,
        CharacterHandle target)
    {
        context = projectileContext;
        ClearVisual();
        alreadyHit ??= Remembers;

        traits = projectileData.Traits;
        search = projectileData.Search;
        state = new ProjectileMoveState
        {
            Position = transform.position,
            Direction = direction.normalized,
            Target = target,
            MaxDistance = projectileData.MaxDistance,
        };
        hit = projectileData.Hit;
        projectileMovement = projectileData.Movement;
        movementSpeed = projectileData.Speed;
        lifetime = projectileData.Lifetime;
        hitRadius = bulletVisual.HitRadius * traits.Size;
        hitOffset = bulletVisual.HitOffset * traits.Size;
        impactEffect = bulletVisual.ImpactEffect;
        pierceLeft = traits.Pierce;
        pulseTimer = 0f;
        hitCount = hitCursor = 0;
        spin = 0f;
        FaceFlightDirection();

        // Gắn visual con và giữ đúng transform local đã cấu hình trong prefab visual. Vỏ đang bật nên visual bật ngay
        // khi được gắn (OnEnable của visual phát hạt): truyền sẵn vị trí/góc thế giới đúng để hạt đầu tiên không lệch.
        GameObject prefab = bulletVisual.VisualPrefab;
        visualObject = context.Pools.Spawn(prefab, transform.TransformPoint(prefab.transform.localPosition),
            transform.rotation * prefab.transform.localRotation, PoolGroup.Other, transform);
        if (visualObject == null) throw new InvalidOperationException("Could not spawn projectile visual.");
        visualObject.transform.localPosition = prefab.transform.localPosition;
        visualObject.transform.localRotation = prefab.transform.localRotation;
        visualObject.transform.localScale = prefab.transform.localScale;
        visualObject.GetComponentsInChildren(true, visualRenderers);

        isFlying = true;
        movementHandle = context.MovementRegistration.Register(transform);
        context.Projectiles.Register(this, movementHandle);
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        // context null nghĩa là vỏ prewarm chưa bay lần nào, nên không có gì để gỡ đăng ký.
        context?.Projectiles.Unregister(movementHandle);
        context?.MovementRegistration.Unregister(movementHandle);

        movementHandle = default;
        isFlying = false;
        projectileMovement = null;
        traits = null;
        search = null;
        state = default;
        hit = default;
        impactEffect = EffectType.None;
        hitOffset = default;
        movementSpeed = hitRadius = lifetime = pulseTimer = spin = 0f;
        pierceLeft = 0;
        Array.Clear(hitMemory, 0, hitMemory.Length);
        hitCount = hitCursor = 0;
    }

    #endregion

    #region Per-Frame Flight (ProjectileSystem gọi)

    /// <summary>
    /// Bước 2 (sau AdvanceLifetime): cập nhật quỹ đạo, chạy tác động theo nhịp, rồi:
    /// đạn thường tạo query quét theo lô (trả true); đạn xuyên/nảy tự quét và gây đòn ngay (trả false).
    /// Được gọi trước MovementSystem.LateTick; hàm này không tự đổi vị trí.
    /// </summary>
    internal bool PrepareQuery(float deltaTime, out SpatialQuery query)
    {
        query = default;
        if (!isFlying) return false;

        state.Position = transform.position;
        state.HasOwner = hit.Attacker.TryGetCharacter(out CharacterView owner);
        if (state.HasOwner) state.OwnerPosition = owner.HitPosition;
        bool wasReturning = state.Returning;
        projectileMovement.Advance(ref state);
        if (state.Returning && !wasReturning) ForgetHits(); // Lượt về đánh lại được quái đã trúng ở lượt đi.
        if (state.Finished || (!state.Returning && state.Travelled >= state.MaxDistance))
        {
            Expire();
            return false;
        }
        FaceFlightDirection();
        Spin(deltaTime);

        if (traits.PulseInterval > 0f && !RunPulses(deltaTime)) return false;

        float travelDistance = movementSpeed * deltaTime;
        if (!state.Returning && state.Travelled + travelDistance > state.MaxDistance)
            travelDistance = Mathf.Max(0f, state.MaxDistance - state.Travelled);
        if (!traits.HitsEnemies) return Move(travelDistance, deltaTime);
        if (traits.Pierce == 0 && !(projectileMovement is RicochetMove) && !state.Returning)
        {
            // Đạn thường: gom vào job quét chung với mọi viên khác.
            CharacterTeam opponentTeam = CharacterSearch.OpponentOf(hit.Team);
            Vector2 origin = HitboxCenter;
            query = context.QueryBuilder.Sweep(origin, origin + state.Direction * travelDistance, hitRadius, opponentTeam);
            CharacterSearch.DrawSearchPath(origin, hitRadius, state.Direction, travelDistance);
            state.Travelled += travelDistance;
            return true;
        }
        SweepAll(travelDistance);
        if (isFlying) Move(travelDistance, deltaTime);
        return false;
    }

    /// <summary>Bước 3 (đạn thường): nhận kết quả quét. Trúng thì gây đòn; không trúng thì bay tiếp.</summary>
    internal void CompleteQuery(CharacterHandle hitTarget, Vector2 hitPosition)
    {
        if (!isFlying) return;
        if (hitTarget.TryGetCharacter(out _)) HandleHit(hitTarget, hitPosition);
        if (isFlying) context.MovementCommand.SetMovement(movementHandle, state.Direction, movementSpeed);
    }

    /// <summary>Bước 1: trừ tuổi thọ. Hết tuổi thọ thì kết thúc (chạy tác động hết đường bay) và trả về false.</summary>
    internal bool AdvanceLifetime(float deltaTime)
    {
        if (!isFlying) return false;
        lifetime = Mathf.Max(0f, lifetime - deltaTime);
        if (lifetime > 0f) return true;
        Expire();
        return false;
    }

    // Đặt lệnh di chuyển cho frame và cộng quãng đã bay. Luôn trả false (không có query theo lô).
    private bool Move(float travelDistance, float deltaTime)
    {
        float speed = deltaTime > 0f ? travelDistance / deltaTime : movementSpeed;
        context.MovementCommand.SetMovement(movementHandle, state.Direction, speed);
        state.Travelled += travelDistance;
        return false;
    }

    // Chạy tác động theo nhịp; false nếu đạn đã bị thu hồi trong callback.
    private bool RunPulses(float deltaTime)
    {
        pulseTimer += deltaTime;
        MovementHandle registration = movementHandle;
        while (pulseTimer >= traits.PulseInterval)
        {
            pulseTimer -= traits.PulseInterval;
            HitPayload payload = hit;
            var args = new ImpactArgs(context.Impacts, search, transform.position, state.Direction, in payload);
            SkillImpacts.Run(traits.OnPulse, in args);
            if (!isFlying || !context.MovementRegistration.IsRegistered(registration)) return false;
        }
        return true;
    }

    #endregion

    #region Sweep (đạn xuyên / nảy)

    // Tìm mọi đối thủ trên đoạn bay của frame (chưa trúng), gây đòn theo thứ tự gần trước. Dừng khi đạn kết thúc.
    private void SweepAll(float travelDistance)
    {
        if (search == null) return;
        Vector2 start = HitboxCenter;
        Vector2 end = start + state.Direction * travelDistance;
        float queryRadius = travelDistance * 0.5f + hitRadius + 0.3f; // + nửa hitbox nhân vật lớn nhất.
        int found = search.FindAll((start + end) * 0.5f, queryRadius, CharacterSearch.OpponentOf(hit.Team), ref sweepCandidates);
        int count = 0;
        for (int index = 0; index < found && count < sweepTargets.Length; index++)
        {
            CharacterHandle candidate = sweepCandidates[index];
            if (Remembers(candidate) || !candidate.TryGetCharacter(out CharacterView victim)) continue;
            if (!SpatialGeometry.SweepCircle(new float2(start.x, start.y), new float2(end.x, end.y), hitRadius,
                victim.GetSpatialEntity(), out float fraction)) continue;
            // Chèn theo thứ tự quãng (gần trước), số phần tử nhỏ nên chèn tuyến tính.
            int slot = count++;
            while (slot > 0 && sweepFractions[slot - 1] > fraction)
            {
                sweepFractions[slot] = sweepFractions[slot - 1];
                sweepTargets[slot] = sweepTargets[slot - 1];
                slot--;
            }
            sweepFractions[slot] = fraction;
            sweepTargets[slot] = candidate;
        }
        Array.Clear(sweepCandidates, 0, found);
        for (int index = 0; index < count && isFlying; index++)
        {
            CharacterHandle target = sweepTargets[index];
            Vector2 point = Vector2.Lerp(start, end, sweepFractions[index]);
            sweepTargets[index] = default;
            if (target.IsAlive) HandleHit(target, point);
        }
        for (int index = 0; index < count; index++) sweepTargets[index] = default;
    }

    #endregion

    #region Hit And Release

    // Một lần trúng: gây đòn → tác động khi trúng → hỏi quỹ đạo → trừ lượt xuyên. Hết lượt thì kết thúc (không chạy tác động hết đường).
    private void HandleHit(CharacterHandle hitTarget, Vector2 hitPosition)
    {
        // Callback sát thương có thể giết mục tiêu, kéo theo despawn hoặc spawn lại, và có thể tái sử dụng
        // chính viên đạn này. Vì vậy chốt mọi dữ liệu của lượt trúng TRƯỚC khi gọi ra ngoài.
        MovementHandle registration = movementHandle;
        HitPayload payload = hit;
        ProjectileTraits flightTraits = traits;
        ICharacterSearch flightSearch = search;
        Vector2 direction = state.Direction;
        Vector3 point = new Vector3(hitPosition.x, hitPosition.y, transform.position.z);
        Remember(hitTarget);

        context.Hits.Apply(hitTarget, in payload, point, impactEffect, flightTraits.PushAlongFlight ? direction : Vector2.zero);
        if (flightTraits.OnHit.Count > 0)
        {
            var args = new ImpactArgs(context.Impacts, flightSearch, hitPosition, direction, in payload, hitTarget);
            SkillImpacts.Run(flightTraits.OnHit, in args);
        }
        if (!isFlying || !context.MovementRegistration.IsRegistered(registration)) return;

        state.Position = hitPosition;
        bool continues = projectileMovement.OnHit(ref state, hitTarget, search, CharacterSearch.OpponentOf(hit.Team), alreadyHit);
        if (pierceLeft < 0) return;
        if (pierceLeft > 0) { pierceLeft--; return; }
        if (continues) return;
        isFlying = false;
        context.Projectiles.Release(this);
    }

    // Kết thúc tự nhiên (hết giờ, hết quãng, về tới tay): chạy tác động hết đường bay rồi trả vỏ.
    private void Expire()
    {
        if (!isFlying) return;
        isFlying = false;
        ProjectileTraits flightTraits = traits;
        HitPayload payload = hit;
        ICharacterSearch flightSearch = search;
        Vector2 position = transform.position;
        Vector2 direction = state.Direction;
        context.Projectiles.Release(this);
        if (flightTraits != null && flightTraits.OnExpire.Count > 0)
        {
            var args = new ImpactArgs(context.Impacts, flightSearch, position, direction, in payload);
            SkillImpacts.Run(flightTraits.OnExpire, in args);
        }
    }

    /// <summary>Kết thúc lượt bay và trả vỏ về pool, không chạy tác động. ProjectileSystem gọi khi dọn đạn hàng loạt / ra khỏi camera.</summary>
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
        visualRenderers.Clear();
        if (previous != null) context.Pools.Despawn(previous);
    }

    #endregion

    #region Hit Memory

    private void Remember(CharacterHandle target)
    {
        hitMemory[hitCursor] = target;
        hitCursor = (hitCursor + 1) % HitMemorySize;
        if (hitCount < HitMemorySize) hitCount++;
    }

    private bool Remembers(CharacterHandle target)
    {
        for (int index = 0; index < hitCount; index++)
            if (hitMemory[index].Equals(target)) return true;
        return false;
    }

    private void ForgetHits()
    {
        Array.Clear(hitMemory, 0, hitMemory.Length);
        hitCount = hitCursor = 0;
    }

    #endregion

    #region Helpers

    private void FaceFlightDirection()
        => transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(state.Direction.y, state.Direction.x) * Mathf.Rad2Deg);

    // Xoay visual con quanh trục Z (rìu, khiên); giữ góc riêng vì vỏ quay theo hướng bay mỗi frame.
    private void Spin(float deltaTime)
    {
        if (traits.SpinDegreesPerSecond == 0f || visualObject == null) return;
        spin = Mathf.Repeat(spin + traits.SpinDegreesPerSecond * deltaTime, 360f);
        visualObject.transform.rotation = Quaternion.Euler(0f, 0f, spin);
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
