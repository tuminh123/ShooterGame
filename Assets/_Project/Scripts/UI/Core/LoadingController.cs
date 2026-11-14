using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

// Hiển thị tiến độ thật của prewarm; GameManager quyết định khi nào được vào Home.
public sealed class LoadingController : UIPanelController
{
    #region Scene References

    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text progressText;
    private PoolPrewarmService resources;
    private GameManager game;

    #endregion

    #region Progress And Visibility

    [Inject]
    public void Construct(PoolPrewarmService resources, GameManager game)
    {
        Unsubscribe();
        this.resources = resources;
        this.game = game;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (resources == null) return;
        resources.ProgressChanged -= ShowProgress;
        resources.ProgressChanged += ShowProgress;
        ShowProgress(resources.Progress);
    }

    private void Unsubscribe()
    {
        if (resources != null) resources.ProgressChanged -= ShowProgress;
    }

    private void ShowProgress(float value)
    {
        if (progressBar != null) progressBar.SetValueWithoutNotify(Mathf.Clamp01(value));
        if (progressText != null) progressText.text = $"Loading... {Mathf.RoundToInt(value * 100f)}%";
    }

    public override void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible) return;
        transform.SetAsLastSibling();
        ShowProgress(resources == null ? 0f : resources.Progress);
        if (game != null && game.State == GameState.Faulted && progressText != null)
            progressText.text = "Loading failed. Check Console.";
    }

    #endregion
}
