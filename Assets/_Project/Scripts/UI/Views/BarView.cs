using UnityEngine;
using UnityEngine.UI;

/// <summary>View dùng Slider chuẩn của bộ UI mới; không giữ dữ liệu hay tự tạo UI lúc chạy.</summary>
public sealed class BarView : MonoBehaviour
{
    #region References

    [SerializeField] private Slider slider;
    [SerializeField] private ShieldBorderGraphic shieldBorder;

    #endregion

    #region Layout

    /// <summary>Đặt kích thước thanh và độ dày viền giáp theo cấu hình của nhân vật.</summary>
    public void ApplyLayout(Vector2 size, float shieldThickness)
    {
        ((RectTransform)transform).sizeDelta = size;
        if (shieldBorder != null) shieldBorder.SetThickness(shieldThickness);
    }

    #endregion

    #region Vitals Presentation

    /// <summary>Hiển thị tỉ lệ current/maximum; maximum không dương thì thanh rỗng.</summary>
    public void SetValue(float current, float maximum)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f);
    }

    /// <summary>Hiển thị tỉ lệ giáp trên viền; maximum không dương thì viền rỗng.</summary>
    public void SetShield(float current, float maximum)
    {
        if (shieldBorder != null)
            shieldBorder.SetRatio(maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f);
    }

    /// <summary>Bật/tắt cả thanh.</summary>
    public void SetVisible(bool visible) => gameObject.SetActive(visible);
    #endregion
}
