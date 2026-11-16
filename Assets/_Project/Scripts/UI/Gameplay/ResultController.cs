using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

// Chung cho thắng/thua; UIManager chọn lúc hiện, tween hiện có quản lý hiệu ứng scale.
public sealed class ResultController : UIPanelController
{

    #region Supporting Types
    [Serializable]
    private sealed class ResultPanel
    {
        public GameObject panel;
        public StaggeredPopupTween popupTween;
        public Button continueButton;
        public TMP_Text goldText;
        public TMP_Text diamondText;

        public void Validate()
        {
            if (panel == null || popupTween == null || continueButton == null || goldText == null || diamondText == null)
                throw new InvalidOperationException("Assign result panel, popup tween, Continue button and currency texts.");
        }

        public void Hide()
        {
            continueButton.interactable = false;
            popupTween.StopAndReset();
            panel.SetActive(false);
        }
    }

    #endregion

    #region Dependencies And State

    [SerializeField] private ResultPanel victory = new ResultPanel();
    [SerializeField] private ResultPanel defeat = new ResultPanel();
    private IGameStateReader gameState;
    private IRunCommands runCommands;
    private IRunStatistics session;
    private IStageResults stage;
    private ResultPanel activePanel;
    private int presentationVersion;

    #endregion

    #region Initialization

    [Inject]
    public void Construct(
        IGameFlow gameFlow,
        IRunSession runSession,
        IStageService stageService)
    {
        this.gameState = gameFlow.GetGameStateReader();
        this.runCommands = gameFlow.GetRunCommands();
        this.session = runSession.GetRunStatistics();
        this.stage = stageService.GetStageResults();
        victory.Validate();
        defeat.Validate();
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        victory.continueButton.onClick.AddListener(RestartRun);
        defeat.continueButton.onClick.AddListener(RestartRun);
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
        next.goldText.text = checked((long)session.Gold + stage.GetResultReward(CurrencyType.Gold)).ToString();
        next.diamondText.text = stage.GetResultReward(CurrencyType.Diamond).ToString();
        next.panel.transform.SetAsLastSibling();
        next.panel.SetActive(true);
        // StaggeredPopupTween/PopupTweenBase đã có scale vượt đỉnh, trả scale gốc và unscaled time.
        next.popupTween.PlayOpen(() =>
        {
            if (activePanel == next && version == presentationVersion)
                next.continueButton.interactable = true;
        });
    }

    private void RestartRun()
    {
        ResultPanel panel = activePanel;
        if (panel == null || !panel.continueButton.interactable || gameState == null ||
            (gameState.State != GameState.Won && gameState.State != GameState.Lost)) return;
        panel.continueButton.interactable = false;
        // StartRun tự dọn/reset trận, spawn Player và đưa UI về Playing.
        if (!runCommands.StartRun() && activePanel == panel &&
            (gameState.State == GameState.Won || gameState.State == GameState.Lost))
            panel.continueButton.interactable = true;
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        victory.continueButton.onClick.RemoveListener(RestartRun);
        defeat.continueButton.onClick.RemoveListener(RestartRun);
        SetVisible(false);
    }

    #endregion
}
