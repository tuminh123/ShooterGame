using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>VFX phát một animation clip một lần qua PlayableGraph, và báo đã xong khi clip chạy hết.</summary>
/// <remarks>Không cần AnimatorController hay timer ước lượng. Không đổi tên class: prefab tham chiếu script qua GUID.</remarks>
[RequireComponent(typeof(Animator))]
public sealed class AnimationEffectVisual : MonoBehaviour, IEffectVisual
{
    #region Frame

    private int activationFrame = -1; // Bỏ qua frame vừa bật: vỏ pool tái sử dụng không đọc trạng thái cũ.

    private void OnEnable() => activationFrame = Time.frameCount;

    #endregion

    #region Playback & Completion
    [SerializeField] private AnimationClip clip;
    [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
    private Animator animator;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private bool playing;

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

    /// <summary>CombatVfx gọi ở LateTick (qua EffectView): trả về true khi clip đã chạy hết.</summary>
    public bool Advance(float deltaTime)
    {
        if (!playing) return true;
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return false;
        if (graph.IsValid() && playable.GetTime() < clip.length) return false;
        playing = false;
        if (graph.IsValid()) graph.Stop();
        return true;
    }

    // Hủy/cleanup: dừng graph, không coi là phát xong tự nhiên.
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

    private void OnDestroy() => OnDisable();
    #endregion
}
