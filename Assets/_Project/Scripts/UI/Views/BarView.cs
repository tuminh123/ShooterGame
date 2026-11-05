using HeyHeyThere.PixelUIFantasyFree;
using UnityEngine;
using UnityEngine.UI;

// View thanh chung cho HP/giáp/EXP hoặc dữ liệu khác; không biết manager, nhân vật hay quy tắc gameplay.
public sealed class BarView : MonoBehaviour
{
    #region References

    [SerializeField] private PixelBar bar; // Thanh Parchment đã có hình nền và sprite fill riêng.
    private Image fillImage; // Chỉ tra khi cần đổi màu, không GetComponent mỗi lần nạp giá trị.

    #endregion

    #region Presentation

    public void SetValue(float current, float maximum)
        => bar.value = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

    public void SetColor(Color color)
    {
        if (fillImage == null) fillImage = bar.fill.GetComponent<Image>();
        fillImage.color = color;
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    #endregion
}
