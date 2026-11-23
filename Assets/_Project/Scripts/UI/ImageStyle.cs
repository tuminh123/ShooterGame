using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cách tô một lớp hình UI theo độ hiếm: <see cref="ImageStyleMode.Color"/> = giữ sprite gốc của prefab và tô màu,
/// <see cref="ImageStyleMode.Sprite"/> = thay bằng sprite kéo vào (không tô màu, giữ đúng màu của ảnh).
/// </summary>
/// <remarks>
/// Dùng cho mọi lớp nền/khung trong <see cref="RarityStyleDatabaseSO"/>. Chỉ là dữ liệu; việc nhớ sprite gốc để quay lại
/// khi đổi từ Sprite về Color nằm ở <see cref="StyledImage"/> phía View.
/// </remarks>
[Serializable]
public sealed class ImageStyle
{
    #region Fields

    [Tooltip("Color: giữ sprite gốc, tô màu bên dưới. Sprite: thay bằng sprite kéo vào.")]
    [SerializeField] private ImageStyleMode mode = ImageStyleMode.Color;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Sprite sprite;

    #endregion

    #region Constructors

    /// <summary>Constructor mặc định cho Unity: tô màu trắng (không đổi gì).</summary>
    public ImageStyle() { }

    /// <summary>Tạo kiểu tô màu (dùng khi điền asset bằng code).</summary>
    public ImageStyle(Color color)
    {
        mode = ImageStyleMode.Color;
        this.color = color;
    }

    #endregion

    #region Queries

    /// <summary>Tô màu hay thay sprite.</summary>
    public ImageStyleMode Mode => mode;

    /// <summary>Màu dùng ở chế độ Color.</summary>
    public Color Color => color;

    /// <summary>Sprite dùng ở chế độ Sprite.</summary>
    public Sprite Sprite => sprite;

    #endregion

    #region Apply

    /// <summary>
    /// Tô <paramref name="image"/>. Chế độ Sprite mà chưa kéo sprite thì quay về tô màu, để quên kéo ảnh không làm mất hình.
    /// <paramref name="originalSprite"/> là sprite gốc của prefab, dùng lại ở chế độ Color.
    /// </summary>
    public void Apply(Image image, Sprite originalSprite)
    {
        if (image == null) return;
        if (mode == ImageStyleMode.Sprite && sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white; // Không tô: giữ đúng màu của ảnh kéo vào.
            return;
        }
        image.sprite = originalSprite;
        image.color = color;
    }

    #endregion
}

/// <summary>
/// Một Image trên View được tô theo <see cref="ImageStyle"/>. Nhớ sprite gốc của prefab ngay lần đầu,
/// để thẻ dùng lại (lưới, pool) đổi từ Sprite về Color hoặc về trạng thái trống vẫn đúng hình ban đầu.
/// </summary>
public sealed class StyledImage
{
    private readonly Image image;
    private readonly Sprite originalSprite;

    /// <summary>Gọi trước khi đổi sprite lần nào (thường lúc Bind đầu tiên), để chụp đúng sprite của prefab.</summary>
    public StyledImage(Image image)
    {
        this.image = image;
        originalSprite = image != null ? image.sprite : null;
    }

    /// <summary>Tô theo kiểu của độ hiếm.</summary>
    public void Apply(ImageStyle style) => style?.Apply(image, originalSprite);

    /// <summary>Về sprite gốc và tô <paramref name="color"/> (dùng cho thẻ trống).</summary>
    public void Reset(Color color)
    {
        if (image == null) return;
        image.sprite = originalSprite;
        image.color = color;
    }
}
