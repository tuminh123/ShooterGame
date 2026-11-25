using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Trình bày bảng chọn skill khi lên cấp: slow motion, pause, mở/đóng panel và chuyển thao tác click cho miền Battle.
/// Gắn trên root LevelUpUI là cha của LevelUpPanel, vẫn hoạt động khi panel con tắt.
/// </summary>
/// <remarks>
/// Đếm lượt chờ, roll lựa chọn, áp dụng skill và chặn skill lỗi thuộc <see cref="ISkillChoice"/> (miền Battle, Giai đoạn 6);
/// controller này chỉ quyết định <b>khi nào</b> và <b>cách</b> hiển thị.
/// </remarks>
public sealed class SkillChoiceController : UIPanelController, Zenject.ILateTickable
{
    #region Frame

    // LateTick do Zenject gọi: UIInstaller bind controller này làm ILateTickable với thứ tự sau mọi Manager,
    // nên bảng chọn skill luôn mở SAU khi BattleManager đã xử lý xong loot/lên cấp trong cùng frame.
    private int activationFrame = -1; // Bỏ qua frame vừa bật để không xử lý ngay trong frame panel được bật lại.

    #endregion

    #region Panel Reference

    [SerializeField] private SkillChoiceUI levelUpPanel; // View độc lập hiển thị ba lựa chọn và phát sự kiện click.
    [SerializeField, Range(0.05f, 1f)] private float levelUpTimeScale = 0.65f; // Chậm nhẹ trước khi pause mở popup.
    [SerializeField, Min(0f)] private float slowMotionDuration = 0.3f; // Giây thực, không bị kéo dài bởi timeScale.

    #endregion

    #region Presentation State And Dependencies

    private readonly List<SkillChoiceOffer> offers = new List<SkillChoiceOffer>(3); // Các lựa chọn đang hiển thị; roll lại ghi đè.
    private readonly List<SkillRuntime> ownedSkills = new List<SkillRuntime>(); // Chỉ skill gameplay; skill nhân vật thuộc CharacterSkills.
    private ISkillChoice choices; // Miền Battle: lượt chờ, roll, áp dụng.
    private IOwnedSkillReader skills; // Miền Combat: chỉ đọc để hiện các ô skill đang sở hữu.
    private IGamePause pauses; // Pause nhiều nguồn; đóng modal không gỡ pause thủ công.
    private IGameState game; // Rời Playing (thắng/thua/về Home) thì hủy bảng thưởng còn đang chờ.
    private IDisposable pauseLease; // Token pause của bảng lên cấp; trả khi đóng hoặc ẩn bảng.
    private IDisposable slowMotionLease;
    private float slowMotionUntil;
    private bool subscribed; // Chống đăng ký lặp event khi Construct và OnEnable cùng chạy.
    private bool refreshPending; // Hoãn roll/render đến LateTick sau khi callback gameplay hoàn tất.
    private bool selecting; // Chặn việc áp dụng lựa chọn lồng nhau trong callback.
    private bool rerollPending;
    private bool visible; // UIManager cấp quyền trình bày; controller không tự chuyển các UI khác.
    private int version; // Tăng khi lượt chờ bị hủy: kết quả callback cũ không làm mới bảng của phiên mới.

    /// <summary>Số lượt thưởng đang chờ (đọc từ miền Battle).</summary>
    public int PendingSkillChoices => choices != null ? choices.PendingChoices : 0;

    #endregion

    #region Initialization And Subscription

    // Nhận dependency từ Zenject, kiểm tra reference scene và khởi tạo subscription kể cả khi panel đang tắt.
    [Inject]
    public void Construct(ISkillChoice skillChoice, IOwnedSkillReader ownedSkills, IGamePause gamePause, IGameState gameState)
    {
        choices = skillChoice;
        skills = ownedSkills;
        pauses = gamePause;
        game = gameState;
        if (levelUpPanel == null)
            throw new InvalidOperationException("Assign LevelUpPanel in SkillChoiceController.");
        levelUpPanel.Hide();
        if (isActiveAndEnabled) Subscribe();
    }

    // Đăng ký lại khi Canvas được bật; chờ Construct nếu Zenject chưa inject.
    private void OnEnable()
    {
        activationFrame = Time.frameCount;
        if (choices != null) Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        choices.PendingChanged += OnPendingChanged;
        choices.ChoicesCanceled += OnChoicesCanceled;
        game.StateChanged += OnGameStateChanged;
        levelUpPanel.Selected += Choose;
        levelUpPanel.RerollRequested += Reroll;
        levelUpPanel.Hidden += OnPanelHidden;
    }

    #endregion

    #region Battle Events

    // Thắng/thua/về Home chấm dứt bảng thưởng của trận, kể cả khi Player chưa chết hoặc chưa bị thu hồi.
    private void OnGameStateChanged(GameState previous, GameState current)
    {
        if (current != GameState.Playing) choices.CancelChoices();
    }

    // Lượt lên cấp đầu tiên có một nhịp slow motion; các lượt đang xếp hàng không phát lại nhịp này.
    private void OnPendingChanged(int previous, int current)
    {
        if (current <= previous) return; // Giảm (chọn xong/pool cạn) đã được xử lý ở Choose/LateTick.
        refreshPending = true;
        if (previous == 0 && !visible && choices.CanChoose && slowMotionDuration > 0f && Time.timeScale > 0f)
        {
            slowMotionLease = pauses.AcquireSlowMotion(levelUpTimeScale);
            slowMotionUntil = Time.unscaledTime + slowMotionDuration;
        }
        else if (slowMotionLease == null) RequestVisibility(true);
    }

    // Reset lượt, đổi Player, Player chết hoặc rời Playing: đóng bảng và bỏ mọi kết quả đang dở.
    private void OnChoicesCanceled()
    {
        version++;
        ReleaseSlowMotion(); // Không để sót slow motion khi trận kết thúc giữa nhịp chậm.
        refreshPending = false;
        rerollPending = false;
        ownedSkills.Clear();
        if (levelUpPanel != null)
        {
            levelUpPanel.ShowOwnedSkills(ownedSkills);
            FinishPresentation();
        }
    }

    #endregion

    #region Presentation

    /// <summary>Roll từ runtime mới nhất; pool cạn vì lỗi thì đóng bảng, trả pause nhưng miền Battle giữ lượt để retry.</summary>
    public void LateTick()
    {
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return;
        if (!refreshPending) return;
        if (slowMotionLease != null)
        {
            if (Time.unscaledTime < slowMotionUntil) return;
            ReleaseSlowMotion(); // Trả tốc độ nền trước khi token pause của popup được tạo.
        }
        refreshPending = false;
        if (!choices.CanChoose) { choices.CancelChoices(); return; }
        choices.Roll(offers, rerollPending);
        rerollPending = false;
        if (offers.Count == 0)
        {
            FinishPresentation();
            return;
        }
        if (!visible) RequestVisibility(true);
        if (visible)
        {
            levelUpPanel.Show(offers, choices.PendingChoices);
            RefreshOwnedSlots();
        }
    }

    // Roll miễn phí, không tiêu lượt thưởng và không nhả pause của popup.
    private void Reroll()
    {
        if (!visible || selecting || refreshPending || offers.Count == 0 || !choices.CanChoose) return;
        rerollPending = true;
        refreshPending = true;
    }

    // Miền Battle chỉ tiêu lượt khi gameplay thành công; lỗi được Battle log và chặn skill đó khỏi các lần roll sau.
    private void Choose(int index)
    {
        if (selecting || refreshPending || index < 0 || index >= offers.Count) return;
        selecting = true;
        int revision = version; // Chụp phiên trước khi gameplay có thể phát callback hủy.
        SkillChoiceOffer offer = offers[index]; // Chụp offer vì callback có thể đóng panel và xóa list.
        try
        {
            if (choices.Choose(offer) && revision == version) RefreshOwnedSlots();
        }
        finally
        {
            selecting = false;
            if (revision == version) refreshPending = true;
        }
    }

    /// <summary>Retry sau khi sửa dữ liệu skill lỗi; không cấp thêm hoặc xóa lượt đang chờ.</summary>
    public void RetrySkillChoices()
    {
        choices.RetryFailedChoices();
        refreshPending = choices.PendingChoices > 0;
        if (refreshPending) RequestVisibility(true);
    }

    // Slot là phần trình bày phụ: lỗi prefab/pool phải được ghi log nhưng không chặn mở bảng,
    // không làm mất lượt và không biến một skill vừa nhận thành ứng viên lỗi.
    private void RefreshOwnedSlots()
    {
        try
        {
            skills.GetOwnedSkills(choices.ChoiceOwner, ownedSkills);
            levelUpPanel.ShowOwnedSkills(ownedSkills);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    #endregion

    #region Panel Lifecycle And Pause

    // Panel bị tắt từ ngoài: trả pause và bỏ offer cũ, không xóa lượt đang chờ.
    private void OnPanelHidden()
    {
        ReleasePause();
        offers.Clear();
        refreshPending = false;
        rerollPending = false;
        if (visible) RequestVisibility(false);
    }

    // Trả riêng token của bảng skill; gọi lặp vẫn an toàn và không ảnh hưởng nguồn pause khác.
    private void ReleasePause()
    {
        IDisposable previous = pauseLease;
        pauseLease = null;
        previous?.Dispose();
    }

    private void ReleaseSlowMotion()
    {
        IDisposable previous = slowMotionLease;
        slowMotionLease = null;
        previous?.Dispose();
    }

    // Ẩn view và dọn offer/pause; giữ nguyên lượt chờ để hỗ trợ ẩn UI hoặc phục hồi sau lỗi.
    private void ClosePanel()
    {
        levelUpPanel.Hide();
        offers.Clear();
        rerollPending = false;
        ReleasePause();
    }

    // Kết thúc yêu cầu modal để UIManager quay về panel trước hoặc gameplay; không tự chuyển UI khác.
    private void FinishPresentation()
    {
        RequestVisibility(false);
        ClosePanel();
    }

    /// <summary>UIManager cấp/thu quyền hiển thị; còn lượt chờ thì giữ pause và roll ở LateTick.</summary>
    public override void SetVisible(bool value)
    {
        visible = value;
        if (!value) ClosePanel();
        if (value && choices != null && choices.PendingChoices > 0)
        {
            refreshPending = true;
            if (pauseLease == null) pauseLease = pauses.Acquire();
        }
    }

    #endregion

    #region Cleanup

    // Gỡ toàn bộ subscription và token pause khi root controller hoặc Canvas ngừng hoạt động.
    private void OnDestroy() => OnDisable();

    // Canvas bị tắt giữa trận: hủy lượt chờ như trước đây (miền Battle bỏ qua nếu scene đang dọn).
    private void OnDisable()
    {
        if (subscribed)
        {
            choices.PendingChanged -= OnPendingChanged;
            choices.ChoicesCanceled -= OnChoicesCanceled;
            game.StateChanged -= OnGameStateChanged;
            levelUpPanel.Selected -= Choose;
            levelUpPanel.RerollRequested -= Reroll;
            levelUpPanel.Hidden -= OnPanelHidden;
            subscribed = false;
        }
        choices?.CancelChoices();
        OnChoicesCanceled();
    }

    #endregion
}
