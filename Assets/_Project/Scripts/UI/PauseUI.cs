using UnityEngine;
using UnityEngine.UI;

// Nút pause tự quản lý click; không để UIManager xử lý hành vi của từng nút.
public sealed class PauseUI : MonoBehaviour
{
    [SerializeField] private Button button;
    private bool paused;
    private float previousTimeScale;

    private void OnEnable() => button.onClick.AddListener(Toggle);
    private void OnDisable()
    {
        button.onClick.RemoveListener(Toggle);
        Resume();
    }

    private void Toggle()
    {
        if (paused) { Resume(); return; }
        previousTimeScale = Time.timeScale;
        paused = true;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;
        Time.timeScale = previousTimeScale;
    }
}
