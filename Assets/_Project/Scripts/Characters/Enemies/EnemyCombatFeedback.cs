using System;
using UnityEngine;

// State trình diễn của đúng một lượt spawn enemy. CharacterView sở hữu và dọn trước khi trả visual về pool.
public sealed class EnemyCombatFeedback : IDisposable
{
    #region Spawn Identity And Original Presentation

    private readonly CharacterView owner;
    private readonly CharacterRuntimeData identity;
    private readonly ulong generation;
    private readonly Transform visual;
    private readonly Animator[] animators;
    private readonly bool[] animatorEnabled;
    private readonly SpriteDeathAnimation deathAnimation; // Frame và tốc độ thuộc prefab visual của từng loại quái.

    #endregion

    #region Death Playback State

    private Action deathCompleted;
    private bool dying;
    private bool disposed;

    #endregion

    #region Initialization

    public EnemyCombatFeedback(CharacterView owner, Transform visual,
        Animator[] animators, SpriteDeathAnimation deathAnimation = null)
    {
        this.owner = owner;
        identity = owner.RuntimeData;
        generation = identity.Generation;
        this.visual = visual;
        this.animators = animators;
        this.deathAnimation = deathAnimation;
        animatorEnabled = new bool[animators.Length];
        for (int i = 0; i < animators.Length; i++) animatorEnabled[i] = animators[i].enabled;
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

    // Dừng callback trước khi trả object pool; hoàn nguyên sprite/Animator cho lần spawn sau.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        deathCompleted = null;
        deathAnimation?.StopAndReset();
        RestoreAnimators();
    }

    #endregion
}
