using System;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Thẻ một mission chính (cả thẻ tổng kết): tên, các mission con, hàng thưởng và nút nhận. Chỉ hiển thị và phát event.</summary>
public sealed class MissionCardView : MonoBehaviour
{
    #region References

    [SerializeField] private CanvasGroup group;
    [Tooltip("Chỉ dùng để co chiều cao khi thẻ rời chỗ; bình thường để -1.")]
    [SerializeField] private LayoutElement layout;
    [SerializeField] private Image background;
    [SerializeField] private Image inner;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color normalInner = Color.white;
    [SerializeField] private Color claimableColor = Color.white;
    [SerializeField] private Color claimableInner = Color.white;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private RectTransform taskRoot;
    [Tooltip("Dòng mẫu nằm sẵn trong Task Root; được nhân bản khi cần.")]
    [SerializeField] private MissionTaskRowView taskTemplate;
    [SerializeField] private RectTransform rewardRoot;
    [SerializeField] private RewardSlotView rewardPrefab;

    [Header("Trạng thái")]
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimText;
    [Tooltip("Thay nút Claim khi đang làm hoặc đã nhận.")]
    [SerializeField] private GameObject statusRoot;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Lớp xám phủ thẻ đã nhận.")]
    [SerializeField] private GameObject dim;
    [Tooltip("Chấm báo còn thưởng chưa nhận.")]
    [SerializeField] private GameObject alert;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.25f;

    #endregion

    #region Events And State

    /// <summary>Bấm nút nhận thưởng; tham số là ID mission.</summary>
    public event Action<string> ClaimClicked;

    /// <summary>ID mission đang hiện.</summary>
    public string MissionId { get; private set; }

    private readonly List<MissionTaskRowView> rows = new List<MissionTaskRowView>();
    private readonly List<RewardSlotView> slots = new List<RewardSlotView>();
    private Tween pulse;
    private Sequence move;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        claimButton.onClick.AddListener(OnClaim);
        UpdatePulse();
    }

    private void OnDisable()
    {
        claimButton.onClick.RemoveListener(OnClaim);
        StopPulse();
        ResetMove();
    }

    private void OnClaim() => ClaimClicked?.Invoke(MissionId);

    #endregion

    #region Presentation

    /// <summary>Hiện một mission.</summary>
    public void Bind(MissionCardModel model)
    {
        ResetMove();
        MissionId = model.Id;
        titleText.text = model.Title;
        descriptionText.text = model.Description;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(model.Description));

        bool claimable = model.State == MissionState.Claimable;
        background.color = claimable ? claimableColor : normalColor;
        inner.color = claimable ? claimableInner : normalInner;
        claimButton.gameObject.SetActive(claimable);
        statusRoot.SetActive(!claimable);
        claimText.text = model.ButtonText;
        statusText.text = model.ButtonText;
        dim.SetActive(model.State == MissionState.Claimed);
        alert.SetActive(claimable);

        if (rows.Count == 0) rows.Add(taskTemplate);
        while (rows.Count < model.Tasks.Count) rows.Add(Instantiate(taskTemplate, taskRoot));
        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < model.Tasks.Count;
            rows[i].gameObject.SetActive(used);
            if (used) rows[i].Bind(model.Tasks[i]);
        }
        RewardSlotView.BindAll(slots, rewardPrefab, rewardRoot, model.Rewards);
        UpdatePulse();
    }

    /// <summary>Mờ đi rồi co chiều cao để các thẻ dưới dồn lên, xong gọi <paramref name="onComplete"/>.</summary>
    public void PlayDisappear(Action onComplete)
    {
        ResetMove();
        float height = ((RectTransform)transform).rect.height;
        move = Sequence.Create(useUnscaledTime: true)
            .Chain(Tween.Alpha(group, 1f, 0f, moveDuration))
            .Chain(Tween.Custom(height, 0f, moveDuration, SetHeight, Ease.InOutQuad))
            .ChainCallback(() => onComplete());
    }

    /// <summary>Hiện dần ở vị trí mới.</summary>
    public void PlayAppear()
    {
        ResetMove();
        move = Sequence.Create(useUnscaledTime: true).Chain(Tween.Alpha(group, 0f, 1f, moveDuration));
    }

    #endregion

    #region Motion Helpers

    private void SetHeight(float height)
    {
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private void ResetMove()
    {
        move.Stop();
        group.alpha = 1f;
        if (layout.preferredHeight >= 0f) SetHeight(-1f);
    }

    // Nhịp ở chữ chứ không ở nút: scale của nút thuộc về ButtonClickTween.
    private void UpdatePulse()
    {
        if (!claimButton.gameObject.activeInHierarchy) StopPulse();
        else if (!pulse.isAlive)
            pulse = Tween.Scale(claimText.transform, Vector3.one, Vector3.one * 1.1f, 0.5f, Ease.InOutSine, -1, CycleMode.Yoyo,
                useUnscaledTime: true);
    }

    private void StopPulse()
    {
        pulse.Stop();
        claimText.transform.localScale = Vector3.one;
    }

    #endregion
}
