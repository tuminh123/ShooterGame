using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Chung cho thắng/thua; UIManager chọn lúc hiện, tween hiện có quản lý hiệu ứng scale.
/// Popup Victory có Claim và Claim x2, nhận thưởng xong tự về Home; popup Defeat có thêm nút Home bên cạnh nút chơi lại.
/// </summary>
public sealed class ResultController : UIPanelController
{

    #region Supporting Types
    [Serializable]
    private sealed class ResultPanel
    {
        /// <summary>Gốc của popup kết quả.</summary>
        public GameObject panel;
        /// <summary>Tween mở lần lượt các phần tử của popup.</summary>
        public StaggeredPopupTween popupTween;
        /// <summary>Nút hành động của popup (Victory: Claim, Defeat: chơi lại); khóa cho tới khi tween mở xong.</summary>
        [FormerlySerializedAs("continueButton")] public Button actionButton;
        [Tooltip("Nút thứ hai. Victory: Claim x2, Defeat: về Home.")]
        [FormerlySerializedAs("homeButton")] public Button secondButton;
        [Tooltip("Các phần tử nảy ra trước ô thưởng, theo thứ tự.")]
        public Transform[] headItems;
        [Tooltip("Lưới chứa ô thưởng; ô được sinh theo thưởng thật của Stage.")]
        public RectTransform slotRoot;

        private readonly List<RewardSlotView> slots = new List<RewardSlotView>();

        /// <summary>Ném lỗi sớm nếu Inspector thiếu reference.</summary>
        public void Validate()
        {
            if (panel == null || popupTween == null || actionButton == null || secondButton == null || headItems == null || slotRoot == null)
                throw new InvalidOperationException("Assign result panel, popup tween, both buttons, head items and slot root.");
        }

        /// <summary>Ẩn ngay popup: khóa nút, dừng tween và tắt panel.</summary>
        public void Hide()
        {
            SetInteractable(false);
            popupTween.StopAndReset();
            panel.SetActive(false);
        }

        /// <summary>Vẽ ô thưởng và đặt lại thứ tự nảy: phần đầu → từng ô → hai nút (số ô đổi theo Stage).</summary>
        public void BindRewards(RewardSlotView prefab, IReadOnlyList<RewardSlotModel> models)
        {
            RewardSlotView.BindAll(slots, prefab, slotRoot, models);
            var targets = new List<Transform>(headItems);
            for (int i = 0; i < models.Count; i++) targets.Add(slots[i].transform);
            targets.Add(actionButton.transform);
            targets.Add(secondButton.transform);
            popupTween.Initialize(targets.ToArray());
        }

        /// <summary>Khóa/mở mọi nút cùng lúc, để không chạy hai lệnh từ hai nút.</summary>
        public void SetInteractable(bool value)
        {
            actionButton.interactable = value;
            secondButton.interactable = value;
        }
    }

    #endregion

    #region Dependencies And State

    [SerializeField] private ResultPanel victory = new ResultPanel();
    [SerializeField] private ResultPanel defeat = new ResultPanel();

    [Header("Phần thưởng")]
    [Tooltip("Màn đen nhận thưởng, dùng chung với màn Mission.")]
    [SerializeField] private RewardRevealView rewardReveal;
    [Tooltip("Nguồn icon tiền, dùng chung với màn Equipment.")]
    [SerializeField] private EquipmentThemeSO theme;
    [SerializeField] private RewardSlotView slotPrefab;
    [SerializeField] private string amountFormat = "x{0}";
    [SerializeField] private string levelFormat = "Lv.{0}";
    [SerializeField] private Color rewardFrame = new Color(0.45f, 0.48f, 0.56f, 1f);

    private IGameState gameState;
    private IGameFlow gameFlow;
    private IBattleProgress battle;
    private IStageResults stage;
    private IWallet wallet;
    private IScreenTransition transition;
    private RewardSlotFactory slotFactory;
    private ResultPanel activePanel;
    private bool claiming; // Đã bấm Claim: đang diễn nhận thưởng rồi về Home.
    private int presentationVersion;

    #endregion

    #region Initialization

    /// <summary>SceneContext inject trạng thái, luồng game, dữ liệu trận và kết quả Stage.</summary>
    [Inject]
    public void Construct(
        IGameState gameState,
        IGameFlow gameFlow,
        IBattleProgress battleProgress,
        IStageResults stageResults,
        IWallet walletService,
        IScreenTransition screenTransition)
    {
        this.gameState = gameState;
        this.gameFlow = gameFlow;
        this.battle = battleProgress;
        this.stage = stageResults;
        wallet = walletService;
        transition = screenTransition;
        victory.Validate();
        defeat.Validate();
        if (rewardReveal == null || theme == null || slotPrefab == null)
            throw new InvalidOperationException("Assign reward reveal, theme and slot prefab on ResultController.");
        slotFactory = new RewardSlotFactory(theme, amountFormat, levelFormat, rewardFrame);
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        victory.actionButton.onClick.AddListener(Claim);
        victory.secondButton.onClick.AddListener(ClaimDouble);
        defeat.actionButton.onClick.AddListener(PlayAgain);
        defeat.secondButton.onClick.AddListener(ReturnHome);
    }

    #endregion

    #region Runtime Operations

    public override void SetVisible(bool visible)
    {
        ResultPanel next = null;
        if (visible && gameState != null)
        {
            switch (gameState.State)
            {
                case GameState.Won: next = victory; break;
                case GameState.Lost: next = defeat; break;
            }
        }
        if (next != null && activePanel == next && next.panel.activeSelf) return;

        int version = ++presentationVersion;
        activePanel = null;
        if (claiming) rewardReveal.Hide();
        claiming = false;
        victory.Hide();
        defeat.Hide();
        if (next == null) return;

        activePanel = next;
        next.BindRewards(slotPrefab, slotFactory.Result(TotalRewards()));
        next.panel.transform.SetAsLastSibling();
        next.panel.SetActive(true);
        // StaggeredPopupTween/PopupTweenBase đã có scale vượt đỉnh, trả scale gốc và unscaled time.
        next.popupTween.PlayOpen(() =>
        {
            if (activePanel == next && version == presentationVersion)
                next.SetInteractable(true);
        });
    }

    // Thua: kết thúc lượt và về Home (ReturnHome tự dọn trận, UIManager mở Home theo state).
    private void ReturnHome() => ExecuteResultCommand(defeat, gameFlow.ReturnHome);

    // Thua: vào ngay trận mới (StartBattle tự dọn/reset trận, spawn Player và đưa UI về Playing).
    private void PlayAgain() => ExecuteResultCommand(defeat, gameFlow.StartBattle);

    // Đúng những gì đã nhận ở lượt này: thưởng Stage trao thực tế cộng vàng nhặt trong trận.
    private RewardResult TotalRewards()
    {
        var total = new RewardResult();
        if (battle.Gold > 0) total.Add(new CurrencyCost(CurrencyType.Gold, battle.Gold));
        if (stage.ResultRewards != null) total.Include(stage.ResultRewards);
        return total;
    }

    // Chỉ nhận click của đúng popup đang hiện; khóa nút trước khi chạy lệnh để không nhận click lặp.
    // Lệnh thất bại (đang chuyển state) thì mở lại nút để người chơi bấm lại.
    private void ExecuteResultCommand(ResultPanel panel, Func<bool> command)
    {
        if (panel == null || activePanel != panel || !panel.actionButton.interactable || gameState == null ||
            (gameState.State != GameState.Won && gameState.State != GameState.Lost)) return;
        panel.SetInteractable(false);
        if (!command() && activePanel == panel &&
            (gameState.State == GameState.Won || gameState.State == GameState.Lost))
            panel.SetInteractable(true);
    }

    #endregion

    #region Victory Claim

    // Thưởng đã trao và lưu từ lúc thắng (tắt app không mất) nên Claim chỉ trình bày.
    private void Claim() => ExecuteClaim(false);

    // Sau này gắn quảng cáo tại đây: chiếu quảng cáo trước, xem xong mới gọi ExecuteClaim(true).
    private void ClaimDouble() => ExecuteClaim(true);

    private void ExecuteClaim(bool doubled)
    {
        if (claiming || activePanel != victory || !victory.actionButton.interactable || gameState.State != GameState.Won) return;
        claiming = true;
        victory.SetInteractable(false);

        RewardResult total = TotalRewards();
        if (doubled)
        {
            // Trao lỗi thì vẫn cho nhận phần gốc rồi về Home, không kẹt ở popup.
            try { if (stage.TryDoubleRewards(battle.Gold, out RewardResult bonus)) total.Include(bonus); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        List<RewardSlotModel> slots = slotFactory.Result(total);
        if (slots.Count == 0)
        {
            FinishClaim();
            return;
        }

        victory.Hide(); // Màn đen hơi trong: không để popup và nút lộ phía sau.
        rewardReveal.Show(slots, BalanceBefore(slots, RewardFlyTarget.Gold, CurrencyType.Gold),
            BalanceBefore(slots, RewardFlyTarget.Diamond, CurrencyType.Diamond), FinishClaim);
    }

    // Tiền đã vào ví; màn nhận thưởng đếm từ số cũ.
    private long BalanceBefore(List<RewardSlotModel> slots, RewardFlyTarget target, CurrencyType type)
    {
        long balance = wallet.GetBalance(type);
        foreach (RewardSlotModel slot in slots)
            if (slot.Target == target) balance -= slot.Amount;
        return balance;
    }

    // Che màn rồi về Home; lớp che đang bận thì về thẳng để không kẹt ở màn trống.
    private void FinishClaim()
    {
        if (!transition.ReturnHome()) gameFlow.ReturnHome();
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        victory.actionButton.onClick.RemoveListener(Claim);
        victory.secondButton.onClick.RemoveListener(ClaimDouble);
        defeat.actionButton.onClick.RemoveListener(PlayAgain);
        defeat.secondButton.onClick.RemoveListener(ReturnHome);
        SetVisible(false);
    }

    #endregion
}
