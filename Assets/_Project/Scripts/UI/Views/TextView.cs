using TMPro;
using UnityEngine;

/// <summary>View text chung: chỉ hiển thị dữ liệu đã định dạng, không tự tính vàng, thời gian hoặc level.</summary>
public sealed class TextView : MonoBehaviour
{
    #region References

    [SerializeField] private TMP_Text label; // Font, cỡ chữ, màu và căn lề được cấu hình trên prefab.

    #endregion

    #region Presentation

    /// <summary>Hiển thị chuỗi đã định dạng sẵn.</summary>
    public void SetText(string text) => label.text = text;
    /// <summary>Đổi màu chữ.</summary>
    public void SetColor(Color color) => label.color = color;
    /// <summary>Bật/tắt text.</summary>
    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    #endregion
}
