using UnityEngine;
using Zenject;

// Điều phối UI của scene: nghe gameplay và truyền dữ liệu cho view chung; không giữ tiến trình trận.
public sealed class UIManager : MonoBehaviour
{
    #region Inspector References

    [SerializeField] private BarView experienceBar; // Nạp tiến trình EXP vào view chung.
    [SerializeField] private TextView goldText; // Chỉ hiển thị chuỗi do manager chuẩn bị.
    [SerializeField] private TextView timeText; // Cùng TextView với vàng/level, khác dữ liệu và vị trí.
    [SerializeField] private TextView levelText;

    #endregion

    #region Dependencies & Subscriptions

    private LevelManager levels;
    private RunSessionManager session; // Vàng và đồng hồ do gameplay quản lý, không phụ thuộc hệ thống nhặt đồ.
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
        if (levels != null) Subscribe(); // Zenject có thể inject sau OnEnable đầu tiên của scene.
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        levels.RunExperienceChanged += RefreshExperience;
        session.GoldChanged += RefreshGold;
        session.TimeChanged += RefreshTime;
        RefreshExperience(levels.RunExperience, levels.RunExperienceToNextLevel);
        RefreshGold(session.Gold);
        RefreshTime(session.ElapsedSeconds);
    }

    private void OnDisable()
    {
        if (!subscribed) return;
        subscribed = false;
        levels.RunExperienceChanged -= RefreshExperience;
        session.GoldChanged -= RefreshGold;
        session.TimeChanged -= RefreshTime;
    }

    #endregion

    #region Run Counters

    private void RefreshExperience(int experience, int required)
    {
        // Đạt trần level thì hiển thị đầy; BarView không cần biết quy tắc level của game.
        experienceBar.SetValue(required > 0 ? experience : 1, required > 0 ? required : 1);
        levelText.SetText($"LV {levels.RunLevel}");
    }

    private void RefreshGold(int amount) => goldText.SetText($"GOLD {amount}");

    private void RefreshTime(int seconds) => timeText.SetText($"{seconds / 60:00}:{seconds % 60:00}"); // Chỉ đổi khi có nhịp mới.

    #endregion

    #region UI Visibility

    // HUD có sẵn trong Canvas; bật lại sẽ đăng ký event và đọc dữ liệu hiện tại, không spawn hoặc dùng pool.
    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    #endregion
}
