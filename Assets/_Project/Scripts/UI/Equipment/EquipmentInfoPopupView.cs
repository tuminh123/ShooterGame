using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup chi tiết một món đồ (Popup_EquipmentItemInfo): đầu popup, dòng chính, các dòng phụ, khối Skill, khối Set,
/// gợi ý mốc, nút Equip/Unequip và Level Up. Chỉ hiển thị model và phát event; không gọi service.
/// </summary>
/// <remarks>Nền tối phủ màn chặn mọi nút phía sau khi popup mở; chạm nền tối hoặc nút X thì đóng.</remarks>
public sealed class EquipmentInfoPopupView : MonoBehaviour
{
    #region References

    [Tooltip("Nền tối phủ toàn màn; chạm vào thì đóng popup.")]
    [SerializeField] private Button dimmer;
    [Tooltip("Khung popup; được phóng to nhẹ khi mở.")]
    [SerializeField] private RectTransform window;
    [SerializeField] private Button closeButton;

    [Header("Header")]
    [SerializeField] private Image slotIcon;
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text rarityText;
    [Tooltip("Lớp sáng trên đầu popup; đổi màu theo độ hiếm.")]
    [SerializeField] private Image headerGlow;
    [SerializeField] private EquipmentCardView card;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Stats")]
    [SerializeField] private EquipmentStatRowView mainRow;
    [Tooltip("Đủ số dòng phụ tối đa (4), theo thứ tự mở.")]
    [SerializeField] private EquipmentStatRowView[] substatRows = Array.Empty<EquipmentStatRowView>();

    [Header("Skill / Set")]
    [SerializeField] private GameObject skillBlock;
    [SerializeField] private Image skillIcon;
    [SerializeField] private TMP_Text skillText;
    [SerializeField] private GameObject setBlock;
    [SerializeField] private Image setIcon;
    [SerializeField] private TMP_Text setText;
    [Tooltip("Màu chữ khối Skill/Set khi đã có hiệu lực / chưa có hiệu lực.")]
    [SerializeField] private Color activeColor = new Color(0.55f, 1f, 0.45f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.75f, 0.78f, 0.85f, 1f);
    [SerializeField] private TMP_Text hintText;

    [Header("Buttons")]
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipLabel;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeLabel;
    [SerializeField, Min(0.01f)] private float openDuration = 0.18f;

    #endregion

    #region Events And State

    /// <summary>Bấm Equip hoặc Unequip (controller biết món đang mặc hay chưa).</summary>
    public event Action EquipClicked;

    /// <summary>Bấm Level Up.</summary>
    public event Action UpgradeClicked;

    /// <summary>Popup đóng (nền tối, nút X hoặc controller gọi Hide).</summary>
    public event Action Closed;

    private Sequence openSequence;

    /// <summary>Popup đang mở.</summary>
    public bool IsOpen => window.gameObject.activeSelf;

    #endregion

    #region Lifecycle

    private void Awake() => SetVisible(false);

    private void OnEnable()
    {
        dimmer.onClick.AddListener(Hide);
        closeButton.onClick.AddListener(Hide);
        equipButton.onClick.AddListener(OnEquip);
        upgradeButton.onClick.AddListener(OnUpgrade);
    }

    private void OnDisable()
    {
        dimmer.onClick.RemoveListener(Hide);
        closeButton.onClick.RemoveListener(Hide);
        equipButton.onClick.RemoveListener(OnEquip);
        upgradeButton.onClick.RemoveListener(OnUpgrade);
        openSequence.Stop();
        window.localScale = Vector3.one;
    }

    #endregion

    #region Presentation

    /// <summary>
    /// Mở popup hoặc vẽ lại tại chỗ khi đang mở (sau khi lên cấp). <paramref name="animate"/> = false khi chỉ vẽ lại,
    /// để popup không nảy lại mỗi lần bấm Level Up.
    /// </summary>
    public void Show(EquipmentInfoModel model, bool animate)
    {
        slotIcon.sprite = model.SlotIcon;
        slotText.text = model.SlotText;
        titleText.text = model.Title;
        rarityText.text = model.RarityText;
        rarityText.color = model.RarityColor;
        if (headerGlow != null)
            headerGlow.color = model.RarityColor;
        card.Bind(model.Card);
        descriptionText.text = model.Description;
        mainRow.Bind(model.MainStat);
        for (int i = 0; i < substatRows.Length; i++)
        {
            bool used = i < model.Substats.Length;
            substatRows[i].gameObject.SetActive(used);
            if (used)
                substatRows[i].Bind(model.Substats[i]);
        }
        BindBlock(skillBlock, skillIcon, skillText, model.Skill);
        BindBlock(setBlock, setIcon, setText, model.Set);
        hintText.text = model.Hint;
        hintText.gameObject.SetActive(!string.IsNullOrEmpty(model.Hint));
        equipLabel.text = model.EquipLabel;
        upgradeLabel.text = model.UpgradeLabel;
        upgradeButton.interactable = model.UpgradeInteractable;

        bool wasOpen = IsOpen;
        SetVisible(true);
        // Ép layout tính lại ngay để khung khớp nội dung mới trước khung hình đầu tiên.
        LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        if (animate || !wasOpen)
            PlayOpen();
    }

    /// <summary>Nảy dòng chính và dòng phụ thứ <paramref name="changedSubstat"/> (vừa mở hoặc vừa được cộng; -1 = không có).</summary>
    public void PlayLevelUp(int changedSubstat)
    {
        mainRow.Flash();
        if (changedSubstat >= 0 && changedSubstat < substatRows.Length)
            substatRows[changedSubstat].Flash();
    }

    /// <summary>Nút Level Up rung ngang khi lần bấm bị từ chối (không đủ tiền); cần <see cref="ButtonDeniedShake"/> trên nút.</summary>
    public void PlayUpgradeDenied()
    {
        if (upgradeButton.TryGetComponent(out ButtonDeniedShake shake))
            shake.Shake();
    }

    /// <summary>Đóng popup.</summary>
    public void Hide()
    {
        bool wasOpen = IsOpen;
        openSequence.Stop();
        window.localScale = Vector3.one;
        SetVisible(false);
        if (wasOpen)
            Closed?.Invoke();
    }

    private void BindBlock(GameObject block, Image image, TMP_Text text, EquipmentBonusBlockModel model)
    {
        block.SetActive(model.Visible);
        if (!model.Visible)
            return;
        image.sprite = model.Icon;
        image.enabled = model.Icon != null;
        text.text = model.Text;
        text.color = model.Active ? activeColor : inactiveColor;
    }

    // Phóng nhẹ từ 0.9 lên 1 cho cảm giác "bật lên"; thời gian không scale vì Home có thể đang pause.
    private void PlayOpen()
    {
        openSequence.Stop();
        window.localScale = Vector3.one * 0.9f;
        openSequence = Sequence.Create(useUnscaledTime: true)
            .Chain(Tween.Scale(window, 1f, openDuration, Ease.OutBack));
    }

    private void SetVisible(bool visible)
    {
        dimmer.gameObject.SetActive(visible);
        window.gameObject.SetActive(visible);
    }

    private void OnEquip() => EquipClicked?.Invoke();

    private void OnUpgrade() => UpgradeClicked?.Invoke();

    #endregion
}
