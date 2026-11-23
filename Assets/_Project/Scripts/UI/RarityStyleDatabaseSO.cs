using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Một chỗ duy nhất để chỉnh màu và nhãn theo độ hiếm của UI, chia hai phần riêng:
/// <b>Character</b> (thẻ ở màn Collection, nhãn và nền màn Detail) và <b>Equipment</b> (khung thẻ đồ, nhãn trong popup).
/// </summary>
/// <remarks>
/// Hai theme (<see cref="CharacterScreenThemeSO"/>, <see cref="EquipmentThemeSO"/>) chỉ tham chiếu asset này và đọc qua
/// <c>GetRarity</c>, nên Controller/View không phải biết dữ liệu nằm ở đâu. Chỉ chứa dữ liệu hiển thị, không có trạng thái runtime.
/// </remarks>
[CreateAssetMenu(fileName = "RarityStyleDatabase", menuName = "Shooter Game/UI/Rarity Style Database")]
public sealed class RarityStyleDatabaseSO : ScriptableObject
{
    #region Entry Types

    /// <summary>Kiểu hiển thị một độ hiếm của nhân vật.</summary>
    [Serializable]
    public sealed class CharacterStyle
    {
        [SerializeField] private CharacterRarity rarity;

        [Header("Label (thẻ Collection + màn Detail)")]
        [SerializeField] private Sprite label;
        [SerializeField] private string text = "COMMON";
        [SerializeField] private Color textColor = Color.white;

        // Mỗi lớp hình chọn Color (giữ sprite gốc, tô màu) hoặc Sprite (kéo ảnh vào thay thế).
        [Header("Card Background (màn Collection)")]
        [Tooltip("Lớp nền chính của thẻ (Bg).")]
        [SerializeField] private ImageStyle cardBackground = new ImageStyle();
        [Tooltip("Lớp sáng trên nền thẻ (Bg/Gradient).")]
        [SerializeField] private ImageStyle cardLight = new ImageStyle();
        [Tooltip("Quầng sáng trên nền thẻ (Bg/Glow).")]
        [SerializeField] private ImageStyle cardGlow = new ImageStyle();

        [Header("Detail Screen")]
        [Tooltip("Nền cả màn Detail. Nếu tô màu, nên dùng tông tối để chữ và chỉ số nổi lên.")]
        [SerializeField] private ImageStyle screenBackground = new ImageStyle();
        [Tooltip("Lớp phát sáng của màn Detail (PanelEffect/PanelGlow), cùng tông với nền nhưng sáng hơn một bậc.")]
        [SerializeField] private ImageStyle screenGlow = new ImageStyle();

        public CharacterRarity Rarity => rarity;
        public Sprite Label => label;
        public string Text => text;
        public Color TextColor => textColor;

        /// <summary>Lớp nền chính của thẻ.</summary>
        public ImageStyle CardBackground => cardBackground;

        /// <summary>Lớp sáng trên nền thẻ.</summary>
        public ImageStyle CardLight => cardLight;

        /// <summary>Quầng sáng trên nền thẻ.</summary>
        public ImageStyle CardGlow => cardGlow;

        /// <summary>Nền cả màn Detail.</summary>
        public ImageStyle ScreenBackground => screenBackground;

        /// <summary>Lớp phát sáng của màn Detail.</summary>
        public ImageStyle ScreenGlow => screenGlow;
    }

    /// <summary>Kiểu hiển thị một độ hiếm của trang bị: màu các lớp khung thẻ đồ (ItemFrame03) và nhãn.</summary>
    [Serializable]
    public sealed class EquipmentStyle
    {
        [SerializeField] private EquipmentRarity rarity;

        [Header("Label (popup chi tiết)")]
        [SerializeField] private string text = "COMMON";
        [SerializeField] private Color textColor = Color.white;

        // Mỗi lớp khung chọn Color (giữ sprite gốc, tô màu) hoặc Sprite (kéo ảnh vào thay thế).
        [Header("Card Frame")]
        [SerializeField] private ImageStyle background = new ImageStyle();
        [SerializeField] private ImageStyle light = new ImageStyle();
        [SerializeField] private ImageStyle border = new ImageStyle();
        [SerializeField] private ImageStyle glow = new ImageStyle();
        [SerializeField] private ImageStyle cornerDeco = new ImageStyle();
        [Tooltip("Viền tròn quanh icon ô.")]
        [SerializeField] private ImageStyle slotCircle = new ImageStyle();

        public EquipmentRarity Rarity => rarity;
        public string Text => text;
        public Color TextColor => textColor;
        public ImageStyle Background => background;
        public ImageStyle Light => light;
        public ImageStyle Border => border;
        public ImageStyle Glow => glow;
        public ImageStyle CornerDeco => cornerDeco;
        public ImageStyle SlotCircle => slotCircle;
    }

    #endregion

    #region Configuration

    [Tooltip("CHARACTER: mỗi CharacterRarity một mục (nhãn, màu thẻ Collection, màu nền màn Detail).")]
    [SerializeField] private List<CharacterStyle> characterRarities = new List<CharacterStyle>();
    [Tooltip("EQUIPMENT: mỗi EquipmentRarity một mục (màu khung thẻ đồ, nhãn trong popup).")]
    [SerializeField] private List<EquipmentStyle> equipmentRarities = new List<EquipmentStyle>();

    #endregion

    #region Lookups

    /// <summary>Kiểu hiển thị của một độ hiếm nhân vật; ném lỗi nếu thiếu (Validate đã chặn trước).</summary>
    public CharacterStyle GetCharacter(CharacterRarity rarity)
    {
        foreach (CharacterStyle entry in characterRarities)
        {
            if (entry != null && entry.Rarity == rarity)
                return entry;
        }
        throw new KeyNotFoundException($"Rarity Style Database is missing character rarity {rarity}.");
    }

    /// <summary>Kiểu hiển thị của một độ hiếm trang bị; ném lỗi nếu thiếu (Validate đã chặn trước).</summary>
    public EquipmentStyle GetEquipment(EquipmentRarity rarity)
    {
        foreach (EquipmentStyle entry in equipmentRarities)
        {
            if (entry != null && entry.Rarity == rarity)
                return entry;
        }
        throw new KeyNotFoundException($"Rarity Style Database is missing equipment rarity {rarity}.");
    }

    #endregion

    #region Validation

    /// <summary>Đủ mọi độ hiếm của nhân vật và trang bị.</summary>
    public void Validate()
    {
        foreach (CharacterRarity rarity in (CharacterRarity[])Enum.GetValues(typeof(CharacterRarity)))
            GetCharacter(rarity);
        foreach (EquipmentRarity rarity in (EquipmentRarity[])Enum.GetValues(typeof(EquipmentRarity)))
            GetEquipment(rarity);
    }

    #endregion
}
