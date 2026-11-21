using System;
using System.Collections.Generic;
using System.Globalization;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// View màn Detail của một nhân vật: header, tab Level/Stars và footer.
/// Chỉ hiện <see cref="CharacterDetailModel"/> và phát event khi người dùng bấm; không gọi service.
/// Khung nhân vật hiện prefab <see cref="CharacterUIVisual"/> (Idle, ăn mừng qua <see cref="Celebrate"/>), thiếu thì hiện ảnh tĩnh.
/// Tab Stars là mockup tĩnh trong demo: View chỉ bật/tắt nó. Vuốt ngang trên màn để đổi nhân vật (như ←/→).
/// </summary>
public sealed class CharacterDetailView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    #region Feel Settings

    [Tooltip("Thời gian thanh EXP chạy tới giá trị mới (giây); lên cấp thì chạy đầy, về 0 rồi chạy tiếp.")]
    [SerializeField, Min(0.01f)] private float barFillDuration = 0.15f;
    [Tooltip("Vuốt ngang tối thiểu, tính theo tỉ lệ chiều rộng màn hình, để đổi nhân vật.")]
    [SerializeField, Range(0.05f, 0.5f)] private float swipeThreshold = 0.15f;

    #endregion

    #region Header References

    [SerializeField] private Image rarityLabel;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Khung nhân vật. Có prefab UI thì nhân vật UI (Idle) đặt vừa khung này và ẩn ảnh; chưa có thì hiện ảnh tĩnh.")]
    [SerializeField] private Image portrait;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [Tooltip("5 ô sao ở header theo thứ tự; luôn hiện đủ, sao đã có tô màu Filled, sao chưa có tô màu Empty.")]
    [SerializeField] private GameObject[] stars = Array.Empty<GameObject>();
    [Tooltip("Màu Image của sao đã có (trắng = giữ nguyên sprite vàng).")]
    [SerializeField] private Color filledStarColor = Color.white;
    [Tooltip("Màu Image của sao chưa có; project không có sprite sao xám nên làm mờ sprite vàng bằng màu.")]
    [SerializeField] private Color emptyStarColor = new Color(0.25f, 0.25f, 0.3f, 0.6f);

    #endregion

    #region Tab References

    [SerializeField] private Button levelTabButton;
    [SerializeField] private Button starsTabButton;
    [SerializeField] private GameObject levelTab;
    [SerializeField] private GameObject starTab;
    [Tooltip("Màu nút tab đang mở và tab còn lại.")]
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [Tooltip("Hướng nội dung lướt đi khi sang tab Stars (tab bên phải). Quay về Level thì lướt ngược lại.")]
    [SerializeField] private SlideDirection tabSlideForward = SlideDirection.Left;

    [SerializeField] private Slider levelSlider;
    [Tooltip("Chữ \"đã nạp/cần\" trên thanh EXP nâng cấp của cấp hiện tại.")]
    [SerializeField] private TMP_Text levelValueText;
    [Tooltip("Chữ trên thanh khi đã đạt cấp tối đa.")]
    [SerializeField] private string maxLevelText = "MAX";
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

    #region Character Preview State

    private CharacterPreviewSlot preview; // Tạo khi bind lần đầu, dùng khung Image_Portrait.

    #endregion

    #region Bind State

    // Nhận ra "vẫn nhân vật cũ" để chỉ cho số nhảy khi nâng cấp, không nhảy khi bấm ←/→.
    private bool hasBound;
    private CharacterType boundType;
    // Tab đang hiện; chỉ trượt khi thật sự đổi tab, lần hiện đầu và các lần vẽ lại cùng tab thì không.
    private bool hasShownTab;
    private CharacterScreenTab shownTab;
    private TextPunch badgePunch;
    private TextPunch progressPunch;
    private int boundLevel; // Cấp lần vẽ trước, để biết vừa lên cấp (thanh chạy đầy rồi về 0).
    private Sequence barSequence;

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
        // Lần mở sau là "lần đầu": không trượt tab, không nhảy số so với lần xem trước.
        hasBound = false;
        hasShownTab = false;
        badgePunch?.Reset();
        progressPunch?.Reset();
        barSequence.Stop();
    }

    #endregion

    #region Presentation

    /// <summary>Vẽ lại toàn bộ màn Detail.</summary>
    public void Bind(CharacterDetailModel model)
    {
        bool sameCharacter = hasBound && model.Type == boundType;
        hasBound = true;
        boundType = model.Type;
        BindHeader(model);
        BindTabs(model.Tab);
        BindLevelTab(model, sameCharacter);
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
        preview ??= new CharacterPreviewSlot(portrait);
        preview.Show(model.UIVisual, model.Portrait);
        BindStars(model.Stars);
    }

    /// <summary>Phát animation ăn mừng (Victory) một lần rồi tự về Idle; không có nhân vật UI thì bỏ qua.</summary>
    public void Celebrate() => preview?.Celebrate();

    // Luôn hiện đủ ô để người chơi thấy còn bao nhiêu sao chưa mở; chỉ đổi màu theo số sao hiện có.
    private void BindStars(int count)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null) continue;
            stars[i].SetActive(true);
            if (stars[i].TryGetComponent(out Image image))
                image.color = i < count ? filledStarColor : emptyStarColor;
        }
    }

    private void BindTabs(CharacterScreenTab tab)
    {
        bool level = tab == CharacterScreenTab.Level;
        levelTabButton.image.color = level ? activeTabColor : inactiveTabColor;
        starsTabButton.image.color = level ? inactiveTabColor : activeTabColor;
        if (hasShownTab && shownTab == tab) return; // Vẽ lại cùng tab: không đụng tới tab đang hiện.

        GameObject next = level ? levelTab : starTab;
        GameObject previous = level ? starTab : levelTab;
        // Lần hiện đầu (vừa mở Detail) thì bật ngay; chỉ trượt khi người dùng đổi tab.
        if (hasShownTab
            && next.TryGetComponent(out SlideTransition slideIn)
            && previous.TryGetComponent(out SlideTransition slideOut))
        {
            // Sang Stars: tab cũ lướt theo tabSlideForward; về Level: lướt theo hướng ngược lại.
            SlideDirection exit = level ? SlideTransition.Opposite(tabSlideForward) : tabSlideForward;
            slideOut.PlayOut(exit);
            slideIn.PlayIn(SlideTransition.Opposite(exit));
        }
        else
        {
            SetTabInstant(next, true);
            SetTabInstant(previous, false);
        }
        hasShownTab = true;
        shownTab = tab;
    }

    private static void SetTabInstant(GameObject tabObject, bool visible)
    {
        if (tabObject.TryGetComponent(out SlideTransition slide)) slide.SetVisibleInstant(visible);
        else tabObject.SetActive(visible);
    }

    private void BindLevelTab(CharacterDetailModel model, bool sameCharacter)
    {
        // Thanh là EXP nâng cấp của cấp hiện tại (giống thanh EXP trong trận); cấp hiển thị ở huy hiệu.
        bool maxed = model.LevelProgressRequired <= 0;
        AnimateBar(maxed ? 1f : model.LevelProgressRequired, maxed ? 1f : model.LevelProgress,
            sameCharacter, sameCharacter && model.Level > boundLevel);
        boundLevel = model.Level;
        progressPunch ??= new TextPunch(levelValueText);
        badgePunch ??= new TextPunch(levelBadgeText);
        progressPunch.Set(maxed
            ? maxLevelText
            : model.LevelProgress.ToString(CultureInfo.InvariantCulture) + "/" + model.LevelProgressRequired.ToString(CultureInfo.InvariantCulture),
            maxed ? 0f : model.LevelProgress, sameCharacter);
        badgePunch.Set(model.Level.ToString(CultureInfo.InvariantCulture), model.Level, sameCharacter);

        for (int i = 0; i < statRows.Length; i++)
        {
            bool used = i < model.Stats.Count;
            statRows[i].gameObject.SetActive(used);
            if (used) statRows[i].Bind(model.Stats[i], sameCharacter);
        }
        for (int i = 0; i < skillIcons.Length; i++)
        {
            Sprite icon = i < model.SkillIcons.Count ? model.SkillIcons[i] : null;
            skillIcons[i].sprite = icon;
            skillIcons[i].enabled = icon != null;
        }
    }

    // Thanh chạy mượt tới giá trị mới, giống thanh EXP trong trận. Đổi nhân vật thì đặt ngay, không chạy.
    // Lên cấp: chạy đầy thanh cũ → về 0 với thanh của cấp mới → chạy tới phần dư.
    private void AnimateBar(float newMax, float target, bool animate, bool levelUp)
    {
        barSequence.Stop();
        levelSlider.minValue = 0f;
        if (!animate || !isActiveAndEnabled)
        {
            levelSlider.maxValue = newMax;
            levelSlider.value = target;
            return;
        }
        Sequence sequence = Sequence.Create(useUnscaledTime: true); // Màn Character chạy khi game đang pause.
        if (levelUp)
        {
            sequence.Chain(Tween.Custom(levelSlider.value, levelSlider.maxValue, barFillDuration, SetBarValue))
                .ChainCallback(() =>
                {
                    levelSlider.maxValue = newMax;
                    levelSlider.value = 0f;
                });
        }
        else levelSlider.maxValue = newMax;
        barSequence = sequence.Chain(Tween.Custom(levelUp ? 0f : levelSlider.value, target, barFillDuration, SetBarValue));
    }

    private void SetBarValue(float value) => levelSlider.value = value;

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

    #endregion

    #region Swipe (đổi nhân vật)

    /// <summary>Cần để Unity giao sự kiện kéo cho View (kéo bắt đầu trên nền hoặc bất kỳ phần tử con nào).</summary>
    public void OnBeginDrag(PointerEventData eventData) { }

    /// <summary>Không theo tay khi kéo; chỉ xét hướng lúc thả.</summary>
    public void OnDrag(PointerEventData eventData) { }

    /// <summary>Vuốt sang trái → nhân vật kế tiếp, sang phải → nhân vật trước; vuốt dọc hoặc quá ngắn thì bỏ qua.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - eventData.pressPosition;
        if (!nextButton.gameObject.activeSelf) return; // Chỉ một nhân vật thì không đổi.
        if (Mathf.Abs(delta.x) < Screen.width * swipeThreshold || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;
        if (delta.x < 0f) OnNext();
        else OnPrev();
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
