using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>Phát một âm khi bấm nút. Gắn cạnh <see cref="Button"/>; mặc định âm ButtonClick.</summary>
/// <remarks>Âm nút nên khai báo ignorePause trong AudioDatabase để vẫn kêu khi game pause.</remarks>
[RequireComponent(typeof(Button))]
public sealed class ButtonSound : MonoBehaviour
{
    [SerializeField] private AudioId sound = AudioId.ButtonClick;

    private Button button;
    private IAudio audioPlayer;

    [Inject]
    private void Construct(IAudio audio) => audioPlayer = audio;

    private void Awake() => button = GetComponent<Button>();

    private void OnEnable() => button.onClick.AddListener(PlaySound);

    private void OnDisable() => button.onClick.RemoveListener(PlaySound);

    private void PlaySound() => audioPlayer?.Play(sound);
}
