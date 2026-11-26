using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Kiểu hiển thị số sát thương bay lên: font, màu, cỡ, nhịp chuyển động và giới hạn số lượng.
/// Chỉ chứa tham số; DamageTextSystem (miền Character) đọc để dựng mesh.
/// </summary>
[CreateAssetMenu(fileName = "DamageTextStyle", menuName = "Shooter Game/Characters/Damage Text Style")]
public sealed class DamageTextStyleSO : ScriptableObject
{
    #region Fields

    [Header("Font")]
    [Tooltip("Font SDF của TextMeshPro; số được vẽ bằng atlas của font này (cần có sẵn 0-9 . K M !).")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Trống = material mặc định của font.")]
    [SerializeField] private Material material;
    [SerializeField] private string sortingLayer = "Default";
    [Tooltip("Vẽ trên nhân vật và đạn.")]
    [SerializeField] private int sortingOrder = 1000;

    [Header("Cỡ (world unit)")]
    [Tooltip("Chiều cao chữ số của đòn thường. Camera hiện ~533 px mỗi unit; quái nhỏ cao ~0.17 unit.")]
    [SerializeField, Min(0.01f)] private float size = 0.1f;
    [Tooltip("Hệ số cỡ của đòn chí mạng.")]
    [SerializeField, Min(1f)] private float criticalScale = 1.3f;
    [Tooltip("Số hiện cao hơn tâm hitbox bao nhiêu (unit).")]
    [SerializeField] private float headOffset = 0.08f;

    [Header("Màu")]
    [SerializeField] private Color enemyHit = Color.white;
    [SerializeField] private Color critical = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color playerHit = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("Đòn chỉ trừ giáp, chưa mất HP.")]
    [SerializeField] private Color shieldHit = new Color(0.6f, 0.85f, 1f, 1f);

    [Header("Chuyển động (giây, theo thời gian game)")]
    [SerializeField, Min(0.1f)] private float lifetime = 0.7f;
    [Tooltip("Bay lên bao xa trong cả thời gian sống (unit).")]
    [SerializeField] private float rise = 0.15f;
    [Tooltip("Lệch ngang ngẫu nhiên tối đa, để các số không chồng lên nhau.")]
    [SerializeField, Min(0f)] private float drift = 0.06f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.1f;
    [Tooltip("Phóng quá cỡ lúc nảy ra (đòn chí mạng nảy mạnh hơn một nấc).")]
    [SerializeField, Min(1f)] private float popScale = 1.25f;
    [Tooltip("Bắt đầu mờ dần từ phần này của thời gian sống.")]
    [SerializeField, Range(0f, 1f)] private float fadeStart = 0.7f;

    [Header("Giới hạn")]
    [Tooltip("Số tối đa cùng lúc; đầy thì thay số cũ nhất.")]
    [SerializeField, Range(8, 256)] private int maxCount = 64;
    [Tooltip("Cùng mục tiêu trúng liên tiếp trong khoảng này thì cộng dồn vào số đang hiện (OrbitSaw, cháy, độc).")]
    [SerializeField, Min(0f)] private float mergeWindow = 0.15f;

    #endregion

    #region Getters

    public TMP_FontAsset Font => font;
    public Material Material => material != null ? material : font != null ? font.material : null;
    public string SortingLayer => sortingLayer;
    public int SortingOrder => sortingOrder;
    public float Size => size;
    public float CriticalScale => criticalScale;
    public float HeadOffset => headOffset;
    public Color EnemyHit => enemyHit;
    public Color Critical => critical;
    public Color PlayerHit => playerHit;
    public Color ShieldHit => shieldHit;
    public float Lifetime => lifetime;
    public float Rise => rise;
    public float Drift => drift;
    public float PopDuration => popDuration;
    public float PopScale => popScale;
    public float FadeStart => fadeStart;
    public int MaxCount => maxCount;
    public float MergeWindow => mergeWindow;

    #endregion

    #region Validation

    /// <summary>Ném lỗi nếu thiếu font hoặc font thiếu ký tự cần vẽ.</summary>
    public void Validate()
    {
        if (font == null) throw new InvalidOperationException("Damage Text Style needs a TMP font: " + name);
        if (Material == null) throw new InvalidOperationException("Damage Text Style needs a material: " + name);
        foreach (char character in DamageTextSystem.Characters)
            if (!font.characterLookupTable.ContainsKey(character))
                throw new InvalidOperationException("Damage text font " + font.name + " has no '" + character + "' glyph.");
    }

    #endregion
}
