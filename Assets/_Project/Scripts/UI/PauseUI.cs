using UnityEngine;
using UnityEngine.UI;
using Zenject;

// Nút pause tự quản lý click; không để UIManager xử lý hành vi của từng nút.
public sealed class PauseUI : MonoBehaviour
{
    [SerializeField] private Button button;
    private GameplayPauseService pauses;
    private System.IDisposable pauseLease;

    [Inject]
    public void Construct(GameplayPauseService pauses) => this.pauses = pauses;

    private void OnEnable() => button.onClick.AddListener(Toggle);
    private void OnDisable()
    {
        button.onClick.RemoveListener(Toggle);
        Resume();
    }

    private void Toggle()
    {
        if (pauseLease != null) { Resume(); return; }
        pauseLease = pauses.Acquire();
    }

    public void Resume()
    {
        System.IDisposable previous = pauseLease;
        pauseLease = null;
        previous?.Dispose();
    }
}
