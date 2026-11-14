using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Phát trực tiếp clip một lần, không cần AnimatorController hoặc timer ước lượng.
[RequireComponent(typeof(Animator))]
public sealed class AnimationEffectVisual : MonoBehaviour, IEffectVisual, Zenject.ILateTickable
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
        tickables.Value.AddLate(this, 500);
        registeredForTicks = true;
    }

    private void StopFrameUpdates()
    {
        if (!registeredForTicks) return;
        tickables.Value.RemoveLate(this);
        registeredForTicks = false;
    }

    private void Start() => StartFrameUpdates();

    private void OnEnable() => activationFrame = Time.frameCount;

    #endregion

    #region Playback & Completion
    [SerializeField] private AnimationClip clip;
    [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
    private Animator animator;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private bool playing;
    public event Action Completed;

    public void SetVisual(AnimationClip animationClip)
    {
        StopAndReset();
        clip = animationClip;
    }

    public void Play()
    {
        StopAndReset();
        if (clip == null || !(clip.length > 0f))
            throw new InvalidOperationException("Assign a non-empty animation clip to AnimationEffectVisual.");
        if (!(playbackSpeed > 0f) || float.IsInfinity(playbackSpeed))
            throw new InvalidOperationException("Animation playback speed must be positive and finite.");
        if (animator == null) animator = GetComponent<Animator>();
        try
        {
            graph = PlayableGraph.Create("EffectAnimation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetDuration(clip.length);
            playable.SetSpeed(playbackSpeed);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Visual", animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            graph.Evaluate(0f); // Lần lấy từ pool luôn bắt đầu ở frame đầu tiên.
            playing = true;
        }
        catch { StopAndReset(); throw; }
    }

    public void LateTick()
    {
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return;
        if (!playing || !graph.IsValid() || playable.GetTime() < clip.length) return;
        playing = false;
        graph.Stop();
        Completed?.Invoke();
    }

    // Hủy/cleanup không được phát Completed.
    public void StopAndReset()
    {
        playing = false;
        if (graph.IsValid()) graph.Destroy();
        graph = default;
        playable = default;
    }

    private void OnDisable()
    {
        StopAndReset();
    }
    private void OnDestroy() { StopFrameUpdates(); OnDisable(); }
    #endregion
}
