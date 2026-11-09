using System;
using PrimeTween;
using UnityEngine;

// State trình diễn của đúng một lượt spawn enemy. CharacterView sở hữu và dọn trước khi trả visual về pool.
public sealed class EnemyCombatFeedback : IDisposable
{
    #region Inspector Settings

    [Serializable]
    public sealed class Settings
    {
        public Color hitColor = new Color(1f, 0.2f, 0.2f, 1f); // Nhân với màu gốc, giữ alpha của từng sprite.
        [Min(0.01f)] public float hitDuration = 0.12f; // Nhận đòn liên tiếp sẽ bắt đầu lại nhịp nháy.
    }

    #endregion

    #region Spawn Identity And Original Presentation

    private readonly CharacterView owner;
    private readonly CharacterRuntimeData identity;
    private readonly ulong generation;
    private readonly Transform visual;
    private readonly SpriteRenderer[] sprites;
    private readonly Color[] originalColors;
    private readonly Animator[] animators;
    private readonly bool[] animatorEnabled;
    private readonly SpriteDeathAnimation deathAnimation; // Frame và tốc độ thuộc prefab visual của từng loại quái.
    private readonly Settings settings;

    #endregion

    #region Tween Ownership

    private Tween flashTimer;
    private Action deathCompleted;
    private bool dying;
    private bool disposed;

    #endregion

    #region Initialization

    public EnemyCombatFeedback(CharacterView owner, Transform visual, SpriteRenderer[] sprites,
        Animator[] animators, Settings settings, SpriteDeathAnimation deathAnimation = null)
    {
        this.owner = owner;
        identity = owner.RuntimeData;
        generation = identity.Generation;
        this.visual = visual;
        this.sprites = sprites;
        this.animators = animators;
        this.settings = settings;
        this.deathAnimation = deathAnimation;
        originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) originalColors[i] = sprites[i].color;
        animatorEnabled = new bool[animators.Length];
        for (int i = 0; i < animators.Length; i++) animatorEnabled[i] = animators[i].enabled;
    }

    #endregion

    #region Accepted Damage Flash

    // Kể cả đòn chỉ làm mất giáp cũng có feedback. Không thay đổi màu gốc đã cache khi nhận hit mới.
    public void Flash()
    {
        if (!IsCurrent() || dying) return;
        flashTimer.Stop();
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            Color tint = originalColors[i] * settings.hitColor;
            tint.a = originalColors[i].a;
            sprites[i].color = tint;
        }
        flashTimer = Tween.Delay(this, Mathf.Max(0.01f, settings.hitDuration), feedback =>
        {
            if (feedback.IsCurrent()) feedback.RestoreColors();
        });
    }

    private void RestoreColors()
    {
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null) sprites[i].color = originalColors[i];
    }

    #endregion

    #region Sprite Death Animation

    // Gọi sau khi gameplay đã xử lý chết; không lặp phần thưởng, không giữ mục tiêu sống thêm.
    // Enemy ngoài camera được thu hồi ngay, không bật visual đang ẩn chỉ để phát animation chết.
    public bool PlayDeath(Action onComplete)
    {
        if (!IsCurrent() || dying || !owner.isActiveAndEnabled || !owner.IsVisualVisible || deathAnimation == null)
            return false;
        dying = true;
        deathCompleted = onComplete;
        foreach (Animator animator in animators)
            if (animator != null) animator.enabled = false; // Tránh Animator đi bộ ghi đè sprite chết.
        if (deathAnimation.Play(owner.Timers, CompleteDeath)) return true;
        deathCompleted = null;
        dying = false;
        RestoreAnimators();
        return false; // Prefab chưa có frame hợp lệ thì controller thu hồi ngay.
    }

    private void CompleteDeath()
    {
        Action callback = deathCompleted;
        deathCompleted = null;
        if (IsCurrent()) callback?.Invoke(); // Callback thu hồi đúng vỏ/runtime của lượt spawn này.
    }

    // Tắt tạm hierarchy không được trả pool bên trong OnDisable; giữ lịch chết để tiếp tục khi bật lại.
    public void SetPaused(bool paused)
    {
        deathAnimation?.SetPaused(paused);
        if (!paused) return;
        flashTimer.Stop();
        RestoreColors();
    }

    #endregion

    #region Cleanup Before Pool Return

    private bool IsCurrent() => !disposed && owner != null && visual != null &&
        ReferenceEquals(owner.RuntimeData, identity) && identity.Generation == generation;

    private void RestoreAnimators()
    {
        for (int i = 0; i < animators.Length; i++)
            if (animators[i] != null) animators[i].enabled = animatorEnabled[i];
    }

    // Dừng callback trước khi trả object pool; hoàn nguyên màu/sprite/Animator cho lần spawn sau.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        deathCompleted = null;
        flashTimer.Stop();
        deathAnimation?.StopAndReset();
        RestoreColors();
        RestoreAnimators();
    }

    #endregion
}
