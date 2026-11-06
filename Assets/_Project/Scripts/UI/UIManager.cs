using UnityEngine;

// Trạm trung tâm giữ các UI trong scene; dữ liệu trận do RunUI, HP/giáp do CharacterUI quản lý.
public sealed class UIManager : MonoBehaviour
{
    [SerializeField] private RunUI runUI;
    [SerializeField] private PauseUI pauseUI;

    public void SetVisible(bool visible)
    {
        if (pauseUI != null && !visible) pauseUI.Resume();
        if (runUI != null) runUI.gameObject.SetActive(visible);
    }
}