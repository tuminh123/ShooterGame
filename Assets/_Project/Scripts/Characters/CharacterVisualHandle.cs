using System;
using UnityEngine;

// Một đầu mối quay mặt và animation trên prefab ngoại hình. Không gây damage hoặc điều khiển movement.
public sealed class CharacterVisualHandle : MonoBehaviour, Zenject.ILateTickable
{
    #region Zenject Tick Registration

    private Zenject.LazyInject<Zenject.TickableManager> tickables;
    private bool registeredForTicks;
    private int activationFrame = -1;

    [Zenject.Inject]
    public void ConstructTickables(Zenject.LazyInject<Zenject.TickableManager> tickables) => this.tickables = tickables;

    // Đăng ký một lần ở Start để tránh vòng phụ thuộc trong lúc Zenject dựng container.
    // Object trong pool vẫn giữ đăng ký; Tick bỏ qua khi tắt hoặc vừa được tái sử dụng.
    private void StartFrameUpdates()
    {
        if (registeredForTicks || tickables == null) return;
        tickables.Value.AddLate(this, 400);
        registeredForTicks = true;
    }

    private void StopFrameUpdates()
    {
        if (!registeredForTicks) return;
        tickables.Value.RemoveLate(this);
        registeredForTicks = false;
    }

    private void Start() => StartFrameUpdates();

    #endregion

    #region Configuration & Playback State
    [Header("References")]
    [SerializeField] private Transform facingRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private bool modelFacesRight = true;

    [Header("Animator States (full paths)")]
    [SerializeField] private string idleState = "Base Layer.Idle";
    [SerializeField] private string moveState = "Base Layer.Walk";
    [SerializeField] private string attackState = "Base Layer.Attack";
    [SerializeField] private string castState = "Base Layer.Skill";
    [SerializeField] private string hitState = "Base Layer.Hit";
    [SerializeField] private string deathState = "Base Layer.Dead";
    [SerializeField] private string winState = "Base Layer.Victory";
    [SerializeField] private string loseState = "Base Layer.Defeat";

    private ICharacterVisualEventSource source;
    private Vector3 originalFacingScale;
    private int idleHash, moveHash, attackHash, castHash, hitHash, deathHash, winHash, loseHash;
    private int playingHash;
    private CharacterVisualState movementState, currentState;
    private bool cached, resetPending, playPending, actionPlaying, terminalPlaying, terminalFinished;
    private float originalAnimatorSpeed;
    private AnimatorUpdateMode originalUpdateMode;
    private Action terminalCompleted;

    #endregion

    #region Binding & Animation Playback

    private void Awake() => CacheConfiguration();

    private void CacheConfiguration()
    {
        if (cached) return;
        if (facingRoot == null || animator == null)
            throw new InvalidOperationException("Assign Facing Root and Animator on CharacterVisualHandle.");
        cached = true;
        originalFacingScale = facingRoot.localScale;
        originalAnimatorSpeed = animator.speed;
        originalUpdateMode = animator.updateMode;
        animator.keepAnimatorStateOnDisable = true;
        idleHash = Hash(idleState); moveHash = Hash(moveState);
        attackHash = Hash(attackState); castHash = Hash(castState);
        hitHash = Hash(hitState); deathHash = Hash(deathState);
        winHash = Hash(winState); loseHash = Hash(loseState);
    }

    private static int Hash(string name) => string.IsNullOrWhiteSpace(name) ? 0 : Animator.StringToHash(name);

    // Capture the scale after CharacterView applies the template's visual settings.
    public void CaptureFacingScale() => originalFacingScale = facingRoot.localScale;

    public void Bind(ICharacterVisualEventSource eventSource)
    {
        Unbind();
        CacheConfiguration();
        source = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
        movementState = source.MovementVisualState;
        currentState = source.IsVisualAlive ? movementState : CharacterVisualState.Death;
        terminalPlaying = !source.IsVisualAlive;
        resetPending = playPending = true;
        source.VisualStateRequested += OnVisualStateRequested;
        source.FacingChanged += OnFacingChanged;
        OnFacingChanged(source.FacingRight);
        ApplyPendingPlayback();
    }

    public void Unbind()
    {
        if (source != null)
        {
            source.VisualStateRequested -= OnVisualStateRequested;
            source.FacingChanged -= OnFacingChanged;
        }
        source = null;
        terminalCompleted = null;
        actionPlaying = terminalPlaying = terminalFinished = resetPending = playPending = false;
        playingHash = 0;
        currentState = movementState = CharacterVisualState.Idle;
        if (cached)
        {
            if (facingRoot != null) facingRoot.localScale = originalFacingScale;
            if (animator != null)
            {
                animator.speed = originalAnimatorSpeed;
                animator.updateMode = originalUpdateMode;
            }
        }
    }

    private void OnFacingChanged(bool facingRight)
    {
        if (terminalPlaying) return;
        Vector3 scale = originalFacingScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight == modelFacesRight ? 1f : -1f);
        facingRoot.localScale = scale;
    }

    private void OnVisualStateRequested(CharacterVisualState state)
    {
        if (terminalPlaying) return;
        switch (state)
        {
            case CharacterVisualState.Idle:
            case CharacterVisualState.Move:
                movementState = state;
                if (actionPlaying) return;
                break;
            case CharacterVisualState.Attack:
            case CharacterVisualState.Cast:
                // Một skill thực thi mới được phép phát lại cùng enum.
                actionPlaying = true;
                break;
            case CharacterVisualState.Hit:
                // Flash màu vẫn chạy; hit không cắt animation tấn công đang phát.
                if (actionPlaying) return;
                actionPlaying = true;
                break;
            case CharacterVisualState.Death:
            case CharacterVisualState.Win:
            case CharacterVisualState.Lose:
                terminalPlaying = true;
                actionPlaying = false;
                terminalFinished = false;
                break;
            default: return;
        }
        currentState = state;
        playPending = true;
        ApplyPendingPlayback();
    }

    private int StateHash(CharacterVisualState state)
    {
        switch (state)
        {
            case CharacterVisualState.Idle: return idleHash;
            case CharacterVisualState.Move: return moveHash;
            case CharacterVisualState.Attack: return attackHash;
            case CharacterVisualState.Cast: return HasState(castHash) ? castHash : attackHash;
            case CharacterVisualState.Hit: return hitHash;
            case CharacterVisualState.Death: return deathHash;
            case CharacterVisualState.Win:
            {
                // Animator thiếu Victory thì giữ nhân vật ở Idle.
                return HasState(winHash) ? winHash : idleHash;
            }
            case CharacterVisualState.Lose: return loseHash;
            default: return 0;
        }
    }

    private bool HasState(int hash) => hash != 0 && animator.runtimeAnimatorController != null && animator.HasState(0, hash);

    private void OnEnable()
    {
        activationFrame = Time.frameCount;
        if (source == null) return;
        OnFacingChanged(source.FacingRight);
        ApplyPendingPlayback();
    }

    private void ApplyPendingPlayback()
    {
        if (source == null || !isActiveAndEnabled || !animator.isActiveAndEnabled) return;
        if (resetPending)
        {
            resetPending = false;
            animator.speed = originalAnimatorSpeed;
            animator.Rebind();
            animator.Update(0f);
        }
        if (!playPending) return;
        playPending = false;
        animator.updateMode = IsResultState(currentState) ? AnimatorUpdateMode.UnscaledTime : originalUpdateMode;
        int hash = StateHash(currentState);
        if (!HasState(hash))
        {
            if (terminalPlaying)
            {
                terminalFinished = true;
                Debug.LogWarning($"Missing Animator state for {currentState} on {name}: {hash}.", this);
                return;
            }
            actionPlaying = false;
            currentState = movementState;
            hash = StateHash(currentState);
            if (!HasState(hash)) hash = idleHash;
            if (!HasState(hash)) return;
        }
        playingHash = hash;
        animator.speed = originalAnimatorSpeed;
        animator.Play(hash, 0, 0f);
    }

    public void LateTick()
    {
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return;
        if (source == null || !animator.isActiveAndEnabled ||
            (!IsResultState(currentState) && Time.deltaTime <= 0f)) return;
        ApplyPendingPlayback();
        if (playingHash == 0 || terminalFinished || (!actionPlaying && !terminalPlaying)) return;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.fullPathHash != playingHash || info.normalizedTime < 1f) return;
        if (terminalPlaying)
        {
            // Death/Win/Lose đều phát đúng một vòng rồi giữ frame cuối dù clip bật Loop.
            terminalFinished = true;
            animator.Play(playingHash, 0, 0.9999f);
            animator.Update(0f);
            animator.speed = 0f;
            Action callback = terminalCompleted;
            terminalCompleted = null;
            callback?.Invoke();
        }
        else
        {
            actionPlaying = false;
            currentState = movementState;
            playPending = true;
            ApplyPendingPlayback();
        }
    }

    // Chỉ báo animation chết hoàn tất; gameplay quyết định game over hoặc despawn.
    public bool WaitForDeathAnimation(Action onComplete)
        => WaitForTerminalAnimation(CharacterVisualState.Death, onComplete);

    public bool WaitForResultAnimation(CharacterVisualState state, Action onComplete)
        => IsResultState(state) && WaitForTerminalAnimation(state, onComplete);

    private static bool IsResultState(CharacterVisualState state)
        => state == CharacterVisualState.Win || state == CharacterVisualState.Lose;

    private bool WaitForTerminalAnimation(CharacterVisualState state, Action onComplete)
    {
        if (source == null || currentState != state || !terminalPlaying || terminalFinished ||
            !isActiveAndEnabled || !animator.isActiveAndEnabled || !HasState(StateHash(state)))
            return false;
        terminalCompleted = onComplete;
        return true;
    }

    private void OnDestroy() { StopFrameUpdates(); Unbind(); }
    #endregion
}
