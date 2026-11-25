using UnityEngine;
using Zenject;

/// <summary>UI tiến trình trận tự đăng ký dữ liệu; UIManager chỉ giữ/kết nối các UI.</summary>
public sealed class BattleHud : MonoBehaviour
{
    #region References And Dependencies

    [SerializeField] private BarView experienceBar;
    [SerializeField] private TextView goldText;
    [SerializeField] private TextView timeText;
    [SerializeField] private TextView levelText;
    [SerializeField] private TextView defeatedText;
    private IBattleProgress battle;
    private bool subscribed;

    #endregion

    #region Injection And Subscription

    /// <summary>SceneContext inject nguồn dữ liệu tiến trình trận.</summary>
    [Inject]
    public void Construct(IBattleProgress battleProgress)
    {
        this.battle = battleProgress;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        if (battle != null) Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        battle.ExperienceChanged += RefreshExperience;
        battle.GoldChanged += RefreshGold;
        battle.TimeChanged += RefreshTime;
        battle.EnemiesDefeatedChanged += RefreshDefeated;
        RefreshExperience(battle.Experience, battle.ExperienceToNextLevel);
        RefreshGold(battle.Gold);
        RefreshTime(battle.ElapsedSeconds);
        RefreshDefeated(battle.EnemiesDefeated);
    }

    private void OnDisable()
    {
        if (!subscribed) return;
        subscribed = false;
        battle.ExperienceChanged -= RefreshExperience;
        battle.GoldChanged -= RefreshGold;
        battle.TimeChanged -= RefreshTime;
        battle.EnemiesDefeatedChanged -= RefreshDefeated;
    }

    #endregion

    #region HUD Presentation

    private void RefreshExperience(int current, int required)
    {
        experienceBar.SetValue(required > 0 ? current : 1, required > 0 ? required : 1);
        levelText.SetText(battle.Level.ToString());
    }

    private void RefreshGold(int amount) => goldText.SetText(amount.ToString());
    private void RefreshTime(int seconds) => timeText.SetText($"{seconds / 60:00}:{seconds % 60:00}");
    private void RefreshDefeated(int count) => defeatedText.SetText(count.ToString());
    #endregion
}
