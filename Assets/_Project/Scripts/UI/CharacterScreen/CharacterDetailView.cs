using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View màn Detail của một nhân vật: header, tab Level/Stars và footer.
/// Chỉ hiện <see cref="CharacterDetailModel"/> và phát event khi người dùng bấm; không gọi service.
/// Tab Stars là mockup tĩnh trong demo: View chỉ bật/tắt nó.
/// </summary>
public sealed class CharacterDetailView : MonoBehaviour
{
    #region Header References

    [SerializeField] private Image rarityLabel;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image portrait;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [Tooltip("5 ô sao ở header theo thứ tự; bật đúng số sao hiện có.")]
    [SerializeField] private GameObject[] stars = Array.Empty<GameObject>();

    #endregion

    #region Tab References

    [SerializeField] private Button levelTabButton;
    [SerializeField] private Button starsTabButton;
    [SerializeField] private GameObject levelTab;
    [SerializeField] private GameObject starTab;
    [Tooltip("Màu nút tab đang mở và tab còn lại.")]
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [SerializeField] private Slider levelSlider;
    [Tooltip("Chữ \"level/max\" trên thanh level.")]
    [SerializeField] private TMP_Text levelValueText;
    [Tooltip("Số level trong huy hiệu cạnh thanh.")]
    [SerializeField] private TMP_Text levelBadgeText;
    [Tooltip("8 ô chỉ số theo thứ tự trong Theme.")]
    [SerializeField] private StatRowView[] statRows = Array.Empty<StatRowView>();
    [Tooltip("Icon của 3 ô skill; ô không có skill bị ẩn icon.")]
    [SerializeField] private Image[] skillIcons = Array.Empty<Image>();

    #endregion

    #region Footer References

    [SerializeField] private Button backButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text selectText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeTitle;
    [Tooltip("Vùng chứa các mục giá; ẩn khi không có giá (MAX, chưa mở).")]
    [SerializeField] private GameObject costArea;
    [Tooltip("Tối đa 2 mục giá: gốc, icon và chữ cùng chỉ số.")]
    [SerializeField] private GameObject[] costRoots = Array.Empty<GameObject>();
    [SerializeField] private Image[] costIcons = Array.Empty<Image>();
    [SerializeField] private TMP_Text[] costTexts = Array.Empty<TMP_Text>();

    [Tooltip("Chữ trên nút chọn khi nhân vật chưa/đang được dùng.")]
    [SerializeField] private string selectLabel = "SELECT";
    [SerializeField] private string inUseLabel = "IN USE";

    #endregion

    #region Events

    public event Action PrevClicked;
    public event Action NextClicked;
    public event Action BackClicked;
    public event Action SelectClicked;
    public event Action UpgradeClicked;
    public event Action<CharacterScreenTab> TabChanged;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        prevButton.onClick.AddListener(OnPrev);
        nextButton.onClick.AddListener(OnNext);
        backButton.onClick.AddListener(OnBack);
        selectButton.onClick.AddListener(OnSelect);
        upgradeButton.onClick.AddListener(OnUpgrade);
        levelTabButton.onClick.AddListener(OnLevelTab);
        starsTabButton.onClick.AddListener(OnStarsTab);
    }

    private void OnDisable()
    {
        prevButton.onClick.RemoveListener(OnPrev);
        nextButton.onClick.RemoveListener(OnNext);
        backButton.onClick.RemoveListener(OnBack);
        selectButton.onClick.RemoveListener(OnSelect);
        upgradeButton.onClick.RemoveListener(OnUpgrade);
        levelTabButton.onClick.RemoveListener(OnLevelTab);
        starsTabButton.onClick.RemoveListener(OnStarsTab);
    }

    #endregion

    #region Presentation

    /// <summary>Vẽ lại toàn bộ màn Detail.</summary>
    public void Bind(CharacterDetailModel model)
    {
        BindHeader(model);
        BindTabs(model.Tab);
        BindLevelTab(model);
        BindFooter(model);
    }

    /// <summary>Bật/tắt nút ←/→ (tắt khi chỉ có một nhân vật).</summary>
    public void SetNavigationVisible(bool visible)
    {
        prevButton.gameObject.SetActive(visible);
        nextButton.gameObject.SetActive(visible);
    }

    private void BindHeader(CharacterDetailModel model)
    {
        if (model.RarityLabel != null) rarityLabel.sprite = model.RarityLabel;
        rarityText.text = model.RarityText;
        rarityText.color = model.RarityTextColor;
        nameText.text = model.Name;
        if (model.Portrait != null) portrait.sprite = model.Portrait;
        SetActiveCount(stars, model.Stars);
    }

    private void BindTabs(CharacterScreenTab tab)
    {
        bool level = tab == CharacterScreenTab.Level;
        levelTab.SetActive(level);
        starTab.SetActive(!level);
        levelTabButton.image.color = level ? activeTabColor : inactiveTabColor;
        starsTabButton.image.color = level ? inactiveTabColor : activeTabColor;
    }

    private void BindLevelTab(CharacterDetailModel model)
    {
        levelSlider.minValue = 0f;
        levelSlider.maxValue = Mathf.Max(1, model.MaxLevel);
        levelSlider.value = model.Level;
        levelValueText.text = model.Level.ToString(CultureInfo.InvariantCulture) + "/" + model.MaxLevel.ToString(CultureInfo.InvariantCulture);
        levelBadgeText.text = model.Level.ToString(CultureInfo.InvariantCulture);

        for (int i = 0; i < statRows.Length; i++)
        {
            bool used = i < model.Stats.Count;
            statRows[i].gameObject.SetActive(used);
            if (used) statRows[i].Bind(model.Stats[i]);
        }
        for (int i = 0; i < skillIcons.Length; i++)
        {
            Sprite icon = i < model.SkillIcons.Count ? model.SkillIcons[i] : null;
            skillIcons[i].sprite = icon;
            skillIcons[i].enabled = icon != null;
        }
    }

    private void BindFooter(CharacterDetailModel model)
    {
        selectButton.interactable = !model.IsSelected;
        selectText.text = model.IsSelected ? inUseLabel : selectLabel;

        UpgradeButtonModel upgrade = model.Upgrade;
        upgradeButton.interactable = upgrade.Interactable;
        upgradeTitle.text = upgrade.Title;
        IReadOnlyList<CostModel> costs = upgrade.Costs;
        if (costArea != null) costArea.SetActive(costs.Count > 0);
        for (int i = 0; i < costRoots.Length; i++)
        {
            bool used = i < costs.Count;
            costRoots[i].SetActive(used);
            if (!used) continue;
            costIcons[i].sprite = costs[i].Icon;
            costTexts[i].text = costs[i].Amount;
        }
    }

    // Bật đúng `count` phần tử đầu, tắt phần còn lại.
    private static void SetActiveCount(GameObject[] items, int count)
    {
        for (int i = 0; i < items.Length; i++)
            if (items[i] != null) items[i].SetActive(i < count);
    }

    #endregion

    #region Input Forwarding

    private void OnPrev() => PrevClicked?.Invoke();
    private void OnNext() => NextClicked?.Invoke();
    private void OnBack() => BackClicked?.Invoke();
    private void OnSelect() => SelectClicked?.Invoke();
    private void OnUpgrade() => UpgradeClicked?.Invoke();
    private void OnLevelTab() => TabChanged?.Invoke(CharacterScreenTab.Level);
    private void OnStarsTab() => TabChanged?.Invoke(CharacterScreenTab.Stars);

    #endregion
}
