using UnityEngine;

// Shared shell: all element/flight/hit settings come from runtime data before activation.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ProjectileView : MonoBehaviour
{
    private ProjectileService service;
    public ProjectileRuntimeData RuntimeData { get; private set; }
    private bool flying;

    internal void Initialize(ProjectileService owner, ProjectileRuntimeData runtime)
    {
        service = owner;
        RuntimeData = runtime;
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        sprite.sprite = runtime.Data.Sprite;
        sprite.color = runtime.Data.Color;
        transform.localScale = Vector3.one * (runtime.Data.VisualWidth / sprite.sprite.bounds.size.x);
        flying = true;
        FaceFlightDirection();
    }

    private void Update()
    {
        if (!flying || RuntimeData == null || Time.deltaTime <= 0f) return;
        RuntimeData.Age += Time.deltaTime;
        if (RuntimeData.Age >= RuntimeData.Data.Lifetime)
        {
            flying = false;
            service.Release(this);
            return;
        }
        RuntimeData.Direction = RuntimeData.Flight.GetDirection(RuntimeData, transform.position);
        FaceFlightDirection();
        if (TryHit()) return;
        float radius = RuntimeData.Data.HitRadius;
        // Bound queries during a hitch. Every substep checks the actual hit geometry.
        float distance = Mathf.Min(RuntimeData.Data.Speed * Time.deltaTime, radius * 32f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / radius));
        Vector3 step = (Vector3)(RuntimeData.Direction * (distance / steps));
        for (int i = 0; i < steps; i++)
        {
            transform.position += step;
            if (TryHit()) return;
        }
    }

    private bool TryHit()
    {
        CharacterTarget target = CharacterSearch.FindNearest(transform.position,
            RuntimeData.Data.HitRadius, CharacterSearch.OpponentOf(RuntimeData.Damage.SourceTeam));
        if (!target.TryGetCharacter(out _)) return false;
        flying = false;
        ProjectileRuntimeData runtime = RuntimeData;
        ProjectileService owner = service;
        Vector3 position = transform.position;
        try
        {
            if (runtime.Hit.Apply(runtime, target)) owner.PlayImpact(runtime.Data.ImpactPrefab, position);
        }
        finally { owner.Release(this); }
        return true;
    }
    private void FaceFlightDirection()
    {
        Vector2 direction = RuntimeData.Direction;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }
    private void OnDisable()
    {
        flying = false;
        RuntimeData = null;
        service = null;
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (RuntimeData == null) return;
        using (new UnityEditor.Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, RuntimeData.Data.HitRadius);
    }
#endif
}
