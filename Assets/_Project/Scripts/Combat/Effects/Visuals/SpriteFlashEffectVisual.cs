using UnityEngine;

// Giữ hiệu ứng sprite fade/scale của HolyImpact, tách khỏi vỏ EffectView.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFlashEffectVisual : MonoBehaviour, IEffectVisual
{
    #region Settings & State

    [SerializeField, Min(0.01f)] private float fadeDuration = 0.14f; // Thời gian fade; vỏ quản lý despawn.
    [SerializeField, Min(0.01f)] private float visualWidth = 0.16f; // Chiều rộng theo world unit khi scale cha bằng 1.
    private SpriteRenderer sprite; // Sprite của visual con.
    private float age; // Tiến độ trình diễn, không phải thời gian gameplay effect.
    private float baseScale; // Quy đổi kích thước sprite về chiều rộng cấu hình.
    private bool playing; // Chỉ cập nhật sau khi EffectView gọi Play.

    #endregion

    #region Playback

    // Quy đổi chiều rộng sprite sang world unit, bắt đầu ở 60% rồi phóng dần khi fade.
    public void Play()
    {
        StopAndReset();
        baseScale = sprite.sprite != null ? visualWidth / sprite.sprite.bounds.size.x : 1f;
        transform.localScale = Vector3.one * (baseScale * 0.6f);
        playing = true;
    }

    // Update chỉ trình diễn màu/scale; không gây damage và không tự trả visual về pool.
    private void Update()
    {
        if (!playing) return;
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Mathf.Max(0.01f, fadeDuration));
        sprite.color = new Color(1f, 1f, 1f, 1f - progress);
        transform.localScale = Vector3.one * (baseScale * Mathf.Lerp(0.6f, 1.25f, progress));
    }

    #endregion

    #region Reset And Disable Cleanup

    // Xóa tiến độ, màu và scale của lần phát cũ trước khi tái sử dụng.
    public void StopAndReset()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        playing = false;
        age = 0f;
        sprite.color = Color.white;
        transform.localScale = Vector3.one;
    }

    private void OnDisable() => StopAndReset();

    #endregion
}
