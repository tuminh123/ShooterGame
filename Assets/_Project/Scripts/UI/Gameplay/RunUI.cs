using UnityEngine;
using Zenject;

/// <summary>UI tiến trình trận tự đăng ký dữ liệu; UIManager chỉ giữ/kết nối các UI.</summary>
public sealed class RunUI : MonoBehaviour
{
    #region References And Dependencies

    [SerializeField] private BarView experienceBar;
    [SerializeField] private TextView goldText;
    [SerializeField] private TextView timeText;
    [SerializeField] private TextView levelText;
    [SerializeField] private TextView defeatedText;
    private IRunReader run;
    private bool subscribed;

    #endregion

    #region Injection And Subscription

    /// <summary>SceneContext inject nguồn dữ liệu tiến trình trận.</summary>
    [Inject]
    public void Construct(IRunReader runReader)
    {
        this.run = runReader;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        if (run != null) Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        run.ExperienceChanged += RefreshExperience;
        run.GoldChanged += RefreshGold;
        run.TimeChanged += RefreshTime;
        run.EnemiesDefeatedChanged += RefreshDefeated;
        RefreshExperience(run.Experience, run.ExperienceToNextLevel);
        RefreshGold(run.Gold);
        RefreshTime(run.ElapsedSeconds);
        RefreshDefeated(run.EnemiesDefeated);
    }

    private void OnDisable()
    {
        if (!subscribed) return;
        subscribed = false;
        run.ExperienceChanged -= RefreshExperience;
        run.GoldChanged -= RefreshGold;
        run.TimeChanged -= RefreshTime;
        run.EnemiesDefeatedChanged -= RefreshDefeated;
    }

    #endregion

    #region HUD Presentation

    private void RefreshExperience(int current, int required)
    {
        experienceBar.SetValue(required > 0 ? current : 1, required > 0 ? required : 1);
        levelText.SetText(run.Level.ToString());
    }

    private void RefreshGold(int amount) => goldText.SetText(amount.ToString());
    private void RefreshTime(int seconds) => timeText.SetText($"{seconds / 60:00}:{seconds % 60:00}");
    private void RefreshDefeated(int count) => defeatedText.SetText(count.ToString());
    #endregion
}
