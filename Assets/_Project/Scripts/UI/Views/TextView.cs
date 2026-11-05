using TMPro;
using UnityEngine;

// View text chung: chỉ hiển thị dữ liệu đã định dạng, không tự tính vàng, thời gian hoặc level.
public sealed class TextView : MonoBehaviour
{
    #region References

    [SerializeField] private TMP_Text label; // Font, cỡ chữ, màu và căn lề được cấu hình trên prefab.

    #endregion

    #region Presentation

    public void SetText(string text) => label.text = text;
    public void SetColor(Color color) => label.color = color;
    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    #endregion
}
