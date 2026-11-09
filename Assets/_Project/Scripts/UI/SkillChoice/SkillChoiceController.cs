using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Điều khiển riêng luồng chọn skill; gắn trên root LevelUpUI là cha của LevelUpPanel, vẫn hoạt động khi panel con tắt.
public sealed class SkillChoiceController : UIPanelController
{
    #region Panel Reference

    [SerializeField] private SkillChoiceUI levelUpPanel; // View độc lập hiển thị ba lựa chọn và phát sự kiện click.

    #endregion

    #region Choice State And Dependencies

    private readonly SkillChoicePool pool = new SkillChoicePool(); // Áp dụng luật roll cấp kế tiếp, loại skill max và skill đang bị chặn.
    private readonly SkillChoiceProgress progress = new SkillChoiceProgress(); // Số lượt thưởng chưa dùng; chỉ tiêu sau khi nhận/nâng thành công.
    private readonly List<SkillChoiceOffer> offers = new List<SkillChoiceOffer>(3); // Snapshot các lựa chọn đang hiển thị; không giữ qua lần roll mới.
    private readonly List<SkillRuntime> ownedSkills = new List<SkillRuntime>(); // Snapshot innate + run để hiển thị slot đúng cấp thực tế.
    private readonly HashSet<SkillDataSO> failedSkills = new HashSet<SkillDataSO>(); // Skill lỗi bị chặn tới khi reset trận, đổi Player hoặc gọi retry.
    private IReadOnlyList<SkillDataSO> catalog; // Snapshot skill được phép thưởng từ database được Zenject inject.
    private LevelManager levels; // Nguồn sự kiện tăng cấp và reset tiến trình trận.
    private SkillManager skills; // Đầu mối nhận/nâng runtime; UI không trực tiếp sửa behavior.
    private PlayerManager players; // Nguồn Player hiện tại và thông báo đổi nhân vật.
    private GameplayPauseService pauses; // Dịch vụ pause nhiều nguồn; đóng modal không gỡ pause thủ công.
    private CharacterView owner; // Player nhận thưởng; đổi owner phải bỏ queue của owner cũ.
    private IDisposable pauseLease; // Token pause của bảng lên cấp; trả khi đóng hoặc ẩn bảng.
    private bool subscribed; // Chống đăng ký lặp event khi Construct và OnEnable cùng chạy.
    private bool refreshPending; // Hoãn roll/render đến LateUpdate sau khi callback gameplay hoàn tất.
    private bool selecting; // Chặn việc áp dụng lựa chọn lồng nhau trong callback.
    private bool visible; // UIManager cấp quyền trình bày; controller không tự chuyển các UI khác.
    private int version; // Phiên queue; reset/đổi Player vô hiệu hóa kết quả callback cũ.
    public int PendingSkillChoices => progress.Pending; // Cho bên ngoài đọc số lượt thưởng đang chờ, không cho sửa trực tiếp.

    #endregion

    #region Initialization And Subscription

    // Nhận dependency từ Zenject, kiểm tra reference scene và khởi tạo subscription kể cả khi panel đang tắt.
    [Inject]
    public void Construct(LevelManager levels, SkillManager skills, PlayerManager players,
        GameplayPauseService pauses, SkillDatabaseSO database)
    {
        this.levels = levels; this.skills = skills; this.players = players; this.pauses = pauses;
        catalog = database.CreateRunChoiceCatalog();
        if (levelUpPanel == null)
            throw new InvalidOperationException("Assign LevelUpPanel in SkillChoiceController.");
        levelUpPanel.Hide();
        if (isActiveAndEnabled) Subscribe();
    }

    // Đăng ký lại khi Canvas được bật; chờ Construct nếu Zenject chưa inject.
    private void OnEnable() { if (levels != null) Subscribe(); }

    // Theo dõi level, Player và thao tác panel đúng một lần; đồng bộ owner ban đầu.
    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        levels.RunLevelChanged += OnRunLevelChanged;
        GameUIEventBus.StateChanged += OnGameStateChanged;
        levels.RunReset += CancelChoices;
        players.PlayerChanged += OnPlayerChanged;
        levelUpPanel.Selected += Choose;
        levelUpPanel.Hidden += OnPanelHidden;
        OnPlayerChanged(players.CurrentPlayer);
    }

    #endregion

    #region Player Lifecycle

    // Hủy lượt thưởng của Player cũ rồi gắn sự kiện chết/release của Player mới.
    private void OnPlayerChanged(CharacterView character)
    {
        DetachOwner(); CancelChoices(); owner = character;
        if (owner != null) { owner.Died += OnOwnerEnded; owner.Released += OnOwnerEnded; }
    }

    // Gỡ listener khỏi owner cũ trước khi thay reference hoặc dọn manager.
    private void DetachOwner()
    {
        if (owner != null) { owner.Died -= OnOwnerEnded; owner.Released -= OnOwnerEnded; }
    }

    // Player chết hoặc bị trả pool: hủy phần thưởng và đóng bảng để không áp dụng cho lượt spawn khác.
    private void OnOwnerEnded(CharacterView character) => CancelChoices();

    // Thắng/thua/về Home chấm dứt bảng thưởng của trận, kể cả khi Player chưa chết hoặc chưa bị thu hồi.
    private void OnGameStateChanged(GameState previous, GameState current)
    {
        if (current != GameState.Playing) CancelChoices();
    }

    #endregion

    #region Reward Queue And Presentation

    // Cộng đủ số lượt khi lên nhiều cấp và pause ngay; phần render chờ LateUpdate.
    private void OnRunLevelChanged(int previous, int current)
    {
        progress.AddLevels(previous, current);
        refreshPending = true;
        if (progress.Pending > 0) RequestVisibility(true);
    }

    // Roll từ runtime mới nhất; pool cạn vì lỗi thì đóng bảng, trả pause nhưng giữ lượt để retry.
    private void LateUpdate()
    {
        if (!refreshPending) return;
        refreshPending = false;
        if (progress.Pending == 0 || owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive)
        { CancelChoices(); return; }
        pool.Build(catalog, skills, owner, offers, excluded: failedSkills);
        if (offers.Count == 0)
        {
            // Pool cạn vì skill lỗi: giữ queue để retry sau khi sửa cấu hình.
            if (failedSkills.Count == 0) progress.Clear();
            FinishPresentation();
            return;
        }
        RequestVisibility(true);
        if (visible)
        {
            levelUpPanel.Show(offers, progress.Pending);
            RefreshOwnedSlots();
        }
    }

    #endregion

    #region Selection And Error Recovery

    // Chỉ tiêu một lượt sau khi gameplay thành công; exception giữ lượt và chặn ứng viên lỗi khỏi các lần roll sau.
    private void Choose(int index)
    {
        if (selecting || refreshPending || index < 0 || index >= offers.Count) return;
        selecting = true;
        int revision = version; // Chụp phiên queue trước khi gameplay có thể phát callback reset.
        SkillChoiceOffer offer = offers[index]; // Chụp offer vì callback có thể đóng panel và xóa list.
        try
        {
            if (offer.TryApply(skills, owner) && revision == version)
            {
                progress.Consume();
                RefreshOwnedSlots();
            }
        }
        catch (Exception exception)
        {
            if (revision == version) failedSkills.Add(offer.Definition);
            Debug.LogException(exception, this);
        }
        finally
        {
            selecting = false;
            if (revision == version) refreshPending = true;
        }
    }

    // Retry sau khi sửa dữ liệu; không cấp thêm hoặc xóa lượt đang chờ.
    public void RetrySkillChoices()
    {
        failedSkills.Clear();
        refreshPending = progress.Pending > 0;
        if (refreshPending) RequestVisibility(true);
    }

    #endregion

    #region Panel Lifecycle And Pause

    // Slot là phần trình bày phụ: lỗi prefab/pool phải được ghi log nhưng không chặn mở bảng,
    // không làm mất lượt và không biến một skill vừa nhận thành ứng viên lỗi.
    private void RefreshOwnedSlots()
    {
        try
        {
            skills.GetOwnedSkills(owner, ownedSkills);
            levelUpPanel.ShowOwnedSkills(ownedSkills);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    // Xử lý panel bị tắt từ ngoài: trả pause và bỏ offer cũ, không tự xóa lượt đang chờ.
    private void OnPanelHidden()
    {
        ReleasePause(); offers.Clear(); refreshPending = false;
        if (visible) RequestVisibility(false);
    }

    // Trả riêng token của bảng skill; gọi lặp vẫn an toàn và không ảnh hưởng nguồn pause khác.
    private void ReleasePause()
    {
        IDisposable previous = pauseLease; pauseLease = null; previous?.Dispose();
    }

    // Ẩn view và dọn offer/pause; giữ nguyên queue để hỗ trợ ẩn UI hoặc phục hồi sau lỗi.
    private void ClosePanel()
    {
        levelUpPanel.Hide(); offers.Clear(); ReleasePause();
    }

    // Kết thúc yêu cầu modal để UIManager quay về panel trước hoặc gameplay; không tự chuyển UI khác.
    private void FinishPresentation()
    {
        RequestVisibility(false);
        ClosePanel();
    }

    // Hủy queue khi reset, đổi Player hoặc tắt manager; tăng phiên để callback cũ không tiêu lượt mới.
    private void CancelChoices()
    {
        version++;
        progress.Clear(); failedSkills.Clear(); refreshPending = false;
        ownedSkills.Clear();
        if (levelUpPanel != null) levelUpPanel.ShowOwnedSkills(ownedSkills);
        if (levelUpPanel != null) FinishPresentation();
    }

    #endregion

    #region Visibility Coordination

    // Nhận yêu cầu hiển thị chung; controller tự giữ queue, trả pause khi ẩn và mở lại ở LateUpdate.
    public override void SetVisible(bool value)
    {
        visible = value;
        if (!value) ClosePanel();
        if (value && progress.Pending > 0)
        {
            refreshPending = true;
            if (pauseLease == null) pauseLease = pauses.Acquire();
        }
    }

    #endregion

    #region Cleanup

    // Gỡ toàn bộ subscription, reference owner và token pause khi root controller hoặc Canvas ngừng hoạt động.
    private void OnDisable()
    {
        if (subscribed)
        {
            levels.RunLevelChanged -= OnRunLevelChanged;
            GameUIEventBus.StateChanged -= OnGameStateChanged;
            levels.RunReset -= CancelChoices;
            players.PlayerChanged -= OnPlayerChanged;
            levelUpPanel.Selected -= Choose;
            levelUpPanel.Hidden -= OnPanelHidden;
            subscribed = false;
        }
        DetachOwner(); owner = null; CancelChoices();
    }
    #endregion
}
