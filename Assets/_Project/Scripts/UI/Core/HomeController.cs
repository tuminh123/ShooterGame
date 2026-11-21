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

    #endregion

    #region Dependencies And State

    private IGameState game;
    private IScreenTransition transition;
    private IPlayerSelection selection;
    private ICharacterUpgrades upgrades;
    private CharacterPreviewSlot preview;
    private bool starting;
    private bool subscribed;
    // Home bị ẩn vì mở màn khác trong menu (màn Character) chứ không phải vì vào trận: chỉ trường hợp này mới trượt.
    private bool hiddenByMenu;

    #endregion

    #region Lifecycle

    /// <summary>SceneContext inject các service cần cho màn Home.</summary>
    [Inject]
    public void Construct(IGameState gameState, IScreenTransition screenTransition,
        IPlayerSelection playerSelection, ICharacterUpgrades characterUpgrades)
    {
        Unsubscribe();
        game = gameState;
        transition = screenTransition;
        selection = playerSelection;
        upgrades = characterUpgrades;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        Subscribe();
    }

    private void OnDisable()
    {
        if (playButton != null) playButton.onClick.RemoveListener(Play);
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
        DrawCharacter();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        selection.SelectionChanged -= OnSelectionChanged;
        upgrades.Upgraded -= OnUpgraded;
    }

    private void OnSelectionChanged(CharacterType type) => DrawCharacter();
    private void OnUpgraded(CharacterType type, CharacterUpgradeKind kind) => DrawCharacter();

    #endregion

    #region Visibility

    // Chỉ trượt khi đổi qua lại với màn Character (UIManager gọi hai bên cùng lúc nên hai màn đẩy nhau).
    // Vào trận / từ trận về Home thì bật/tắt ngay như cũ: ScreenTransition đã làm màn tối dần.
    public override void SetVisible(bool visible)
    {
        bool hasSlide = TryGetComponent(out SlideTransition slide);
        if (!visible)
        {
            // Lúc này state đã đổi nếu là vào trận; còn Home nghĩa là vừa mở màn Character.
            hiddenByMenu = game != null && game.State == GameState.Home;
            if (hasSlide && hiddenByMenu) slide.PlayOut();
            else if (hasSlide) slide.SetVisibleInstant(false);
            else gameObject.SetActive(false);
            return;
        }
        if (hasSlide && hiddenByMenu) slide.PlayIn();
        else if (hasSlide) slide.SetVisibleInstant(true);
        else gameObject.SetActive(true);
        hiddenByMenu = false;
        // Chỉ các nút đã có chức năng được bấm; phần còn lại của bố cục giữ nguyên nhưng tắt.
        foreach (Selectable control in GetComponentsInChildren<Selectable>(true))
            control.interactable = control == playButton;
        DrawCharacter();
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
        if (isActiveAndEnabled && game != null && game.State == GameState.Home) transition.StartRun();
    }

    #endregion
}
