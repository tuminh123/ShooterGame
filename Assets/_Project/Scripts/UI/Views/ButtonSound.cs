using UnityEngine;
using UnityEngine.UI;

/// <summary>Phát một âm khi bấm nút. Gắn cạnh <see cref="Button"/>; mặc định âm ButtonClick.</summary>
/// <remarks>
/// Phát qua <see cref="UIAudio"/> trên Canvas cha, không inject, để nút trong prefab tạo bằng <c>Instantiate</c> cũng kêu.
/// Âm nút nên khai báo ignorePause trong AudioDatabase để vẫn kêu khi game pause. Chọn None để nút này im.
/// </remarks>
[RequireComponent(typeof(Button))]
public sealed class ButtonSound : MonoBehaviour
{
    [SerializeField] private AudioId sound = AudioId.ButtonClick;

    private Button button;
    private UIAudio uiAudio;

    private void Awake() => button = GetComponent<Button>();

    // Tìm lại mỗi lần bật: clone có thể được tạo dưới một parent tạm rồi mới gắn vào Canvas.
    private void OnEnable()
    {
        uiAudio = GetComponentInParent<UIAudio>(true);
        button.onClick.AddListener(PlaySound);
    }

    private void OnDisable() => button.onClick.RemoveListener(PlaySound);

    private void PlaySound()
    {
        if (uiAudio != null) uiAudio.Play(sound);
    }
}
