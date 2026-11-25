using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Controller màn Mission: giữ tab đang xem, gọi <see cref="IMissions"/>, dựng model rồi đẩy xuống View.
/// </summary>
/// <remarks>
/// Mở từ nút Quest ở Home; UIManager đặt màn này đè lên Home và nút Back quay lại. Không phải tab của thanh bar dưới nên
/// hai thanh được ẩn khi màn mở (HomeNavigationBar). Vẽ lại theo event, nhưng hoãn khi màn nhận thưởng đang diễn để thẻ không nhảy chỗ phía sau.
/// </remarks>
public sealed class MissionScreenController : UIPanelController
{
    #region References

    [SerializeField] private MissionListView list;
    [SerializeField] private RewardRevealView rewardReveal;
    [SerializeField] private Button backButton;
    [Tooltip("Icon tiền và màu độ hiếm, dùng chung với màn Equipment.")]
    [SerializeField] private EquipmentThemeSO theme;

    [Header("Text")]
    [SerializeField] private string claimLabel = "Claim";
    [SerializeField] private string progressLabel = "In Progress";
    [SerializeField] private string claimedLabel = "Completed";
    [Tooltip("{0} = tên nhóm.")]
    [SerializeField] private string titleFormat = "{0} Quest";
    [SerializeField] private string amountFormat = "x{0}";
    [SerializeField] private string levelFormat = "Lv.{0}";
    [SerializeField] private Color plainFrame = new Color(0.45f, 0.48f, 0.56f, 1f);

    [Header("Thanh tiến độ")]
    [Tooltip("Chờ màn trượt vào xong rồi thanh mới chạy.")]
    [SerializeField, Min(0f)] private float barDelay = 0.3f;
    [SerializeField, Min(0f)] private float barStagger = 0.08f;

    #endregion

    #region Dependencies And State

    private IMissions missions;
    private IGameState game;
    private IWallet wallet;
    private bool subscribed;
    private bool themeValid;
    private string selectedId; // ID database của tab đang xem; giữ theo ID vì event mở/đóng làm đổi vị trí tab.
    private float nextCheck;
    private bool presenting; // Màn nhận thưởng hoặc thẻ rời chỗ đang diễn: hoãn vẽ lại, chặn đổi tab và nhận tiếp.
    // Mức tiến độ người chơi đã nhìn thấy, để thanh chạy phần tăng thêm. Chỉ ở RAM: mở game lại thì chạy từ 0.
    private readonly Dictionary<string, int> seen = new Dictionary<string, int>();
    private int runningBars;
    private RewardSlotFactory slots;
    private readonly List<MissionBoard> openBoards = new List<MissionBoard>();

    #endregion

    #region Navigation State (HomeNavigationBar)

    /// <summary>Màn đang mở theo yêu cầu điều hướng.</summary>
    public bool IsShown => WantsVisible;

    /// <summary>Phát khi màn mở/đóng.</summary>
    public event Action NavigationChanged;

    /// <summary>Phát sau khi màn đã đóng, khi người chơi bấm Go và cần mở màn đích. Battle nghĩa là chỉ về Home.</summary>
    public event Action<MissionDestination> GoRequested;

    #endregion

    #region Lifecycle

    /// <summary>Zenject inject cả object đang tắt; đăng ký event khi object được bật.</summary>
    [Inject]
    public void Construct(IMissions missionService, IGameState gameState, IWallet walletService)
    {
        Unsubscribe();
        missions = missionService;
        game = gameState;
        wallet = walletService;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (subscribed || missions == null) return;
        subscribed = true;
        missions.Changed += Redraw;
        list.TabClicked += OnTab;
        list.ClaimClicked += OnClaim;
        list.GoClicked += OnGo;
        backButton.onClick.AddListener(Close);
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        missions.Changed -= Redraw;
        list.TabClicked -= OnTab;
        list.ClaimClicked -= OnClaim;
        list.GoClicked -= OnGo;
        backButton.onClick.RemoveListener(Close);
    }

    // Home pause (timeScale = 0) nên dùng unscaled time; mỗi giây kiểm tra sang ngày/tuần mới một lần.
    private void Update()
    {
        if (!WantsVisible || missions == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        missions.Refresh(); // Qua kỳ mới hoặc event mở/đóng thì Changed sẽ vẽ lại.
    }

    #endregion

    #region Open And Visibility

    /// <summary>Mở màn Mission. Chỉ mở được khi game đang ở Home.</summary>
    public void Open()
    {
        if (game == null || game.State != GameState.Home || !EnsureThemeValid()) return;
        RequestVisibility(true);
    }

    /// <summary>Đóng màn về Home.</summary>
    public void Close() => RequestVisibility(false);

    /// <inheritdoc/>
    public override void SetVisible(bool visible)
    {
        NavigationChanged?.Invoke();
        presenting = false;
        if (!visible) rewardReveal.Hide();
        ApplyScreenVisibility(visible);
        if (!visible) return;
        missions.Refresh();
        Redraw();
        list.ScrollToTop();
    }

    private bool EnsureThemeValid()
    {
        if (themeValid) return true;
        try
        {
            if (theme == null) throw new InvalidOperationException("Assign Equipment Theme on MissionScreenController.");
            theme.Validate();
            themeValid = true;
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
        return themeValid;
    }

    #endregion

    #region View Events

    private void OnTab(int index)
    {
        if (presenting || index < 0 || index >= openBoards.Count || openBoards[index].Database.Id == selectedId) return;
        selectedId = openBoards[index].Database.Id;
        Redraw();
        list.ScrollToTop();
    }

    // Đóng màn Mission rồi nhờ bên điều hướng (HomeNavigationBar) mở màn đích; đóng trước để Back không quay lại Mission.
    private void OnGo(MissionDestination destination)
    {
        if (presenting || destination == MissionDestination.None) return;
        Close();
        GoRequested?.Invoke(destination);
    }

    private void OnClaim(string missionId)
    {
        MissionRuntime mission = FindMission(missionId);
        if (presenting || mission == null) return;
        presenting = true; // Đặt trước khi nhận: Changed phát ngay trong TryClaim và không được vẽ lại lúc này.
        RewardResult result = null;
        bool saved = false, claimed = false;
        try { claimed = missions.TryClaim(mission, out result, out saved); }
        finally
        {
            if (!claimed) presenting = false;
        }
        if (!claimed)
        {
            Redraw();
            return;
        }
        if (!saved) Debug.LogWarning("Mission reward was granted but not saved yet; it will be retried.", this);
        rewardReveal.Show(Slots.Result(result), BalanceBefore(result, CurrencyType.Gold), BalanceBefore(result, CurrencyType.Diamond),
            () => OnRevealClosed(missionId));
    }

    // Thẻ vừa nhận rời chỗ, vẽ lại theo thứ tự mới, rồi thẻ hiện ở cuối.
    private void OnRevealClosed(string missionId)
    {
        if (!WantsVisible) return;
        list.PlayClaimed(missionId, () =>
        {
            presenting = false;
            Redraw();
            list.PlayMoved(missionId);
        });
    }

    private MissionRuntime FindMission(string missionId)
    {
        MissionBoard board = Selected();
        if (board == null) return null;
        foreach (MissionRuntime mission in board.Missions)
            if (mission.Data.Id == missionId) return mission;
        return null;
    }

    // Tiền đã vào ví; màn nhận thưởng đếm từ số cũ.
    private long BalanceBefore(RewardResult result, CurrencyType type)
    {
        long balance = wallet.GetBalance(type);
        foreach (CurrencyCost currency in result.Currencies)
            if (currency.Currency == type) balance -= currency.Amount;
        return balance;
    }

    #endregion

    #region Drawing

    private void Redraw()
    {
        // Chỉ vẽ khi màn thật sự mở: vẽ là đánh dấu tiến độ "đã xem".
        if (presenting || !WantsVisible || !themeValid || missions == null || !isActiveAndEnabled || game.State != GameState.Home) return;
        openBoards.Clear();
        foreach (MissionBoard board in missions.Boards)
            if (board.IsOpen && openBoards.Count < list.TabCapacity) openBoards.Add(board);
        MissionBoard selected = Selected();
        selectedId = selected?.Database.Id;
        runningBars = 0;

        var tabs = new List<MissionTabModel>(openBoards.Count);
        foreach (MissionBoard board in openBoards)
            tabs.Add(new MissionTabModel(board.Database.DisplayName, board == selected, HasClaimable(board)));
        var cards = new List<MissionCardModel>();
        MissionCardModel? finalCard = null;
        string title = string.Empty, complete = string.Empty;
        if (selected != null)
        {
            title = string.Format(CultureInfo.InvariantCulture, titleFormat, selected.Database.DisplayName);
            complete = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", selected.ClaimedCount, selected.NormalCount);
            if (selected.FinalMission != null) finalCard = BuildCard(selected, selected.FinalMission);
            foreach (MissionState state in CardOrder)
                foreach (MissionRuntime mission in selected.Missions)
                    if (mission != selected.FinalMission && mission.State == state) cards.Add(BuildCard(selected, mission));
        }
        list.Bind(new MissionScreenModel(title, complete, tabs, finalCard, cards));
    }

    // Nhận được ở trên, đã nhận xuống cuối; cùng trạng thái thì giữ thứ tự trong database.
    private static readonly MissionState[] CardOrder = { MissionState.Claimable, MissionState.InProgress, MissionState.Claimed };

    // Tab đã chọn không còn mở thì về tab đầu.
    private MissionBoard Selected()
    {
        foreach (MissionBoard board in openBoards)
            if (board.Database.Id == selectedId) return board;
        return openBoards.Count > 0 ? openBoards[0] : null;
    }

    private static bool HasClaimable(MissionBoard board)
    {
        foreach (MissionRuntime mission in board.Missions)
            if (mission.State == MissionState.Claimable) return true;
        return false;
    }

    #endregion

    #region Model Building

    private MissionCardModel BuildCard(MissionBoard board, MissionRuntime mission)
    {
        var tasks = new List<MissionTaskModel>(mission.Data.Tasks.Count);
        for (int i = 0; i < mission.Data.Tasks.Count; i++)
            tasks.Add(BuildTask(board, mission, i));
        return new MissionCardModel(mission.Data.Id, mission.Data.DisplayName, mission.Data.Description, mission.State,
            ButtonText(mission.State), tasks, Slots.Preview(mission.Data.Rewards), FirstUnfinishedDestination(mission));
    }

    // Chỉ thẻ đang làm mới có nút Go; đi tới mission con chưa xong đầu tiên (từ trên xuống) để người chơi làm lần lượt.
    private static MissionDestination FirstUnfinishedDestination(MissionRuntime mission)
    {
        if (mission.State != MissionState.InProgress) return MissionDestination.None;
        for (int i = 0; i < mission.Data.Tasks.Count; i++)
        {
            MissionTask task = mission.Data.Tasks[i];
            if (mission.GetProgress(i) < task.Target) return task.Destination;
        }
        return MissionDestination.None;
    }

    private MissionTaskModel BuildTask(MissionBoard board, MissionRuntime mission, int index)
    {
        MissionTask task = mission.Data.Tasks[index];
        int progress = mission.GetProgress(index);
        string key = board.Database.Id + "/" + mission.Data.Id + "/" + task.Id;
        seen.TryGetValue(key, out int from);
        // Đã nhận thì thẻ xám, đứng yên; kỳ mới reset nên tiến độ có thể thấp hơn mức đã xem.
        if (from > progress || mission.State == MissionState.Claimed) from = progress;
        seen[key] = progress;
        float delay = from == progress ? 0f : barDelay + barStagger * runningBars++;
        return new MissionTaskModel(key, task.Description, from, progress, task.Target, delay);
    }

    private string ButtonText(MissionState state)
    {
        switch (state)
        {
            case MissionState.Claimable: return claimLabel;
            case MissionState.Claimed: return claimedLabel;
            default: return progressLabel;
        }
    }

    // Tạo một lần khi cần: theme và chữ lấy từ Inspector.
    private RewardSlotFactory Slots => slots ??= new RewardSlotFactory(theme, amountFormat, levelFormat, plainFrame);

    #endregion
}
