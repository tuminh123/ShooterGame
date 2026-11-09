using System;
using PrimeTween;
using UnityEngine;

// Vòng đời chung của tween popup. UI sở hữu nội dung, input, pause và quyết định tắt GameObject.
// Các lớp con chỉ chọn một khung hoặc nhiều phần tử và khoảng cách phát giữa chúng.
public abstract class PopupTweenBase : MonoBehaviour
{
    #region Shared Scale Settings

    [SerializeField, Range(0f, 1f)] private float startScale = 0f; // Tỷ lệ so với scale gốc khi bắt đầu mở.
    [SerializeField, Min(1f)] private float peakScale = 1.08f; // Mức phóng vượt trước khi trở về hình dạng gốc.
    [SerializeField, Min(0.01f)] private float growDuration = 0.22f; // Giây thực để phóng lên peakScale.
    [SerializeField, Min(0.01f)] private float settleDuration = 0.12f; // Giây thực để thu từ đỉnh về scale gốc.
    [SerializeField, Min(0.01f)] private float closeDuration = 0.16f; // Giây thực để thu mỗi phần tử về 0.

    #endregion

    #region Cached Targets And Playback State

    private Transform[] targets;
    private Vector3[] originalScales; // Giữ cả tỷ lệ từng trục, không giả định prefab có scale bằng 1.
    private Sequence sequence; // Chỉ dừng sequence do component này tạo, không dừng tween của hệ khác.
    private Action completion; // Xóa khi dừng/đổi chiều để callback đóng cũ không ẩn popup vừa mở lại.

    public bool IsPlaying => sequence.isAlive;
    protected bool IsInitialized => targets != null;
    protected int TargetCount => targets != null ? targets.Length : 0;

    #endregion

    #region Public Animation Contract

    // Trả tổng thời gian thực để UI khóa input đến khi trình bày hoàn tất.
    // UI bật popup trước khi gọi PlayOpen; component này không tự mở các panel khác.
    public abstract float PlayOpen(Action onComplete = null);

    // Thu nhỏ từ scale hiện tại rồi gọi callback. UI có thể tắt panel trong callback này.
    // Gọi mở/đóng mới sẽ hủy callback của lần cũ; không gọi SetActive(false) trước khi tween đóng xong.
    public abstract float PlayClose(Action onComplete = null);

    #endregion

    #region Target Initialization

    // Chỉ chụp scale gốc khi gán reference, không chụp lại giữa một animation đang co/phóng.
    // Sao chép danh sách để bên gọi không đổi mục tiêu trong lúc sequence đang chạy.
    protected void InitializeTargets(Transform[] items)
    {
        if (items == null || items.Length == 0)
            throw new ArgumentException("Assign at least one popup tween target.", nameof(items));
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) throw new ArgumentException("Popup tween targets cannot be null.", nameof(items));
            for (int j = 0; j < i; j++)
                if (items[i] == items[j]) throw new ArgumentException("Popup tween targets must be unique.", nameof(items));
        }
        StopAndReset();
        targets = (Transform[])items.Clone();
        originalScales = new Vector3[targets.Length];
        for (int i = 0; i < targets.Length; i++) originalScales[i] = targets[i].localScale;
    }

    #endregion

    #region Shared Open Animation

    // stagger = 0 cho một khung; lớp nhiều phần tử truyền khoảng lệch bắt đầu giữa các phần tử.
    protected float OpenTargets(int count, float stagger, Action onComplete)
    {
        bool interrupted = IsPlaying;
        StopSequence();
        count = Mathf.Clamp(count, 0, TargetCount);
        if (!isActiveAndEnabled || count == 0) { onComplete?.Invoke(); return 0f; }

        // Lần mở bình thường bắt đầu nhỏ. Nếu đang tween thì giữ scale hiện tại để đổi chiều mượt.
        if (!interrupted)
        {
            ResetScales();
            for (int i = 0; i < count; i++) targets[i].localScale = originalScales[i] * startScale;
        }
        sequence = Sequence.Create(useUnscaledTime: true); // Hoạt động kể cả khi game đang pause.
        for (int i = 0; i < count; i++)
        {
            Transform item = targets[i];
            Vector3 original = originalScales[i];
            float delay = i * stagger;
            sequence.Insert(delay, Tween.Scale(item, item.localScale, original * peakScale,
                growDuration, Ease.OutCubic));
            sequence.Insert(delay + growDuration, Tween.Scale(item, original * peakScale, original,
                settleDuration, Ease.OutQuad));
        }
        RegisterCompletion(onComplete);
        return (count - 1) * stagger + growDuration + settleDuration;
    }

    #endregion

    #region Shared Close Animation

    // Chụp scale đang hiển thị sau khi dừng lịch cũ; đóng giữa lúc mở không bị nhảy về scale gốc.
    protected float CloseTargets(int count, float stagger, Action onComplete)
    {
        StopSequence();
        count = Mathf.Clamp(count, 0, TargetCount);
        if (!isActiveAndEnabled || count == 0) { onComplete?.Invoke(); return 0f; }

        sequence = Sequence.Create(useUnscaledTime: true);
        for (int i = 0; i < count; i++)
            sequence.Insert(i * stagger, Tween.Scale(targets[i], targets[i].localScale,
                Vector3.zero, closeDuration, Ease.InQuad));
        RegisterCompletion(onComplete);
        return (count - 1) * stagger + closeDuration;
    }

    #endregion

    #region Completion And Cancellation

    // Sequence gọi một lần sau khi mọi phần tử hoàn tất, thay vì gọi callback theo từng phần tử.
    private void RegisterCompletion(Action onComplete)
    {
        completion = onComplete;
        sequence.OnComplete(this, popup => popup.CompletePlayback());
    }

    private void CompletePlayback()
    {
        Action callback = completion;
        completion = null;
        sequence = default;
        callback?.Invoke(); // Callback được phép tắt popup hoặc bắt đầu một tween mới.
    }

    private void StopSequence()
    {
        completion = null;
        sequence.Stop(); // PrimeTween.Stop không phát callback hoàn tất.
        sequence = default;
    }

    #endregion

    #region Scale Restoration And Unity Cleanup

    private void ResetScales()
    {
        for (int i = 0; i < TargetCount; i++)
            if (targets[i] != null) targets[i].localScale = originalScales[i];
    }

    // Dùng khi đóng tức thời/reset UI; không chạy callback đóng đang chờ.
    public void StopAndReset()
    {
        StopSequence();
        ResetScales();
    }

    // Tắt/hủy từ ngoài vẫn trả scale gốc, không mang trạng thái thu nhỏ sang lần dùng kế tiếp.
    protected virtual void OnDisable() => StopAndReset();
    protected virtual void OnDestroy() => StopAndReset();

    #endregion
}
