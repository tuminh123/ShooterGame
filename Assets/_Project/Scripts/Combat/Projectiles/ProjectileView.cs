using UnityEngine;
using Zenject;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class ProjectileView : MonoBehaviour
{
    private MovementManager movementManager; // Đăng ký và thực hiện di chuyển Transform.
    private ProjectileManager projectileManager; // Gọi Tick chung, thay Update trên từng viên.
    private MovementHandle movementHandle; // Đăng ký di chuyển của lượt spawn hiện tại.
    private CombatSpawner spawner; // Trả viên đạn về pool khi trúng hoặc hết tuổi thọ.
    private SpriteRenderer spriteRenderer; // Hiển thị hình ảnh và màu viên đạn.
    private Vector2 flightDirection; // Hướng bay hiện tại, có thể đổi khi đuổi mục tiêu.
    private CharacterTarget homingTarget; // Đúng lượt spawn của mục tiêu, tránh bám object pool đã đổi chủ.
    private DamageInfo damageInfo; // Sát thương và phe được chốt khi tạo đạn.
    private IProjectileMove projectileMovement; // Strategy chỉ tính hướng bay.
    private float movementSpeed; // Tốc độ lấy từ skill, world unit/giây.
    private float hitRadius; // Bán kính kiểm tra trúng quái.
    private float remainingLifetime; // Số giây còn lại trước khi thu hồi.
    private bool isFlying; // Chặn cập nhật hoặc gây sát thương lặp sau khi trúng.

    [Inject]
    public void Construct(MovementManager movementManager, ProjectileManager projectileManager)
    {
        this.movementManager = movementManager;
        this.projectileManager = projectileManager;
    }

    internal void Initialize(CombatSpawner spawner, CombatAssetsSO.BulletVisual bulletVisual,
        ProjectileSpawnData projectileData,
        Vector2 flightDirection, CharacterTarget homingTarget)
    {
        this.spawner = spawner;
        this.flightDirection = flightDirection.normalized;
        this.homingTarget = homingTarget;
        damageInfo = projectileData.Damage;
        projectileMovement = projectileData.Movement;
        movementSpeed = projectileData.Speed;
        hitRadius = projectileData.HitRadius;
        remainingLifetime = projectileData.Lifetime;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = bulletVisual.sprite;
        spriteRenderer.color = bulletVisual.color;
        transform.localScale = Vector3.one * (bulletVisual.visualWidth / spriteRenderer.sprite.bounds.size.x);
        isFlying = true;
        FaceFlightDirection();
    }

    private void OnEnable()
    {
        if (!isFlying) return;
        movementHandle = movementManager.Register(transform);
        projectileManager.Register(this, movementHandle);
    }

    // ProjectileManager gọi trước MovementManager.LateTick; không tự đổi vị trí tại đây.
    internal void Tick(float deltaTime)
    {
        if (!isFlying) return;
        remainingLifetime -= deltaTime;
        if (remainingLifetime <= 0f)
        {
            Release();
            return;
        }

        flightDirection = projectileMovement.GetDirection(transform.position, flightDirection, homingTarget).normalized;
        FaceFlightDirection();

        CharacterTeam opponentTeam = CharacterSearch.OpponentOf(damageInfo.SourceTeam);
        CharacterTarget hitTarget = CharacterSearch.FindNearest(transform.position, hitRadius, opponentTeam);
        if (hitTarget.TryGetCharacter(out _))
        {
            ApplyHitAndRelease(hitTarget);
            return;
        }

        // Quét đoạn sắp đi để đạn nhanh không bỏ qua quái giữa hai frame.
        float travelDistance = movementSpeed * deltaTime;
        hitTarget = CharacterSearch.FindFirstOnPath(transform.position, hitRadius, flightDirection,
            travelDistance, opponentTeam, out _);
        if (hitTarget.TryGetCharacter(out _))
        {
            ApplyHitAndRelease(hitTarget);
            return;
        }

        movementManager.SetMovement(movementHandle, flightDirection, movementSpeed);
    }

    private void ApplyHitAndRelease(CharacterTarget hitTarget)
    {
        isFlying = false;
        CombatSpawner owningSpawner = spawner;
        MovementHandle hitRegistration = movementHandle;
        try
        {
            if (hitTarget.TryGetCharacter(out CharacterView character)) character.ReceiveDamage(damageInfo);
            // TODO: Thêm hiệu ứng trúng đòn qua hệ thống riêng.
        }
        finally
        {
            // Callback có thể despawn/spawn lại object; chỉ thu hồi đúng lượt vừa gây sát thương.
            if (movementManager.IsRegistered(hitRegistration)) owningSpawner.Release(gameObject);
        }
    }

    internal void Release()
    {
        isFlying = false;
        spawner.Release(gameObject);
    }

    private void FaceFlightDirection()
        => transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg);

    private void OnDisable()
    {
        projectileManager?.Unregister(movementHandle);
        movementManager?.Unregister(movementHandle);
        movementHandle = default;
        isFlying = false;
        spawner = null;
        projectileMovement = null;
        homingTarget = default;
        damageInfo = default;
        flightDirection = default;
        movementSpeed = hitRadius = remainingLifetime = 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!isFlying) return;
        using (new UnityEditor.Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, hitRadius);
    }
#endif
}
