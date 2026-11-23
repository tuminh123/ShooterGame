using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View một thẻ đồ (prefab EquipmentCard, dựng từ khung ItemFrame03): màu khung theo độ hiếm, icon, "Lv.X", icon ô ở góc.
/// Dùng chung cho 6 ô mặc, lưới túi đồ và đầu popup. Thẻ trống chỉ hiện hình mờ của ô.
/// </summary>
public sealed class EquipmentCardView : MonoBehaviour
{
    #region References

    [Tooltip("Có thể để trống (thẻ trong popup không bấm được).")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image cornerDeco;
    [SerializeField] private Image lightLayer;
    [SerializeField] private Image border;
    [SerializeField] private Image glow;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("Viền tròn ở góc; đổi màu theo độ hiếm.")]
    [SerializeField] private Image slotCircle;
    [SerializeField] private Image slotIcon;
    [Tooltip("Hình mờ của ô, chỉ bật khi thẻ trống.")]
    [SerializeField] private Image emptyIcon;
    [Tooltip("Màu nền và viền khi thẻ trống.")]
    [SerializeField] private Color emptyBackground = new Color(0.25f, 0.27f, 0.33f, 1f);
    [SerializeField] private Color emptyBorder = new Color(0.45f, 0.48f, 0.56f, 1f);

    #endregion

    #region State

    // Các lớp khung được tô theo độ hiếm; tạo ở lần Bind đầu để nhớ sprite gốc của prefab.
    private StyledImage styledBackground;
    private StyledImage styledCornerDeco;
    private StyledImage styledLight;
    private StyledImage styledBorder;
    private StyledImage styledGlow;
    private StyledImage styledSlotCircle;

    #endregion

    #region Events

    /// <summary>Người dùng chạm vào thẻ (kể cả thẻ trống: ô trống dùng để chuyển tab).</summary>
    public event Action Clicked;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    #endregion

    #region Presentation

    /// <summary>Hiện dữ liệu một thẻ.</summary>
    public void Bind(EquipmentCardModel model)
    {
        bool empty = model.IsEmpty;
        SetActive(cornerDeco, !empty);
        SetActive(lightLayer, !empty);
        SetActive(glow, !empty);
        SetActive(itemIcon, !empty);
        SetActive(slotCircle, !empty);
        levelText.gameObject.SetActive(!empty);
        if (emptyIcon != null)
        {
            emptyIcon.gameObject.SetActive(empty);
            emptyIcon.sprite = model.EmptyIcon;
        }
        EnsureStyledImages();
        if (empty)
        {
            styledBackground.Reset(emptyBackground);
            styledBorder.Reset(emptyBorder);
            return;
        }

        // Mỗi lớp khung tô theo độ hiếm: màu (giữ sprite gốc) hoặc sprite kéo vào RarityStyleDatabase.
        RarityStyleDatabaseSO.EquipmentStyle rarity = model.Rarity;
        styledBackground.Apply(rarity.Background);
        styledCornerDeco.Apply(rarity.CornerDeco);
        styledLight.Apply(rarity.Light);
        styledBorder.Apply(rarity.Border);
        styledGlow.Apply(rarity.Glow);
        styledSlotCircle.Apply(rarity.SlotCircle);
        itemIcon.sprite = model.Icon;
        slotIcon.sprite = model.SlotIcon;
        levelText.text = model.LevelText;
    }

    // Chụp sprite gốc của prefab ở lần Bind đầu, trước khi có style nào đổi sprite.
    private void EnsureStyledImages()
    {
        if (styledBackground != null) return;
        styledBackground = new StyledImage(background);
        styledCornerDeco = new StyledImage(cornerDeco);
        styledLight = new StyledImage(lightLayer);
        styledBorder = new StyledImage(border);
        styledGlow = new StyledImage(glow);
        styledSlotCircle = new StyledImage(slotCircle);
    }

    private static void SetActive(Component part, bool active)
    {
        if (part != null)
            part.gameObject.SetActive(active);
    }

    private void OnClick() => Clicked?.Invoke();

    #endregion
}
