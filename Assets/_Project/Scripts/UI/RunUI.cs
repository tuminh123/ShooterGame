using UnityEngine;
using Zenject;

// UI tiến trình trận tự đăng ký dữ liệu; UIManager chỉ giữ/kết nối các UI.
public sealed class RunUI : MonoBehaviour
{
    [SerializeField] private BarView experienceBar;
    [SerializeField] private TextView goldText;
    [SerializeField] private TextView timeText;
    [SerializeField] private TextView levelText;
    [SerializeField] private TextView defeatedText;
    private LevelManager levels;
    private RunSessionManager session;
    private bool subscribed;

    [Inject]
    public void Construct(LevelManager levels, RunSessionManager session)
    {
        this.levels = levels;
        this.session = session;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        if (levels != null) Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        levels.RunExperienceChanged += RefreshExperience;
        session.GoldChanged += RefreshGold;
        session.TimeChanged += RefreshTime;
        session.EnemiesDefeatedChanged += RefreshDefeated;
        RefreshExperience(levels.RunExperience, levels.RunExperienceToNextLevel);
        RefreshGold(session.Gold);
        RefreshTime(session.ElapsedSeconds);
        RefreshDefeated(session.EnemiesDefeated);
    }

    private void OnDisable()
    {
        if (!subscribed) return;
        subscribed = false;
        levels.RunExperienceChanged -= RefreshExperience;
        session.GoldChanged -= RefreshGold;
        session.TimeChanged -= RefreshTime;
        session.EnemiesDefeatedChanged -= RefreshDefeated;
    }

    private void RefreshExperience(int current, int required)
    {
        experienceBar.SetValue(required > 0 ? current : 1, required > 0 ? required : 1);
        levelText.SetText(levels.RunLevel.ToString());
    }

    private void RefreshGold(int amount) => goldText.SetText(amount.ToString());
    private void RefreshTime(int seconds) => timeText.SetText($"{seconds / 60:00}:{seconds % 60:00}");
    private void RefreshDefeated(int count) => defeatedText.SetText(count.ToString());
}
