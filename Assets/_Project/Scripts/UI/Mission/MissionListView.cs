using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Nội dung màn Mission: tiêu đề, hàng tab, thẻ tổng kết và danh sách thẻ. Tự sinh và tái dùng; chỉ hiển thị và phát event.</summary>
public sealed class MissionListView : MonoBehaviour
{
    #region Types

    /// <summary>Một nút tab và các phần của nó.</summary>
    [Serializable]
    private sealed class TabBinding
    {
        public Button button;
        public Image background;
        public TMP_Text label;
        [Tooltip("Vệt sáng của tab đang chọn.")]
        public GameObject light;
        public GameObject alert;
    }

    #endregion

    #region References

    [Tooltip("Số tab tối đa; nhóm nhiều hơn số này thì không hiện được.")]
    [SerializeField] private TabBinding[] tabs = Array.Empty<TabBinding>();
    [SerializeField] private Color tabColor = new Color(0.2f, 0.25f, 0.36f, 1f);
    [SerializeField] private Color selectedTabColor = new Color(0.95f, 0.65f, 0.15f, 1f);
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Số mission đã xong \"x/y\".")]
    [SerializeField] private TMP_Text completeText;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private MissionCardView cardPrefab;
    [Tooltip("Thẻ tổng kết, luôn đứng đầu danh sách.")]
    [SerializeField] private MissionCardView finalCard;
    [Tooltip("Hiện khi nhóm không có mission nào hoặc không có nhóm nào mở.")]
    [SerializeField] private GameObject emptyHint;

    #endregion

    #region Events And State

    /// <summary>Bấm tab thứ <c>index</c>.</summary>
    public event Action<int> TabClicked;

    /// <summary>Bấm nhận thưởng; tham số là ID mission.</summary>
    public event Action<string> ClaimClicked;

    /// <summary>Bấm Go trên một thẻ; tham số là màn đích.</summary>
    public event Action<MissionDestination> GoClicked;

    private readonly List<MissionCardView> cards = new List<MissionCardView>();
    private bool bound;

    #endregion

    #region Presentation

    /// <summary>Số tab hiện được.</summary>
    public int TabCapacity => tabs.Length;

    /// <summary>Vẽ lại cả màn.</summary>
    public void Bind(MissionScreenModel model)
    {
        BindEvents();
        IReadOnlyList<MissionTabModel> tabModels = model.Tabs;
        IReadOnlyList<MissionCardModel> cardModels = model.Cards;
        titleText.text = model.Title;
        completeText.text = model.CompleteText;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool used = i < tabModels.Count;
            tabs[i].button.gameObject.SetActive(used);
            if (!used) continue;
            tabs[i].label.text = tabModels[i].Title;
            tabs[i].background.color = tabModels[i].Selected ? selectedTabColor : tabColor;
            tabs[i].light.SetActive(tabModels[i].Selected);
            tabs[i].alert.SetActive(tabModels[i].HasClaimable);
        }
        finalCard.gameObject.SetActive(model.FinalCard.HasValue);
        if (model.FinalCard.HasValue) finalCard.Bind(model.FinalCard.Value);
        while (cards.Count < cardModels.Count)
        {
            MissionCardView card = Instantiate(cardPrefab, content);
            card.ClaimClicked += RaiseClaim;
            card.GoClicked += RaiseGo;
            cards.Add(card);
        }
        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < cardModels.Count;
            cards[i].gameObject.SetActive(used);
            if (used) cards[i].Bind(cardModels[i]);
        }
        emptyHint.SetActive(cardModels.Count == 0);
    }

    /// <summary>Đưa danh sách về đầu (mở màn, đổi tab).</summary>
    public void ScrollToTop()
    {
        scroll.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
    }

    /// <summary>Thẻ vừa nhận rời chỗ cũ rồi gọi <paramref name="onComplete"/>. Thẻ tổng kết đứng yên nên gọi ngay.</summary>
    public void PlayClaimed(string missionId, Action onComplete)
    {
        MissionCardView card = Find(missionId);
        if (card != null) card.PlayDisappear(onComplete);
        else onComplete();
    }

    /// <summary>Thẻ vừa nhận hiện lại ở chỗ mới (sau khi <see cref="Bind"/>).</summary>
    public void PlayMoved(string missionId)
    {
        MissionCardView card = Find(missionId);
        if (card != null) card.PlayAppear();
    }

    #endregion

    #region Helpers

    private MissionCardView Find(string missionId)
    {
        foreach (MissionCardView card in cards)
            if (card.isActiveAndEnabled && card.MissionId == missionId) return card;
        return null;
    }

    private void RaiseClaim(string missionId) => ClaimClicked?.Invoke(missionId);

    private void RaiseGo(MissionDestination destination) => GoClicked?.Invoke(destination);

    // Nút tab và thẻ tổng kết sống cùng View này nên chỉ gắn listener một lần.
    private void BindEvents()
    {
        if (bound) return;
        bound = true;
        finalCard.ClaimClicked += RaiseClaim;
        finalCard.GoClicked += RaiseGo;
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].button.onClick.AddListener(() => TabClicked?.Invoke(index));
        }
    }

    #endregion
}
