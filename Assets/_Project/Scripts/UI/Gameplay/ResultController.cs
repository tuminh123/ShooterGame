using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

// Chung cho thắng/thua; UIManager chọn lúc hiện, tween hiện có quản lý hiệu ứng scale.
public sealed class ResultController : UIPanelController
{
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

    [SerializeField] private ResultPanel victory = new ResultPanel();
    [SerializeField] private ResultPanel defeat = new ResultPanel();
    private GameManager game;
    private RunSessionManager session;
    private StageManager stage;
    private ResultPanel activePanel;
    private int presentationVersion;

    [Inject]
    public void Construct(GameManager game, RunSessionManager session, StageManager stage)
    {
        this.game = game;
        this.session = session;
        this.stage = stage;
        victory.Validate();
        defeat.Validate();
    }

    private void OnEnable()
    {
        victory.continueButton.onClick.AddListener(RestartRun);
        defeat.continueButton.onClick.AddListener(RestartRun);
    }

    public override void SetVisible(bool visible)
    {
        ResultPanel next = null;
        if (visible && game != null)
        {
            switch (game.State)
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
        if (panel == null || !panel.continueButton.interactable || game == null ||
            (game.State != GameState.Won && game.State != GameState.Lost)) return;
        panel.continueButton.interactable = false;
        // StartRun tự dọn/reset trận, spawn Player và đưa UI về Playing.
        if (!game.StartRun() && activePanel == panel &&
            (game.State == GameState.Won || game.State == GameState.Lost))
            panel.continueButton.interactable = true;
    }

    private void OnDisable()
    {
        victory.continueButton.onClick.RemoveListener(RestartRun);
        defeat.continueButton.onClick.RemoveListener(RestartRun);
        SetVisible(false);
    }
}
