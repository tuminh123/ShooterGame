using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

/// <summary>Nút debug: xóa save rồi tải lại scene để thử luồng người chơi mới.</summary>
[RequireComponent(typeof(Button))]
public sealed class ResetSaveButton : MonoBehaviour
{
    #region Dependencies And State
    private ISaveMaintenance saves;
    private Button button;
    private bool reloading;

    #endregion

    #region Injection And Initialization

    /// <summary>SceneContext inject service bảo trì save.</summary>
    [Inject]
    public void Construct(ISaveMaintenance saveMaintenance) => this.saves = saveMaintenance;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ResetAndReload);
    }

    #endregion

    #region Reset And Reload

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

    #endregion

    #region Cleanup

    private void OnDestroy() => button.onClick.RemoveListener(ResetAndReload);
    #endregion
}
