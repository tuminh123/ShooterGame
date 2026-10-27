using UnityEngine;

public enum ProjectileFlight
{
    Straight,
    Homing
}

// * Đạn không có collider/Rigidbody: hình tròn query là vùng trúng của đạn.
// * Mục tiêu ban đầu chỉ điều hướng; Character thực sự nằm trong vùng trúng mới nhận damage.
public sealed class ProjectileView : MonoBehaviour
{
    #region Projectile Configuration

    [SerializeField] private ProjectileFlight flight;
    [SerializeField, Min(0.01f)] private float speed = 2.4f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.035f;
    [SerializeField, Min(0.01f)] private float lifetime = 3f;
    [SerializeField, Min(0.01f)] private float visualWidth = 0.18f;
    [SerializeField] private ImpactVfx impactPrefab;

    #endregion

    private ProjectileService service;
    private CharacterTarget target;
    private DamageInfo damage;
    private Vector2 direction;
    private float age;
    private bool flying;

    internal void Initialize(ProjectileService service, CharacterTarget target, DamageInfo damage, Vector2 direction)
    {
        this.service = service;
        this.target = target;
        this.damage = damage;
        this.direction = direction;
        age = 0f;
        flying = true;
        // * Chuẩn hóa kích thước theo world unit, không phụ thuộc độ phân giải ảnh nguồn.
        if (TryGetComponent(out SpriteRenderer sprite) && sprite.sprite != null)
            transform.localScale = Vector3.one * (visualWidth / sprite.sprite.bounds.size.x);
        FaceFlightDirection();
    }

    private void Update()
    {
        if (!flying || Time.deltaTime <= 0f)
            return;
        age += Time.deltaTime;
        if (age >= Mathf.Max(0.01f, lifetime))
        {
            flying = false;
            service.Release(this);
            return;
        }

        if (flight == ProjectileFlight.Homing && target.TryGetCharacter(out CharacterView character))
        {
            Vector2 toTarget = character.HitPosition - transform.position;
            if (toTarget.sqrMagnitude > 0.000001f)
                direction = toTarget.normalized;
        }
        // * Mục tiêu mất/chết/đổi lượt spawn: giữ direction cuối, không tự chọn mục tiêu mới.
        FaceFlightDirection();
        if (TryHit())
            return;

        // * Vẫn dùng các hình tròn tại vị trí, không cast đường bay.
        // * Chia bước dài thành các bước nhỏ để các vùng kiểm tra không bị cách quá xa khi FPS giảm.
        // * Trần bước tránh đột biến query khi hitch; frame quá dài làm đạn tiến ít hơn thay vì xuyên mục tiêu.
        float radius = Mathf.Max(0.01f, hitRadius);
        float distance = Mathf.Min(Mathf.Max(0f, speed) * Time.deltaTime, radius * 32f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / radius));
        Vector3 step = (Vector3)(direction * (distance / steps));
        for (int i = 0; i < steps; i++)
        {
            transform.position += step;
            if (TryHit())
                return;
        }
    }

    private bool TryHit()
    {
        CharacterTarget contact = CharacterSearch.FindNearest(transform.position,
            Mathf.Max(0.01f, hitRadius), CharacterSearch.OpponentOf(damage.SourceTeam));
        if (!contact.TryGetCharacter(out CharacterView character))
            return false;

        // * Đóng lượt đạn trước callback: không gây damage lặp dù callback tạo đòn đánh mới.
        flying = false;
        Vector3 hitPosition = transform.position;
        ProjectileService owner = service;
        ImpactVfx impact = impactPrefab;
        try
        {
            DamageResult result = character.ReceiveDamage(damage);
            if (result.Accepted)
                owner.PlayImpact(impact, hitPosition);
        }
        finally
        {
            owner.Release(this);
        }
        return true;
    }

    private void FaceFlightDirection()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        flying = false;
        target = default;
        damage = default;
        service = null;
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        using (new UnityEditor.Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, Mathf.Max(0.01f, hitRadius));
    }
    #endif
}
