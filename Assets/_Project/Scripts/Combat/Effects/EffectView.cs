using UnityEngine;

// * Flash trúng đòn ngắn, chỉ thay đổi hình ảnh; không mang collider hoặc gây damage.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class EffectView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.14f;
    [SerializeField, Min(0.01f)] private float visualWidth = 0.16f;
    private CombatSpawner spawner;
    private SpriteRenderer sprite;
    private float age;
    private float baseScale;

    internal void Initialize(CombatSpawner spawner)
    {
        this.spawner = spawner;
        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();
        age = 0f;
        sprite.color = Color.white;
        baseScale = sprite.sprite != null ? visualWidth / sprite.sprite.bounds.size.x : 1f;
        transform.localScale = Vector3.one * baseScale;
    }

    private void Update()
    {
        if (spawner == null || Time.deltaTime <= 0f)
            return;
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));
        sprite.color = new Color(1f, 1f, 1f, 1f - progress);
        transform.localScale = Vector3.one * baseScale * Mathf.Lerp(0.6f, 1.25f, progress);
        if (progress >= 1f)
            spawner.Release(gameObject);
    }

    private void OnDisable() => spawner = null;
}
