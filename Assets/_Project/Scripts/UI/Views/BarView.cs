
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// View thanh chung cho HP/giáp/EXP hoặc dữ liệu khác; không biết manager, nhân vật hay quy tắc gameplay.
public sealed class BarView : MonoBehaviour
{
    #region References

    //[SerializeField] private PixelBar bar; // Thanh Parchment đã có hình nền và sprite fill riêng.
    private Image fillImage; // Chỉ tra khi cần đổi màu, không GetComponent mỗi lần nạp giá trị.
    [FormerlySerializedAs("shieldOverlay")]
    [SerializeField] private Image shieldFill; // Gán sẵn: Image Filled/Horizontal, cùng kích thước thanh máu.
    private bool reportedMissingBar;

    #endregion

    #region Presentation

    //private void Reset() => bar = GetComponent<PixelBar>();

    // private void OnValidate()
    // {
    //     if (bar == null) bar = GetComponent<PixelBar>();
    // }

    public void SetValue(float current, float maximum)
    {
        if (!ResolveBar()) return;
        bar.value = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    }

    private bool ResolveBar()
    {
        if (bar == null) bar = GetComponent<PixelBar>();
        if (bar != null) return true;
        if (!reportedMissingBar)
        {
            reportedMissingBar = true;
            Debug.LogError("BarView: thiếu PixelBar. Gán component PixelBar vào ô Bar trên thanh UI này.", this);
        }
        return false;
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    // Giáp dùng chiều dài chuẩn hóa của cả thanh máu, không phụ thuộc lượng HP còn lại.
    public void SetShield(float current, float maximum)
    {
        if (shieldFill == null) return;
        // Bố cục, màu và kiểu fill chỉnh sẵn trong Inspector; code chỉ nạp dữ liệu khi có event.
        // Không cần tự tạo object, đổi Transform hay chạy tween mỗi frame cho fill phụ này.
        shieldFill.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        bool visible = shieldFill.fillAmount > 0f;
        if (shieldFill.gameObject.activeSelf != visible) shieldFill.gameObject.SetActive(visible);
    }

    #endregion
}
