using UnityEngine;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine.Serialization;
using Zenject;

// Gắn trên GameplayUI luôn hoạt động; chỉ bật/tắt panel con để controller giữ đúng vòng đời.
public sealed class GameplayController : UIPanelController, ILateTickable
{
    #region Scene References

    [SerializeField] private GameObject panel; // Root HUD con chứa RunUI, nút pause và các thành phần gameplay.
    [Tooltip("Thanh thời gian/vàng/quái trên CanvasDynamic; ẩn cùng HUD khi về Home hoặc Loading.")]
    [SerializeField] private GameObject resourceBar;
    [SerializeField] private PauseUI pauseUI; // Pause thủ công thuộc gameplay; trả token trước khi HUD bị ẩn.

    [Header("Spawn Warning")]
    [Tooltip("Root cảnh báo dùng chung cho wave đồng thời, Elite và Boss; không chặn thao tác.")]
    [SerializeField] private CanvasGroup warningPanel;
    [Tooltip("Phần banner trượt ngang từ trái vào, đứng yên rồi trượt sang phải.")]
    [SerializeField] private RectTransform warningContent;
    [SerializeField] private TMP_Text warningTitle;
    [SerializeField] private TMP_Text warningSubtitle;
    [Tooltip("Hiện cảnh báo sớm hơn mốc spawn bao nhiêu giây. Ví dụ spawn ở giây 10: giá trị 2 báo ở giây 8, giá trị 3 báo ở giây 7. Chỉ áp dụng khi lịch spawn xác định được; không dời mốc sinh quái.")]
    [SerializeField, Min(0f)] private float warningLeadTime = 2f;
    [Tooltip("Số giây banner trượt từ ngoài mép trái về vị trí thiết kế.")]
    [SerializeField, Min(0.01f)] private float warningSlideInDuration = 0.35f;
    [FormerlySerializedAs("warningDuration")]
    [Tooltip("Số giây giữ banner đứng yên, tối thiểu tới mốc spawn đã biết. Pause dừng cả chuyển động và thời gian chờ.")]
    [SerializeField, Min(0f)] private float warningHoldDuration = 2f;
    [Tooltip("Số giây banner trượt sang phải, ra khỏi màn hình rồi ẩn.")]
    [SerializeField, Min(0.01f)] private float warningSlideOutDuration = 0.3f;
    [Tooltip("Khoảng cách bổ sung ngoài mép màn hình, tính theo đơn vị Canvas.")]
    [SerializeField, Min(0f)] private float warningSlidePadding = 48f;

    #endregion

    #region Injection & Frame Registration

    private StageManager stage;
    private LazyInject<TickableManager> tickables;
    private bool registeredForTicks, gameplayVisible, subscribed, warningActive;
    private StageSpawnWarning currentWarning;
    private Sequence warningAnimation;
    private Vector2 warningRestPosition;
    private bool warningPoseCached;
    private struct ScheduledWarning
    {
        public StageSpawnWarning Kind;
        public float SpawnAt;
    }
    private readonly List<ScheduledWarning> pendingWarnings = new List<ScheduledWarning>();

    [Inject]
    public void Construct(StageManager stage, LazyInject<TickableManager> tickables)
    {
        Unsubscribe();
        this.stage = stage;
        this.tickables = tickables;
        ClearWarnings();
        if (isActiveAndEnabled) Subscribe();
    }

    // Đăng ký sau khi container đã dựng xong; toàn bộ hiệu ứng dùng LateUpdate của Zenject.
    private void Start()
    {
        if (registeredForTicks || tickables == null) return;
        tickables.Value.AddLate(this, 700);
        registeredForTicks = true;
    }

    private void Subscribe()
    {
        if (subscribed || stage == null) return;
        stage.SpawnScheduled += OnSpawnScheduled;
        stage.WarningsCleared += ClearWarnings;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || stage == null) return;
        stage.SpawnScheduled -= OnSpawnScheduled;
        stage.WarningsCleared -= ClearWarnings;
        subscribed = false;
    }

    private void OnEnable() => Subscribe();

    private void OnDestroy()
    {
        Unsubscribe();
        ClearWarnings();
        if (registeredForTicks) tickables.Value.RemoveLate(this);
        registeredForTicks = false;
    }

    #endregion

    #region Spawn Warning Presentation

    // UI giữ lịch trình bày riêng; hệ spawn không cần biết lead time hay hiệu ứng banner.
    private void OnSpawnScheduled(StageSpawnWarning kind, float spawnAt)
        => pendingWarnings.Add(new ScheduledWarning { Kind = kind, SpawnAt = spawnAt });

    private void ProcessWarnings(float stageTime)
    {
        for (int i = 0; i < pendingWarnings.Count;)
        {
            var item = pendingWarnings[i];
            if (stageTime < Mathf.Max(0f, item.SpawnAt - Mathf.Max(0f, warningLeadTime))) { i++; continue; }
            if (!gameplayVisible && stageTime < item.SpawnAt) { i++; continue; }
            pendingWarnings.RemoveAt(i);
            if (gameplayVisible) ShowWarning(item.Kind, item.SpawnAt, stageTime);
        }
    }

    // Boss ưu tiên hơn Elite và wave; không đẩy ngược thời điểm quái xuất hiện.
    private void ShowWarning(StageSpawnWarning kind, float spawnAt, float stageTime)
    {
        if (!isActiveAndEnabled || !gameplayVisible || warningPanel == null || warningContent == null) return;
        if (warningActive && kind < currentWarning) return;
        ClearWarning();
        if (!warningPoseCached)
        {
            warningRestPosition = warningContent.anchoredPosition;
            warningPoseCached = true;
        }
        currentWarning = kind;
        warningActive = true;
        if (warningTitle != null)
            warningTitle.text = kind == StageSpawnWarning.Boss ? "BOSS INCOMING!" :
                kind == StageSpawnWarning.Elite ? "ELITE INCOMING!" : "ENEMY SURGE!";
        if (warningSubtitle != null)
            warningSubtitle.text = kind == StageSpawnWarning.Boss ? "Prepare for battle" :
                kind == StageSpawnWarning.Elite ? "A powerful enemy is approaching" : "A large enemy wave is approaching";
        warningPanel.interactable = false;
        warningPanel.blocksRaycasts = false;
        warningPanel.gameObject.SetActive(true);
        // Giữ ít nhất tới mốc spawn, nhưng không thay đổi lịch spawn của gameplay.
        float hold = Mathf.Max(warningHoldDuration, spawnAt - stageTime - warningSlideInDuration);
        AnimateWarning(hold);
    }

    public void LateTick()
    {
        if (!isActiveAndEnabled) return;
        if (pauseUI != null) pauseUI.CheckPopupClosed();
        if (stage != null && Time.deltaTime > 0f) ProcessWarnings(stage.StageTime);
    }

    #endregion

    #region Warning Slide Animation

    // Chỉ tween vị trí; dùng scaled time để dừng cùng gameplay khi pause, không thay đổi alpha.
    private void AnimateWarning(float holdDuration)
    {
        warningContent.anchoredPosition = warningRestPosition;
        Canvas.ForceUpdateCanvases();
        GetWarningSlidePositions(out var left, out var right);
        warningContent.anchoredPosition = left;
        warningAnimation = Sequence.Create(useUnscaledTime: false)
            .Chain(Tween.UIAnchoredPosition(warningContent, left, warningRestPosition,
                Mathf.Max(0.01f, warningSlideInDuration), Ease.OutCubic))
            .ChainDelay(Mathf.Max(0f, holdDuration))
            .Chain(Tween.UIAnchoredPosition(warningContent, warningRestPosition, right,
                Mathf.Max(0.01f, warningSlideOutDuration), Ease.InCubic))
            .OnComplete(this, controller =>
            {
                controller.warningAnimation = default;
                controller.ResetWarningView();
            });
    }

    // Tính theo khung Canvas và kích thước thật của banner, không cố định số pixel màn hình.
    private void GetWarningSlidePositions(out Vector2 left, out Vector2 right)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(warningPanel.transform, warningContent);
        Rect viewport = ((RectTransform)warningPanel.transform).rect;
        left = warningRestPosition + Vector2.right * (viewport.xMin - bounds.max.x - warningSlidePadding);
        right = warningRestPosition + Vector2.right * (viewport.xMax - bounds.min.x + warningSlidePadding);
    }

    private void ResetWarningView()
    {
        warningActive = false;
        if (warningPoseCached && warningContent != null) warningContent.anchoredPosition = warningRestPosition;
        if (warningPanel == null) return;
        warningPanel.gameObject.SetActive(false);
    }

    private void ClearWarning()
    {
        // Stop không chạy callback hoàn tất, nên cảnh báo cũ không thể ẩn cảnh báo mới.
        warningAnimation.Stop();
        warningAnimation = default;
        ResetWarningView();
    }

    // Chỉ bỏ toàn bộ lịch khi kết thúc/đổi lượt; tạm ẩn HUD vì popup skill vẫn giữ lịch tương lai.
    private void ClearWarnings()
    {
        pendingWarnings.Clear();
        ClearWarning();
    }

    #endregion

    #region Visibility And Lifecycle

    // Nhận điều phối từ UIManager; dữ liệu HUD vẫn được RunUI và các view con cập nhật độc lập.
    public override void SetVisible(bool visible)
    {
        gameplayVisible = visible;
        if (!visible) ClearWarning();
        if (!visible && pauseUI != null) pauseUI.Resume();
        if (panel != null) panel.SetActive(visible);
        if (resourceBar != null) resourceBar.SetActive(visible);
    }

    // Dọn pause và ẩn HUD khi root controller hoặc cả Canvas bị tắt.
    private void OnDisable()
    {
        Unsubscribe();
        SetVisible(false);
    }

    #endregion
}
