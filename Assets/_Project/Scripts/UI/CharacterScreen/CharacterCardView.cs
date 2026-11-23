using System;
using System.Globalization;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View một thẻ nhân vật (prefab CharacterCard): nhãn + màu nền theo độ hiếm, tên, ảnh, level, sao,
/// con dấu IN USE ở góc và trạng thái Normal/Selected/Empty/Locked (chưa sở hữu: xám + thanh mảnh).
/// </summary>
public sealed class CharacterCardView : MonoBehaviour
{
    #region References

    [SerializeField] private Button button;

    [Header("Background (tô màu theo độ hiếm)")]
    [Tooltip("Lớp nền chính của thẻ (Bg).")]
    [SerializeField] private Image background;
    [Tooltip("Lớp sáng trên nền (Bg/Gradient).")]
    [SerializeField] private Image lightLayer;
    [Tooltip("Quầng sáng trên nền (Bg/Glow).")]
    [SerializeField] private Image glow;
    [Tooltip("Màu nền khi thẻ trống (không có nhân vật); Gradient và Glow đã tắt ở trạng thái này.")]
    [SerializeField] private Color emptyBackground = new Color(0.384f, 0.431f, 0.545f, 1f);

    [Header("Rarity Label (trên cùng thẻ)")]
    [Tooltip("Nhãn độ hiếm; sprite đổi theo độ hiếm (cùng sprite với nhãn ở màn Detail).")]
    [SerializeField] private Image rarityLabel;
    [SerializeField] private TMP_Text rarityText;

    [Header("Content")]
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("5 ô sao theo thứ tự; luôn hiện đủ, sao đã có tô màu Filled, sao chưa có tô màu Empty.")]
    [SerializeField] private GameObject[] stars = Array.Empty<GameObject>();
    [Tooltip("Màu Image của sao đã có (trắng = giữ nguyên sprite vàng).")]
    [SerializeField] private Color filledStarColor = Color.white;
    [Tooltip("Màu Image của sao chưa có; project không có sprite sao xám nên làm mờ sprite vàng bằng màu.")]
    [SerializeField] private Color emptyStarColor = new Color(0.25f, 0.25f, 0.3f, 0.6f);
    [Tooltip("Con dấu IN USE phủ lên góc thẻ, chỉ bật khi thẻ là nhân vật đang chọn.")]
    [SerializeField] private GameObject inUseStamp;
    [Tooltip("Viền xanh quanh thẻ; chỉ bật cho nhân vật đang được sử dụng.")]
    [SerializeField] private GameObject selectedBorder;
    [Tooltip("Icon thẻ trống, chỉ bật ở trạng thái Empty.")]
    [SerializeField] private GameObject emptyIcon;
    [Tooltip("Các phần tắt khi thẻ trống để thẻ tối như mockup (Gradient, Glow, Bottom, ảnh, sao, level, tên).")]
    [SerializeField] private GameObject[] hiddenWhenEmpty = Array.Empty<GameObject>();

    [Header("Locked (chưa sở hữu)")]
    [Tooltip("Material thang xám (shader Shooter Game/UI/Grayscale) cho nền, nhãn và ảnh khi chưa sở hữu.")]
    [SerializeField] private Material lockedMaterial;
    [Tooltip("Huy hiệu level; ẩn khi chưa sở hữu.")]
    [SerializeField] private GameObject levelBadge;
    [Tooltip("Hàng sao; ẩn khi chưa sở hữu.")]
    [SerializeField] private GameObject starsGroup;
    [Tooltip("Thanh mảnh thay cho level/sao khi chưa sở hữu.")]
    [SerializeField] private GameObject shardBar;
    [Tooltip("Phần đầy của thanh (Image kiểu Filled ngang).")]
    [SerializeField] private Image shardFill;
    [SerializeField] private Image shardIcon;
    [SerializeField] private TMP_Text shardText;
    [Tooltip("Viền sáng quanh thẻ khi đủ mảnh để ghép; nhấp nháy để gọi người chơi chạm vào.")]
    [SerializeField] private Image readyBorder;
    [SerializeField] private Color shardFillColor = new Color(0.36f, 0.6f, 0.95f, 1f);
    [Tooltip("Màu thanh khi đủ mảnh.")]
    [SerializeField] private Color shardReadyColor = new Color(1f, 0.82f, 0.23f, 1f);

    #endregion

    #region State

    // 3 lớp nền được tô theo độ hiếm; tạo ở lần Bind đầu để nhớ sprite gốc của prefab.
    private StyledImage styledBackground;
    private StyledImage styledLight;
    private StyledImage styledGlow;
    private Tween readyPulse;

    #endregion

    #region Events

    /// <summary>Người dùng chạm vào thẻ (thẻ trống không phát).</summary>
    public event Action Clicked;

    #endregion

    #region Lifecycle

    private void OnEnable() => button.onClick.AddListener(OnClick);

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
        readyPulse.Stop();
    }

    #endregion

    #region Presentation

    /// <summary>Hiện dữ liệu một thẻ.</summary>
    public void Bind(CharacterCardModel model)
    {
        bool empty = model.State == CharacterCardState.Empty;
        foreach (GameObject part in hiddenWhenEmpty)
            if (part != null) part.SetActive(!empty);
        if (emptyIcon != null) emptyIcon.SetActive(empty);
        bool selected = model.State == CharacterCardState.Selected;
        if (inUseStamp != null) inUseStamp.SetActive(selected);
        if (selectedBorder != null) selectedBorder.SetActive(selected);
        button.interactable = !empty;
        BindBackground(empty ? null : model.Rarity);
        bool locked = model.State == CharacterCardState.Locked;
        BindLocked(locked && !empty, model.Shards);
        if (empty) return;

        if (model.Portrait != null) portrait.sprite = model.Portrait;
        nameText.text = model.Name;
        levelText.text = model.Level.ToString(CultureInfo.InvariantCulture);
        BindStars(model.Stars);
    }

    // Chưa sở hữu: nền, nhãn và ảnh chuyển xám; level/sao ẩn, thanh mảnh hiện. Đủ mảnh: thanh đổi màu và viền sáng nhấp nháy.
    // Thẻ đã sở hữu bỏ material để về màu gốc (thẻ được dùng lại khi vẽ lại lưới).
    private void BindLocked(bool locked, ShardBarModel shards)
    {
        Material material = locked ? lockedMaterial : null;
        foreach (Graphic graphic in new Graphic[] { background, lightLayer, glow, rarityLabel, portrait })
            if (graphic != null) graphic.material = material;
        if (levelBadge != null) levelBadge.SetActive(!locked);
        if (starsGroup != null) starsGroup.SetActive(!locked);
        if (shardBar != null) shardBar.SetActive(locked);

        bool ready = locked && shards.Ready;
        readyPulse.Stop();
        if (readyBorder != null)
        {
            readyBorder.gameObject.SetActive(ready);
            if (ready)
                readyPulse = Tween.Alpha(readyBorder, 1f, 0.35f, 0.6f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
        }
        if (!locked) return;

        if (shardIcon != null && shards.Icon != null)
        {
            shardIcon.sprite = shards.Icon;
            shardIcon.preserveAspect = true; // Giữ tỉ lệ ảnh vuông của icon mảnh, không kéo giãn theo ô.
        }
        if (shardFill != null)
        {
            shardFill.fillAmount = shards.Required > 0 ? Mathf.Clamp01(shards.Current / (float)shards.Required) : 1f;
            shardFill.color = ready ? shardReadyColor : shardFillColor;
        }
        if (shardText != null)
            shardText.text = Math.Min(shards.Current, int.MaxValue).ToString(CultureInfo.InvariantCulture) + "/" +
                             shards.Required.ToString(CultureInfo.InvariantCulture);
    }

    // Tô 3 lớp nền (màu hoặc sprite) và nhãn độ hiếm theo cùng một mục, để thẻ luôn khớp nhãn.
    // Thẻ trống (rarity = null) về sprite gốc + màu xám mặc định; nhãn độ hiếm đã nằm trong hiddenWhenEmpty.
    private void BindBackground(RarityStyleDatabaseSO.CharacterStyle rarity)
    {
        EnsureStyledImages();
        if (rarity == null)
        {
            styledBackground.Reset(emptyBackground);
            return;
        }
        styledBackground.Apply(rarity.CardBackground);
        styledLight.Apply(rarity.CardLight);
        styledGlow.Apply(rarity.CardGlow);
        if (rarityLabel != null && rarity.Label != null) rarityLabel.sprite = rarity.Label;
        if (rarityText != null)
        {
            rarityText.text = rarity.Text;
            rarityText.color = rarity.TextColor;
        }
    }

    // Chụp sprite gốc của prefab ở lần Bind đầu, trước khi có style nào đổi sprite.
    private void EnsureStyledImages()
    {
        if (styledBackground != null) return;
        styledBackground = new StyledImage(background);
        styledLight = new StyledImage(lightLayer);
        styledGlow = new StyledImage(glow);
    }

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

    private void OnClick() => Clicked?.Invoke();

    #endregion
}
