using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Home tạm thời: nút Play hoạt động, giữa màn hiện nhân vật đang chọn; giữ bố cục có sẵn cho các tính năng nối sau.
/// Thanh bar dưới (mở màn Character, về Home) do HomeNavigationBar quản lý, nằm ngoài MainUI để hiện cả ở màn Character.
/// Số tiền do CurrencyBarView trên Topbar dùng chung hiển thị (Topbar nằm ngoài MainUI, HomeNavigationBar bật/tắt).
/// </summary>
public sealed class HomeController : UIPanelController
{
    #region References

    [SerializeField] private Button playButton;
    [Tooltip("Hiệu ứng nhún của nút Play; chạy xong mới bắt đầu trận, dùng unscaled time khi Home đang pause.")]
    [SerializeField] private SinglePanelPopupTween playTween;

    [Header("Nhân vật đang chọn (giữa Home)")]
    [Tooltip("Khung đặt nhân vật UI (Middle/Character); ảnh của nó là ảnh dự phòng khi nhân vật chưa có prefab UI.")]
    [SerializeField] private Image characterFrame;
    [SerializeField] private TMP_Text characterName;
    [Tooltip("Thanh EXP nâng cấp của cấp hiện tại, cùng nghĩa với thanh ở màn Detail.")]
    [SerializeField] private Slider levelSlider;
    [SerializeField] private TMP_Text levelValueText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string maxLevelText = "MAX";

    [Header("Mission")]
    [Tooltip("Nút Quest mở màn Mission; để trống thì bỏ qua.")]
    [SerializeField] private Button questButton;
    [Tooltip("Chấm báo trên nút Quest khi có thưởng chưa nhận.")]
    [SerializeField] private GameObject questAlert;
    [SerializeField] private MissionScreenController missionScreen;

    #endregion

    #region Dependencies And State

    private IGameState game;
    private IScreenTransition transition;
    private IPlayerSelection selection;
    private ICharacterUpgrades upgrades;
    private IMissions missions;
    private CharacterPreviewSlot preview;
    private bool starting;
    private bool subscribed;

    #endregion

    #region Lifecycle

    /// <summary>SceneContext inject các service cần cho màn Home.</summary>
    [Inject]
    public void Construct(IGameState gameState, IScreenTransition screenTransition,
        IPlayerSelection playerSelection, ICharacterUpgrades characterUpgrades, IMissions missionService)
    {
        Unsubscribe();
        game = gameState;
        transition = screenTransition;
        selection = playerSelection;
        upgrades = characterUpgrades;
        missions = missionService;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (questButton != null) questButton.onClick.AddListener(OpenMissions);
        Subscribe();
    }

    private void OnDisable()
    {
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        if (questButton != null) questButton.onClick.RemoveListener(OpenMissions);
        Unsubscribe();
        starting = false;
        if (playTween != null) playTween.StopAndReset();
    }

    // Đổi nhân vật hoặc nâng cấp ở màn Character thì giữa Home cập nhật theo (Home có thể đang trượt về).
    private void Subscribe()
    {
        if (subscribed || selection == null) return;
        subscribed = true;
        selection.SelectionChanged += OnSelectionChanged;
        upgrades.Upgraded += OnUpgraded;
        missions.Changed += DrawQuestAlert;
        DrawCharacter();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        selection.SelectionChanged -= OnSelectionChanged;
        upgrades.Upgraded -= OnUpgraded;
        missions.Changed -= DrawQuestAlert;
    }

    private void OnSelectionChanged(CharacterType type) => DrawCharacter();
    private void OnUpgraded(CharacterType type, CharacterUpgradeKind kind) => DrawCharacter();

    #endregion

    #region Visibility

    // Chỉ trượt khi đổi qua lại với màn menu (UIManager gọi hai bên cùng lúc nên hai màn đẩy nhau): PlayTransition = true.
    // Vào trận / từ trận về Home thì bật/tắt ngay: ScreenTransition đã làm màn tối dần, UIManager truyền PlayTransition = false.
    public override void SetVisible(bool visible)
    {
        bool hasSlide = TryGetComponent(out SlideTransition slide);
        if (!visible)
        {
            if (hasSlide && PlayTransition) slide.PlayOut();
            else if (hasSlide) slide.SetVisibleInstant(false);
            else gameObject.SetActive(false);
            return;
        }
        if (hasSlide && PlayTransition) slide.PlayIn();
        else if (hasSlide) slide.SetVisibleInstant(true);
        else gameObject.SetActive(true);
        // Chỉ các nút đã có chức năng được bấm; phần còn lại của bố cục giữ nguyên nhưng tắt.
        foreach (Selectable control in GetComponentsInChildren<Selectable>(true))
            control.interactable = control == playButton || control == questButton;
        DrawCharacter();
        DrawQuestAlert();
    }

    #endregion

    #region Selected Character

    // Scene inject trước ProgressionManager.Initialize; chỉ đọc tiến trình khi đã tới Home.
    private void DrawCharacter()
    {
        if (selection == null || game == null || game.State != GameState.Home || !isActiveAndEnabled) return;
        CharacterDataSO character = FindSelected();
        if (character == null) return;

        if (characterFrame != null)
        {
            preview ??= new CharacterPreviewSlot(characterFrame);
            preview.Show(character.UIVisualPrefab, character.Portrait);
        }
        if (characterName != null) characterName.text = character.DisplayName;

        UpgradeQuote level = upgrades.GetQuote(character.CharacterType, CharacterUpgradeKind.Level);
        bool maxed = level.ProgressRequired <= 0;
        if (levelText != null) levelText.text = level.Current.ToString(CultureInfo.InvariantCulture);
        if (levelSlider != null)
        {
            levelSlider.minValue = 0f;
            levelSlider.maxValue = maxed ? 1f : level.ProgressRequired;
            levelSlider.value = maxed ? 1f : level.Progress;
        }
        if (levelValueText != null)
            levelValueText.text = maxed
                ? maxLevelText
                : level.Progress.ToString(CultureInfo.InvariantCulture) + "/" + level.ProgressRequired.ToString(CultureInfo.InvariantCulture);
    }

    private CharacterDataSO FindSelected()
    {
        foreach (CharacterDataSO character in selection.PlayableCharacters)
            if (character.CharacterType == selection.SelectedCharacterType) return character;
        return null;
    }

    #endregion

    #region Home Actions

    private void OpenMissions()
    {
        if (!starting && missionScreen != null) missionScreen.Open();
    }

    // Giống DrawCharacter: chỉ đọc tiến độ khi đã tới Home.
    private void DrawQuestAlert()
    {
        if (questAlert == null || missions == null || game == null || game.State != GameState.Home) return;
        questAlert.SetActive(missions.HasClaimable);
    }

    private void Play()
    {
        if (starting || game == null || game.State != GameState.Home || transition == null || transition.IsTransitioning) return;
        starting = true; // Chặn click lặp trong lúc nút đang nhún.
        if (playTween != null) playTween.PlayOpen(CompletePlay);
        else CompletePlay();
    }

    // Callback bị hủy khi Home đóng; không để tween cũ tự khởi động một lượt khác.
    // Thanh bar không trượt lúc vào trận: nó tắt ngay cùng Home khi màn đã tối (xem HomeNavigationBar.Refresh).
    private void CompletePlay()
    {
        if (!starting) return;
        starting = false;
        if (isActiveAndEnabled && game != null && game.State == GameState.Home) transition.StartBattle();
    }

    #endregion
}
