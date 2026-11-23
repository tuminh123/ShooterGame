using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel test gom các nút debug (Gacha trang bị, cộng Gold/Diamond, Reset Data). Thu gọn thành một nút "TEST" nhỏ ở mép màn để không che UI;
/// bấm vào thì mở hoặc đóng panel. Chỉ bật/tắt hiển thị, không gọi service.
/// </summary>
public sealed class TestPanel : MonoBehaviour
{
    #region References

    [Tooltip("Nút nhỏ luôn hiện, bấm để mở/đóng panel.")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleLabel;
    [Tooltip("Khung chứa các nút debug; tắt khi thu gọn.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private string openLabel = "TEST";
    [SerializeField] private string closeLabel = "CLOSE";

    #endregion

    #region Lifecycle

    // Mặc định thu gọn để không che màn chơi.
    private void Awake() => SetOpen(false);

    private void OnEnable() => toggleButton.onClick.AddListener(Toggle);

    private void OnDisable() => toggleButton.onClick.RemoveListener(Toggle);

    #endregion

    #region Presentation

    private void Toggle() => SetOpen(!panel.activeSelf);

    private void SetOpen(bool open)
    {
        panel.SetActive(open);
        if (toggleLabel != null)
            toggleLabel.text = open ? closeLabel : openLabel;
    }

    #endregion
}
