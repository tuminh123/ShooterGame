using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Thanh bar dưới dùng chung của Home và màn Character: nút Battle (về Home) và nút Heroes (mở màn Character).
/// Nút của màn đang mở "nổi lên" bằng cách bật bản Focus và tắt bản thường, giống mặc định ở menu.
/// </summary>
/// <remarks>
/// Nằm ngoài MainUI (sau CharacterScreen trong hierarchy) vì UIManager tắt MainUI khi mở màn Character,
/// mà thanh bar phải còn để bấm về Home. Ở trang Detail thanh bar trượt xuống ẩn để không che footer của Detail.
/// Component đặt trên object luôn bật (HomeNavigation); bản thân thanh bar được bật/tắt bằng SlideTransition.
/// </remarks>
public sealed class HomeNavigationBar : MonoBehaviour
{
    #region Types

    /// <summary>Một nút tab: bản thường và bản nổi (Focus); chỉ một trong hai được bật.</summary>
    [Serializable]
    private sealed class NavTab
    {
        public Button normal;
        public Button focused;

        public void SetFocused(bool value)
        {
            normal.gameObject.SetActive(!value);
            focused.gameObject.SetActive(value);
        }

        public void SetInteractable()
        {
            normal.interactable = true;
            focused.interactable = true;
        }
    }

    #endregion

    #region References

    [Tooltip("Thanh bar (BottomBar_Menu); cần SlideTransition để trượt xuống/lên.")]
    [SerializeField] private SlideTransition bar;
    [Tooltip("Nền dùng chung của Home và màn Character; hiện suốt trạng thái Home để lúc trượt không lộ bản đồ.")]
    [SerializeField] private GameObject sharedBackground;
    [SerializeField] private NavTab battleTab;
    [SerializeField] private NavTab heroesTab;
    [Tooltip("Các nút chưa có chức năng (Shop, Bag, Map...); bị khóa.")]
    [SerializeField] private Button[] lockedButtons = Array.Empty<Button>();
    [SerializeField] private CharacterScreenController characterScreen;

    #endregion

    #region State

    private IGameState game;
    private bool barShown;
    private bool barInitialized; // Lần áp dụng đầu bật/tắt ngay, không trượt.
    private bool wasHome; // Trạng thái Home lần Refresh trước, để nhận ra lúc vào trận / về Home.

    #endregion

    #region Lifecycle

    [Inject]
    public void Construct(IGameState gameState)
    {
        Unsubscribe();
        game = gameState;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        battleTab.normal.onClick.AddListener(OnBattle);
        heroesTab.normal.onClick.AddListener(OnHeroes);
        // Scene lưu các nút mockup ở trạng thái khóa; trước đây HomeController mở khóa khi chúng còn nằm trong MainUI.
        // Thanh bar giờ tự mở khóa nút của nó (cả bản nổi, để không bị tô màu xám "disabled").
        battleTab.SetInteractable();
        heroesTab.SetInteractable();
        foreach (Button button in lockedButtons)
            if (button != null) button.interactable = false;
        Subscribe();
    }

    private void OnDisable()
    {
        battleTab.normal.onClick.RemoveListener(OnBattle);
        heroesTab.normal.onClick.RemoveListener(OnHeroes);
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (game == null) return;
        Unsubscribe(); // Tránh đăng ký hai lần khi Construct và OnEnable cùng chạy.
        game.StateChanged += OnGameStateChanged;
        characterScreen.NavigationChanged += Refresh;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (game != null) game.StateChanged -= OnGameStateChanged;
        if (characterScreen != null) characterScreen.NavigationChanged -= Refresh;
    }

    #endregion

    #region Navigation

    // Bản Focus không cần listener: bấm vào nút của màn đang mở thì không làm gì.
    private void OnBattle() => characterScreen.Close();

    private void OnHeroes() => characterScreen.Open();

    private void OnGameStateChanged(GameState previous, GameState current) => Refresh();

    // Một chỗ quyết định mọi thứ của thanh bar từ trạng thái game và màn Character.
    private void Refresh()
    {
        bool home = game != null && game.State == GameState.Home;
        bool heroesOpen = characterScreen.IsShown;
        if (sharedBackground != null) sharedBackground.SetActive(home);
        battleTab.SetFocused(!heroesOpen);
        heroesTab.SetFocused(heroesOpen);
        // Rời Home (vào trận) hoặc quay về Home: bật/tắt ngay cùng Home lúc màn đã tối, không trượt để khỏi hở đáy màn.
        // Chỉ trượt khi vẫn ở trong menu (vào/ra Detail).
        bool instant = home != wasHome;
        wasHome = home;
        SetBarShown(home && !(heroesOpen && characterScreen.IsOnDetail), instant);
    }

    private void SetBarShown(bool show, bool instant)
    {
        if (barInitialized && show == barShown) return;
        if (!barInitialized || instant) bar.SetVisibleInstant(show);
        else if (show) bar.PlayIn();
        else bar.PlayOut();
        barInitialized = true;
        barShown = show;
    }

    #endregion
}
