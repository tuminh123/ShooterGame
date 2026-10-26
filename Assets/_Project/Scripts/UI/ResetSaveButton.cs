using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

[RequireComponent(typeof(Button))]
public sealed class ResetSaveButton : MonoBehaviour
{
    private SaveManager saves;
    private Button button;
    private bool reloading;

    [Inject]
    public void Construct(SaveManager saves) => this.saves = saves;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ResetAndReload);
    }

    private void ResetAndReload()
    {
        if (reloading)
            return;

        Scene scene = gameObject.scene;
        if (!Application.CanStreamedLevelBeLoaded(scene.path))
        {
            Debug.LogError("Cannot reset: add the current scene to Build Settings.", this);
            return;
        }
        if (!saves.ResetData())
            return;

        reloading = true;
        button.interactable = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene.path);
    }

    private void OnDestroy() => button.onClick.RemoveListener(ResetAndReload);
}
