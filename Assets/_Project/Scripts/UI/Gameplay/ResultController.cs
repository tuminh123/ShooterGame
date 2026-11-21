using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Chung cho thắng/thua; UIManager chọn lúc hiện, tween hiện có quản lý hiệu ứng scale.
/// Nút của popup Victory đưa người chơi về Home; nút của popup Defeat bắt đầu lại trận mới.
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
        /// <summary>Nút hành động của popup (Victory: về Home, Defeat: chơi lại); khóa cho tới khi tween mở xong.</summary>
        [FormerlySerializedAs("continueButton")] public Button actionButton;
        /// <summary>Text hiển thị vàng nhận được.</summary>
        public TMP_Text goldText;
        /// <summary>Text hiển thị kim cương nhận được.</summary>
        public TMP_Text diamondText;

        /// <summary>Ném lỗi sớm nếu Inspector thiếu reference.</summary>
        public void Validate()
        {
            if (panel == null || popupTween == null || actionButton == null || goldText == null || diamondText == null)
                throw new InvalidOperationException("Assign result panel, popup tween, action button and currency texts.");
        }

        /// <summary>Ẩn ngay popup: khóa nút, dừng tween và tắt panel.</summary>
        public void Hide()
        {
            actionButton.interactable = false;
            popupTween.StopAndReset();
            panel.SetActive(false);
        }
    }

    #endregion

    #region Dependencies And State

    [SerializeField] private ResultPanel victory = new ResultPanel();
    [SerializeField] private ResultPanel defeat = new ResultPanel();
    private IGameState gameState;
    private IGameFlow runCommands;
    private IRunReader run;
    private IStageResults stage;
    private ResultPanel activePanel;
    private int presentationVersion;

    #endregion

    #region Initialization

    /// <summary>SceneContext inject trạng thái, luồng game, dữ liệu trận và kết quả Stage.</summary>
    [Inject]
    public void Construct(
        IGameState gameState,
        IGameFlow gameFlow,
        IRunReader runReader,
        IStageResults stageResults)
    {
        this.gameState = gameState;
        this.runCommands = gameFlow;
        this.run = runReader;
        this.stage = stageResults;
        victory.Validate();
        defeat.Validate();
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        victory.actionButton.onClick.AddListener(ReturnHome);
        defeat.actionButton.onClick.AddListener(PlayAgain);
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
        victory.Hide();
        defeat.Hide();
        if (next == null) return;

        activePanel = next;
        // Hiển thị tổng nhận của lượt; vàng nhặt và thưởng Stage đều đã được cộng vào ví riêng.
        next.goldText.text = checked((long)run.Gold + stage.GetResultReward(CurrencyType.Gold)).ToString();
        next.diamondText.text = stage.GetResultReward(CurrencyType.Diamond).ToString();
        next.panel.transform.SetAsLastSibling();
        next.panel.SetActive(true);
        // StaggeredPopupTween/PopupTweenBase đã có scale vượt đỉnh, trả scale gốc và unscaled time.
        next.popupTween.PlayOpen(() =>
        {
            if (activePanel == next && version == presentationVersion)
                next.actionButton.interactable = true;
        });
    }

    // Thắng: kết thúc lượt và về Home (ReturnHome tự dọn trận, UIManager mở Home theo state).
    private void ReturnHome() => RunResultCommand(victory, runCommands.ReturnHome);

    // Thua: vào ngay trận mới (StartRun tự dọn/reset trận, spawn Player và đưa UI về Playing).
    private void PlayAgain() => RunResultCommand(defeat, runCommands.StartRun);

    // Chỉ nhận click của đúng popup đang hiện; khóa nút trước khi chạy lệnh để không nhận click lặp.
    // Lệnh thất bại (đang chuyển state) thì mở lại nút để người chơi bấm lại.
    private void RunResultCommand(ResultPanel panel, Func<bool> command)
    {
        if (panel == null || activePanel != panel || !panel.actionButton.interactable || gameState == null ||
            (gameState.State != GameState.Won && gameState.State != GameState.Lost)) return;
        panel.actionButton.interactable = false;
        if (!command() && activePanel == panel &&
            (gameState.State == GameState.Won || gameState.State == GameState.Lost))
            panel.actionButton.interactable = true;
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        victory.actionButton.onClick.RemoveListener(ReturnHome);
        defeat.actionButton.onClick.RemoveListener(PlayAgain);
        SetVisible(false);
    }

    #endregion
}
