using UnityEngine;
using UnityEngine.UI;

// View dùng Slider chuẩn của bộ UI mới; không giữ dữ liệu hay tự tạo UI lúc chạy.
public sealed class BarView : MonoBehaviour
{
    #region References

    [SerializeField] private Slider slider;
    [SerializeField] private ShieldBorderGraphic shieldBorder;

    #endregion

    #region Layout

    public void ApplyLayout(Vector2 size, float shieldThickness)
    {
        ((RectTransform)transform).sizeDelta = size;
        if (shieldBorder != null) shieldBorder.SetThickness(shieldThickness);
    }

    #endregion

    #region Vitals Presentation

    public void SetValue(float current, float maximum)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f);
    }

    public void SetShield(float current, float maximum)
    {
        if (shieldBorder != null)
            shieldBorder.SetRatio(maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f);
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);
    #endregion
}
